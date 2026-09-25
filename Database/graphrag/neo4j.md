# GraphRAG on Neo4j

Neo4j is the most widely used native property-graph database, and the most common store for GraphRAG. Vectors, full-text search and the knowledge graph live on the same nodes, so one Cypher query can find text by similarity and then traverse from it. This guide uses the SkyWays Air example from the [GraphRAG overview](README.MD). For editions, hosting and installation, see [Neo4j](../graph/neo4j/README.MD).

| | |
| --- | --- |
| **Model** | Labeled property graph, stored natively |
| **Query language** | Cypher (GQL-aligned) |
| **Vector index** | HNSW (Apache Lucene), since 5.11 |
| **Full-text index** | Yes (Lucene) |
| **Communities** | Graph Data Science (GDS) plugin: Leiden, Louvain |
| **License** | Community Edition GPLv3 (free, incl. commercial); Enterprise and AuraDB are paid, with free tiers for development |

---

## 1. Data Model

```text
(:Chunk {id, source, text, embedding})
    -[:MENTIONS]->
(:Entity:FareFamily {id, name, type, description})  -[:INCLUDES {description}]->  (:Entity:Ancillary {...})
    -[:IN_COMMUNITY]->
(:Community {id, title, summary, embedding})
```

- Every entity has the shared label `:Entity` plus a label for its type (`:FareFamily`, `:Ancillary`, …). The shared label gives one place for constraints and indexes; the type label keeps pattern matches fast.
- Relationship types are specific (`INCLUDES`, `CAN_PURCHASE`), not a generic `RELATED_TO {type: ...}`. Neo4j filters on relationship type very cheaply.
- Embeddings are ordinary `LIST<FLOAT>` properties on `Chunk` and `Community` nodes.

---

## 2. Building the Knowledge Base

### Constraints and indexes

```cypher
CREATE CONSTRAINT entity_id    IF NOT EXISTS FOR (e:Entity)    REQUIRE e.id IS UNIQUE;
CREATE CONSTRAINT chunk_id     IF NOT EXISTS FOR (c:Chunk)     REQUIRE c.id IS UNIQUE;
CREATE CONSTRAINT community_id IF NOT EXISTS FOR (k:Community) REQUIRE k.id IS UNIQUE;

// Vector indexes: dimensions must match the embedding model (e.g. 1536)
CREATE VECTOR INDEX chunk_embedding IF NOT EXISTS FOR (c:Chunk) ON c.embedding
OPTIONS {indexConfig: {`vector.dimensions`: 6, `vector.similarity_function`: 'cosine'}};

CREATE VECTOR INDEX community_embedding IF NOT EXISTS FOR (k:Community) ON k.embedding
OPTIONS {indexConfig: {`vector.dimensions`: 6, `vector.similarity_function`: 'cosine'}};

// Keyword entry point for exact names and codes
CREATE FULLTEXT INDEX entity_text IF NOT EXISTS FOR (e:Entity) ON EACH [e.name, e.description];
```

### Loading data

```cypher
CREATE (:Entity:FareFamily {id: 'E02', name: 'Economy Light', type: 'FareFamily',
        description: 'Cheapest fare family: cabin bag only, no free changes, non-refundable.'});

MATCH (a:Entity {id: 'E02'}), (b:Entity {id: 'E05'})
CREATE (a)-[:CAN_PURCHASE {description: 'Economy Light excludes checked bags; one can be bought as an add-on.'}]->(b);

CREATE (:Chunk {id: 'C01', source: 'baggage-policy.pdf',
        text: 'Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45 online or USD 70 at the airport.',
        embedding: [0.90, 0.10, 0.00, 0.20, 0.00, 0.00]});

MATCH (c:Chunk {id: 'C01'}), (e:Entity {id: 'E02'}) CREATE (c)-[:MENTIONS]->(e);
```

In a real pipeline, use `MERGE` instead of `CREATE` so that re-running extraction doesn't create duplicates, and load in batches with `UNWIND $rows AS row`.

> Vector indexes fill in the background. After a bulk load, run `CALL db.awaitIndexes()` before querying.

---

## 3. Retrieval Patterns

### 3.1 Vector RAG (baseline)

```cypher
:param q => [0.95, 0.05, 0.00, 0.10, 0.00, 0.00];

CALL db.index.vector.queryNodes('chunk_embedding', 3, $q) YIELD node AS chunk, score
RETURN chunk.id AS id, round(score, 3) AS score, chunk.text AS text;
```

| id | score | text |
| --- | --- | --- |
| C01 | 0.996 | Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45… |
| C02 | 0.944 | Economy Classic includes one 23 kg checked bag… |
| C03 | 0.800 | Business Flex includes two 32 kg checked bags, lounge access and free onboard Wi-Fi. |

### 3.2 Local search: vector entry point + graph expansion

The vector search finds the chunks. The graph then adds the entities they mention and every fact one hop away from those entities.

```cypher
CALL db.index.vector.queryNodes('chunk_embedding', 3, $q) YIELD node AS chunk, score
MATCH (chunk)-[:MENTIONS]->(e:Entity)
OPTIONAL MATCH (e)-[r]-(n:Entity)
WITH e, max(score) AS score, collect(DISTINCT chunk.text) AS chunks,
     collect(DISTINCT startNode(r).name + ' -[' + type(r) + ']-> ' + endNode(r).name + ': ' + r.description) AS facts
RETURN e.name AS entity, e.type AS type, round(score, 3) AS score, chunks, facts
ORDER BY score DESC, entity;
```

Part of the result for `Economy Light`:

```text
Economy Light -[CAN_PURCHASE]-> Checked Bag 23kg: Economy Light excludes checked bags; one can be bought as an add-on.
Change Policy -[APPLIES_TO]-> Economy Light: Economy Light changes cost USD 75 plus fare difference.
Refund Policy -[APPLIES_TO]-> Economy Light: Economy Light is non-refundable.
SkyWays Air -[OFFERS]-> Economy Light: SkyWays Air sells the Economy Light fare family.
```

`startNode(r)` and `endNode(r)` keep the direction readable even though the pattern `(e)-[r]-(n)` ignores it.

### 3.3 Multi-hop: answered by the graph alone

*"Which fare families or loyalty tiers include lounge access, and at which airports?"*

```cypher
MATCH (p:Entity)-[:INCLUDES]->(:Ancillary {name: 'Lounge Pass'})-[:AVAILABLE_AT]->(a:Airport)
RETURN p.name AS product, p.type AS type, collect(a.name) AS airports
ORDER BY product;
```

| product | type | airports |
| --- | --- | --- |
| Business Flex | FareFamily | [Dubai DXB, London LHR] |
| SkyMiles Gold | LoyaltyTier | [Dubai DXB, London LHR] |

No single chunk says this; it needs two hops.

### 3.4 Global search: community summaries

```cypher
CALL db.index.vector.queryNodes('community_embedding', 3, [0.30, 0.40, 0.50, 0.20, 0.50, 0.20])
YIELD node AS k, score
MATCH (e:Entity)-[:IN_COMMUNITY]->(k)
RETURN k.title AS community, round(score, 3) AS score, k.summary AS summary, collect(e.name) AS members
ORDER BY score DESC;
```

| community | score |
| --- | --- |
| Premium experience | 0.947 |
| Fares and baggage | 0.756 |
| Ticket flexibility | 0.626 |

Each summary goes to the LLM with the question (map); the partial answers are then combined into one (reduce).

### 3.5 Hybrid: full-text entry point

```cypher
CALL db.index.fulltext.queryNodes('entity_text', 'wifi OR "Wi-Fi"') YIELD node AS e, score
MATCH (e)-[r]-(n:Entity)
RETURN e.name AS entity, type(r) AS rel, n.name AS neighbour, r.description AS fact;
```

Use this for exact terms (fare codes, flight numbers) that embeddings handle poorly. Combine it with vector search by merging the two result lists, for example with reciprocal rank fusion.

---

## 4. Building Communities with GDS

The example loads communities that were computed in advance. With the Graph Data Science plugin, Neo4j can compute them itself:

```cypher
// Project the entity graph (chunks and communities excluded) into memory
MATCH (a:Entity)-[r]->(b:Entity)
WITH gds.graph.project('entities', a, b, {}, {undirectedRelationshipTypes: ['*']}) AS g
RETURN g.graphName;

// Leiden writes a communityId property on every entity
CALL gds.leiden.write('entities', {writeProperty: 'communityId'});
```

Then group entities by `communityId`, ask the LLM to summarise each group, and store the summaries as `:Community` nodes with embeddings.

---

## 5. From an Application (Python)

```python
from neo4j import GraphDatabase

EXPAND = """
MATCH (c:Chunk)-[:MENTIONS]->(e:Entity)-[r]-(:Entity)
WHERE c.id IN $ids
RETURN DISTINCT startNode(r).name + ' -[' + type(r) + ']-> ' + endNode(r).name + ': ' + r.description AS fact
"""

with GraphDatabase.driver("bolt://localhost:7687", auth=("neo4j", "<password>")) as driver:
    q = embed(question)  # the SAME embedding model used for the chunks
    rows, _, _ = driver.execute_query(
        "CALL db.index.vector.queryNodes('chunk_embedding', 3, $q) YIELD node, score "
        "RETURN node.id AS id, score, node.text AS text", q=q)
    facts, _, _ = driver.execute_query(EXPAND, ids=[r["id"] for r in rows])
    context = [r["text"] for r in rows] + [f["fact"] for f in facts]
```

The official [`neo4j-graphrag`](https://github.com/neo4j/neo4j-graphrag-python) package wraps this pattern (`VectorCypherRetriever`) and also has a knowledge-graph builder that does the LLM extraction for you.

---

## 6. Things to Know

- **Scores are `(1 + cosine) / 2`**, so they range from 0 to 1 and look higher than raw cosine (0.996 here is cosine 0.992).
- **Vectors are quantized by default** (5.23+), which saves memory but makes scores approximate: C03 scores 0.800 instead of the exact 0.782. The ranking doesn't change. Add `` `vector.quantization.enabled`: false `` to `indexConfig` for exact scores.
- The vector index is **approximate** (HNSW). Ask for more results than you need (e.g. 20), then filter or rerank.
- **Supernodes:** `SkyWays Air` connects to every fare. On real data, cap expansion (`LIMIT` per entity, or skip nodes with very high degree).
- Community Edition runs a single instance only; clustering, hot backups and RBAC need Enterprise or AuraDB. GDS Community is limited to 4 CPU cores.

## 7. Strengths and Weaknesses

| Strengths | Weaknesses |
| --- | --- |
| Most mature GraphRAG ecosystem: libraries, examples, courses | Enterprise features (clustering, RBAC) are paid |
| Vectors, full-text and graph in one query | Another database to run if you don't already use Neo4j |
| GDS for Leiden, PageRank and similarity algorithms | Disk-based, so slower per query than the in-memory engines at small scale |
| Managed option (AuraDB) on AWS, Azure and GCP | Very large graphs need careful memory sizing (page cache) |
