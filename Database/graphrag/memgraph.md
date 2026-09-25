# GraphRAG on Memgraph

Memgraph is an in-memory property-graph database that speaks **Cypher** over **Bolt**, the same protocol as Neo4j, so Neo4j drivers work unchanged. Its **MAGE** library runs graph algorithms inside the database. For GraphRAG, that means Memgraph can **build the communities itself** and keep them updated as data streams in. This guide uses the SkyWays Air example from the [GraphRAG overview](README.MD).

| | |
| --- | --- |
| **Model** | Labeled property graph, in memory (snapshots + write-ahead log on disk) |
| **Query language** | Cypher (openCypher plus extensions) |
| **Protocol** | Bolt (Neo4j drivers work) |
| **Vector index** | HNSW (usearch library), built in |
| **Communities** | ✅ MAGE: Louvain (`community_detection`), Leiden, and more |
| **Streaming** | Native Kafka / Pulsar / Redpanda stream ingestion and triggers |
| **License** | Community: BSL 1.1 (free, including commercial use, but not as a hosted DB service). Enterprise: paid |

---

## 1. Data Model

The same property graph as Neo4j:

```text
(:Chunk {id, source, text, embedding})
    -[:MENTIONS]->
(:Entity:FareFamily {id, name, type, description})  -[:INCLUDES {description}]->  (:Entity:Ancillary {...})
    -[:IN_COMMUNITY]->
(:Community {id, title, summary, embedding})
```

---

## 2. Building the Knowledge Base

### Constraints and indexes

```cypher
CREATE CONSTRAINT ON (e:Entity) ASSERT e.id IS UNIQUE;
CREATE CONSTRAINT ON (c:Chunk)  ASSERT c.id IS UNIQUE;

// Memgraph constraints don't create indexes, so add them explicitly
CREATE INDEX ON :Entity(id);
CREATE INDEX ON :Chunk(id);

CREATE VECTOR INDEX chunk_embedding ON :Chunk(embedding)
WITH CONFIG {"dimension": 6, "capacity": 1000, "metric": "cos"};

CREATE VECTOR INDEX community_embedding ON :Community(embedding)
WITH CONFIG {"dimension": 6, "capacity": 1000, "metric": "cos"};
```

`capacity` sizes the index up front; set it to at least the number of nodes you expect to index.

### Loading data

Plain Cypher, and embeddings are ordinary lists:

```cypher
CREATE (:Chunk {id: 'C01', source: 'baggage-policy.pdf',
        text: 'Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45 online or USD 70 at the airport.',
        embedding: [0.90, 0.10, 0.00, 0.20, 0.00, 0.00]});

MATCH (c:Chunk {id: 'C01'}), (e:Entity {id: 'E02'}) CREATE (c)-[:MENTIONS]->(e);
```

---

## 3. Retrieval Patterns

### 3.1 Vector RAG (baseline)

```cypher
CALL vector_search.search("chunk_embedding", 3, [0.95, 0.05, 0.00, 0.10, 0.00, 0.00])
YIELD node AS chunk, similarity
RETURN chunk.id AS id, round(similarity * 1000) / 1000 AS similarity, chunk.text AS text
ORDER BY similarity DESC;
```

| id | similarity | text |
| --- | --- | --- |
| C01 | 0.992 | Economy Light fares include one 7 kg cabin bag only… |
| C02 | 0.888 | Economy Classic includes one 23 kg checked bag… |
| C03 | 0.563 | Business Flex includes two 32 kg checked bags… |

The procedure yields both `similarity` and `distance`. These are exact cosine values, unlike Neo4j's `(1 + cosine) / 2`. Add `ORDER BY` rather than relying on the procedure's output order.

### 3.2 Local search: vector entry point + graph expansion

```cypher
CALL vector_search.search("chunk_embedding", 3, [0.95, 0.05, 0.00, 0.10, 0.00, 0.00])
YIELD node AS chunk, similarity
MATCH (chunk)-[:MENTIONS]->(e:Entity)
OPTIONAL MATCH (e)-[r]-(n:Entity)
WITH e, max(similarity) AS similarity, collect(DISTINCT chunk.id) AS chunks,
     collect(DISTINCT startNode(r).name + ' -[' + type(r) + ']-> ' + endNode(r).name + ': ' + r.description) AS facts
RETURN e.name AS entity, round(similarity * 1000) / 1000 AS similarity, chunks, facts
ORDER BY similarity DESC, entity;
```

The result has the same entities and facts as Neo4j's (Checked Bag, Economy Light, Economy Classic, Business Flex, Lounge Pass, Onboard Wi-Fi).

### 3.3 Multi-hop: answered by the graph alone

```cypher
MATCH (p:Entity)-[:INCLUDES]->(:Ancillary {name: 'Lounge Pass'})-[:AVAILABLE_AT]->(a:Airport)
RETURN p.name AS product, p.type AS type, collect(a.name) AS airports
ORDER BY product;
```

Result: `Business Flex` and `SkyMiles Gold`, both at `[Dubai DXB, London LHR]`.

### 3.4 Global search: community summaries

```cypher
CALL vector_search.search("community_embedding", 3, [0.30, 0.40, 0.50, 0.20, 0.50, 0.20])
YIELD node AS k, similarity
MATCH (e:Entity)-[:IN_COMMUNITY]->(k)
RETURN k.title AS community, round(similarity * 1000) / 1000 AS similarity, k.summary AS summary, collect(e.name) AS members
ORDER BY similarity DESC;
```

Order: Premium experience (0.894), Fares and baggage (0.509), Ticket flexibility (0.251).

---

## 4. Building Communities Inside the Database

This is where Memgraph differs from the others. Louvain runs over just the entity graph, leaving chunks and communities out:

```cypher
MATCH (a:Entity)-[r]->(b:Entity)
WITH collect(DISTINCT a) + collect(DISTINCT b) AS nodes, collect(r) AS rels
CALL community_detection.get_subgraph(nodes, rels) YIELD node, community_id
RETURN community_id, collect(DISTINCT node.name) AS members
ORDER BY community_id;
```

| community_id | members |
| --- | --- |
| 0 | Checked Bag 23kg, Dubai DXB, Economy Classic, SkyWays Air |
| 1 | Onboard Wi-Fi, Refund Policy, Change Policy, Business Flex, Economy Light |
| 2 | Extra Legroom Seat, London LHR, SkyMiles Gold, Lounge Pass |

The algorithm's grouping differs from the hand-made communities in the example, which is normal: algorithms group by connectivity, people group by topic. The next steps are:

1. Write `community_id` back to the entities (`SET node.community = community_id`).
2. For each community, collect its entities and relationship descriptions, and ask the LLM for a summary.
3. Store the summary and its embedding on a `:Community` node.

With **triggers** and **stream ingestion**, Memgraph can re-run steps like these as new documents arrive.

---

## 5. From an Application (Python)

Because Memgraph speaks Bolt, the Neo4j driver works:

```python
from neo4j import GraphDatabase

with GraphDatabase.driver("bolt://localhost:7687", auth=("", "")) as driver:
    rows, _, _ = driver.execute_query(
        "CALL vector_search.search('chunk_embedding', 3, $q) YIELD node, similarity "
        "RETURN node.id AS id, similarity AS score, node.text AS text ORDER BY score DESC",
        q=embed(question),
    )
```

LangChain (`MemgraphGraph`) and LlamaIndex (`MemgraphPropertyGraphStore`) also have Memgraph integrations.

---

## 6. Things to Know

- **Everything lives in RAM**; size the machine for the full graph plus vectors. Snapshots and the WAL provide durability.
- The `memgraph-mage` image loads many Python modules at start-up, so it takes several seconds before it accepts connections.
- Cypher is very close to Neo4j's, but DDL differs (`CREATE CONSTRAINT ON ... ASSERT`, `CREATE INDEX ON :Label(prop)`, vector index `WITH CONFIG`), and APOC is replaced by MAGE modules.
- Memgraph Lab is the visual UI for exploring the graph and running queries.

## 7. Strengths and Weaknesses

| Strengths | Weaknesses |
| --- | --- |
| In-DB graph algorithms: communities, PageRank, centrality | Graph must fit in memory |
| Real-time: stream ingestion and triggers keep the graph fresh | Smaller ecosystem than Neo4j |
| Bolt and Cypher: Neo4j drivers and most skills transfer | DDL and procedure names differ from Neo4j |
| Low latency | BSL license forbids offering it as a DB service |
