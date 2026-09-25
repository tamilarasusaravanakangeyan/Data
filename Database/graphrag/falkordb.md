# GraphRAG on FalkorDB

FalkorDB is an in-memory property-graph database, the successor to RedisGraph. It stores the graph as **sparse adjacency matrices** and runs traversals as linear algebra, which keeps multi-hop queries fast. It is marketed specifically as a low-latency GraphRAG store. It speaks **Cypher** over the **Redis protocol** and has built-in vector and full-text indexes. This guide uses the SkyWays Air example from the [GraphRAG overview](README.MD).

| | |
| --- | --- |
| **Model** | Labeled property graph, in memory (sparse matrices, GraphBLAS) |
| **Query language** | Cypher (openCypher subset plus extensions) |
| **Protocol** | Redis (`GRAPH.QUERY`, `GRAPH.RO_QUERY`); clients for Python, JS, Java, Go, Rust… |
| **Vector index** | HNSW, built in |
| **Full-text index** | Yes (RediSearch) |
| **Communities** | Not built in; compute outside and write back |
| **License** | SSPL v1: free, including commercial internal use. Offering FalkorDB itself as a public service triggers SSPL obligations. |

---

## 1. Data Model

Same as the other Cypher databases:

```text
(:Chunk {id, source, text, embedding: vecf32})
    -[:MENTIONS]->
(:Entity:FareFamily {id, name, type, description})  -[:INCLUDES {description}]->  (:Entity:Ancillary {...})
    -[:IN_COMMUNITY]->
(:Community {id, title, summary, embedding: vecf32})
```

One Redis server can hold many **named graphs** (`GRAPH.QUERY skyways ...`), which makes one graph per tenant or per document collection easy.

---

## 2. Building the Knowledge Base

### Indexes

```cypher
CREATE INDEX FOR (e:Entity) ON (e.id);
CREATE INDEX FOR (c:Chunk) ON (c.id);

CREATE VECTOR INDEX FOR (c:Chunk)     ON (c.embedding) OPTIONS {dimension: 6, similarityFunction: 'cosine'};
CREATE VECTOR INDEX FOR (k:Community) ON (k.embedding) OPTIONS {dimension: 6, similarityFunction: 'cosine'};

CALL db.idx.fulltext.createNodeIndex('Entity', 'name', 'description');
```

Indexes are identified by **label + property**, not by a name.

### Loading data

The main difference from Neo4j: **vectors must be stored with `vecf32(...)`**. A plain list is stored as a list and isn't indexed.

```cypher
CREATE (:Entity:FareFamily {id: 'E02', name: 'Economy Light', type: 'FareFamily',
        description: 'Cheapest fare family: cabin bag only, no free changes, non-refundable.'});

CREATE (:Chunk {id: 'C01', source: 'baggage-policy.pdf',
        text: 'Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45 online or USD 70 at the airport.',
        embedding: vecf32([0.90, 0.10, 0.00, 0.20, 0.00, 0.00])});

MATCH (c:Chunk {id: 'C01'}), (e:Entity {id: 'E02'}) CREATE (c)-[:MENTIONS]->(e);
```

Each `GRAPH.QUERY` command runs **one statement**. Load from a client library with parameters and `UNWIND $rows AS row`, rather than from a script file.

---

## 3. Retrieval Patterns

From `redis-cli`, parameters use a `CYPHER name=value` prefix; client libraries pass `params={...}` instead.

### 3.1 Vector RAG (baseline)

```cypher
CYPHER q=[0.95, 0.05, 0.00, 0.10, 0.00, 0.00]
CALL db.idx.vector.queryNodes('Chunk', 'embedding', 3, vecf32($q)) YIELD node AS chunk, score
RETURN chunk.id AS id, score AS distance, chunk.text AS text
ORDER BY distance;
```

| id | distance | text |
| --- | --- | --- |
| C01 | 0.008 | Economy Light fares include one 7 kg cabin bag only… |
| C02 | 0.112 | Economy Classic includes one 23 kg checked bag… |
| C03 | 0.437 | Business Flex includes two 32 kg checked bags… |

⚠️ **The score is a distance: lower is better.** Sort ascending, and use `1 - distance` if you need a similarity.

### 3.2 Local search: vector entry point + graph expansion

```cypher
CYPHER q=[0.95, 0.05, 0.00, 0.10, 0.00, 0.00]
CALL db.idx.vector.queryNodes('Chunk', 'embedding', 3, vecf32($q)) YIELD node AS chunk, score
MATCH (chunk)-[:MENTIONS]->(e:Entity)
OPTIONAL MATCH (e)-[r]-(n:Entity)
WITH e, min(score) AS distance, collect(DISTINCT chunk.id) AS chunks,
     collect(DISTINCT startNode(r).name + ' -[' + type(r) + ']-> ' + endNode(r).name + ': ' + r.description) AS facts
RETURN e.name AS entity, distance, chunks, facts
ORDER BY distance, entity;
```

Note `min(score)` rather than `max(score)`, because smaller distances are better.

| entity | distance | chunks |
| --- | --- | --- |
| Checked Bag 23kg | 0.008 | [C01, C02, C03] |
| Economy Light | 0.008 | [C01] |
| Economy Classic | 0.112 | [C02] |
| Business Flex, Lounge Pass, Onboard Wi-Fi | 0.437 | [C03] |

### 3.3 Multi-hop: answered by the graph alone

```cypher
MATCH (p:Entity)-[:INCLUDES]->(:Ancillary {name: 'Lounge Pass'})-[:AVAILABLE_AT]->(a:Airport)
RETURN p.name AS product, p.type AS type, collect(a.name) AS airports
ORDER BY product;
```

Result: `Business Flex` and `SkyMiles Gold`, both at `[Dubai DXB, London LHR]`. The query is identical to Neo4j's.

### 3.4 Global search: community summaries

```cypher
CYPHER q=[0.30, 0.40, 0.50, 0.20, 0.50, 0.20]
CALL db.idx.vector.queryNodes('Community', 'embedding', 3, vecf32($q)) YIELD node AS k, score
MATCH (e:Entity)-[:IN_COMMUNITY]->(k)
RETURN k.title AS community, score AS distance, k.summary AS summary, collect(e.name) AS members
ORDER BY distance;
```

Order: Premium experience (0.106), Fares and baggage (0.491), Ticket flexibility (0.749).

### 3.5 Hybrid: full-text entry point

```cypher
CALL db.idx.fulltext.queryNodes('Entity', 'lounge') YIELD node AS e
MATCH (e)-[r]-(n:Entity)
RETURN e.name AS entity, type(r) AS rel, n.name AS neighbour, r.description AS fact
ORDER BY entity, rel;
```

⚠️ The full-text tokenizer splits on hyphens: `Wi-Fi` is indexed as `wi` and `fi`, so a search for `wifi` finds nothing. Normalise such terms when loading, or store a keyword field.

---

## 4. Communities

FalkorDB has no community-detection procedure. Export the entity edges, run Leiden in Python (`igraph`, `graspologic`) or let a framework do it, then write the results back:

```cypher
UNWIND $members AS m
MATCH (e:Entity {id: m.entity_id})
MERGE (k:Community {id: m.community_id})
MERGE (e)-[:IN_COMMUNITY]->(k);
```

---

## 5. From an Application (Python)

```python
from falkordb import FalkorDB

graph = FalkorDB(host="localhost", port=6379).select_graph("skyways")
res = graph.ro_query(                                   # ro_query = read-only
    "CALL db.idx.vector.queryNodes('Chunk', 'embedding', 3, vecf32($q)) YIELD node, score "
    "RETURN node.id, score, node.text ORDER BY score",
    params={"q": embed(question)},
)
chunks = [(cid, 1 - dist, text) for cid, dist, text in res.result_set]   # distance -> similarity
```

FalkorDB's [`GraphRAG-SDK`](https://github.com/FalkorDB/GraphRAG-SDK) goes further: it can infer an ontology from your documents, extract entities with an LLM, and answer questions with generated Cypher.

---

## 6. Things to Know

- **The whole graph must fit in RAM**, including vectors. A 1536-dim float32 vector is about 6 KB, so 10 million chunks need about 60 GB for vectors alone.
- Persistence is Redis-style (RDB snapshots and AOF). Plan backups the same way as for Redis.
- It supports most of openCypher, but not everything; check the docs before porting complex Neo4j queries (e.g. APOC procedures don't exist).
- Use `GRAPH.RO_QUERY` / `ro_query` for retrieval, especially for LLM-generated queries, so they can't write.

## 7. Strengths and Weaknesses

| Strengths | Weaknesses |
| --- | --- |
| Very low latency for traversals and vector lookups | Graph and vectors must fit in memory |
| Cypher, so skills transfer from Neo4j | Smaller ecosystem and community than Neo4j |
| Many named graphs per server (multi-tenant GraphRAG) | No built-in graph algorithms for communities |
| GraphRAG-SDK for extraction and Q&A | SSPL license restricts offering it as a service |
