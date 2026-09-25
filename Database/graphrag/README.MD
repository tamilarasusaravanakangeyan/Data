# GraphRAG Databases

**GraphRAG** (graph retrieval-augmented generation) gives an LLM two kinds of context: text passages found by **vector search**, and connected **facts from a knowledge graph**. Plain vector RAG only returns passages that *look like* the question. GraphRAG can also follow relationships, so it can answer:

- **multi-hop questions**, such as "which products include lounge access, and at which airports?"
- **broad questions**, such as "what are the main ways to save money?", which no single passage answers.

This folder is learning material. It explains GraphRAG once, then shows **how the same GraphRAG knowledge base is built and queried in five different databases**, so you can compare their data models, query languages and trade-offs. For background, see [Graph Databases](../graph/README.MD) and [Vector Databases](../vectordatabase/README.MD).

| Guide | Database | In one line |
| --- | --- | --- |
| [neo4j.md](neo4j.md) | **Neo4j** | The reference GraphRAG store, with the most tooling and examples. |
| [falkordb.md](falkordb.md) | **FalkorDB** | In-memory graph built for low-latency GraphRAG. |
| [memgraph.md](memgraph.md) | **Memgraph** | In-memory graph with in-database algorithms (e.g. community detection). |
| [postgres-age.md](postgres-age.md) | **PostgreSQL + Apache AGE + pgvector** | GraphRAG without a new database, if you already run Postgres. |
| [arangodb.md](arangodb.md) | **ArangoDB** | Documents and graph in one engine and one query language. |

---

## 1. How GraphRAG Works

```text
                         ┌───────────── indexing (offline) ──────────────┐
 documents ──► chunks ──►│ embed chunks      extract entities + relations │──► database
                         │ (vectors)         (LLM)   ──► communities +    │    (vectors + graph)
                         │                            LLM summaries       │
                         └────────────────────────────────────────────────┘
                         ┌───────────── retrieval (per question) ─────────┐
 question ──► embed ────►│ vector search ──► entry nodes ──► traverse      │──► context ──► LLM ──► answer
                         └────────────────────────────────────────────────┘
```

**Indexing** (done once, then kept up to date):

1. **Chunk** the documents into passages of 300–800 tokens.
2. **Embed** each chunk with an embedding model.
3. **Extract** entities and relationships from each chunk with an LLM, using a fixed schema (allowed entity and relationship types).
4. **Resolve** duplicates ("Business Flex", "Biz Flex" → one node).
5. **Link** each chunk to the entities it mentions (`MENTIONS`), so every fact can cite its source.
6. **Cluster** the entity graph into communities (Leiden or Louvain), have an LLM summarise each community, and embed the summaries.

**Retrieval** (per question): embed the question, find entry points by vector (or keyword) search, traverse the graph from them, and send the text plus facts to the LLM.

---

## 2. Retrieval Patterns

| Pattern | How it works | Good for |
| --- | --- | --- |
| **Vector RAG** (baseline) | Return the chunks nearest to the question embedding. | Questions one passage can answer. |
| **Local search** | Vector search finds chunks, then the graph adds the entities they mention and their 1-hop relationships. | Specific questions that need related facts ("…and what does it cost?"). |
| **Graph-only / multi-hop** | Pure pattern match over the graph. | Questions that are really joins ("which X include Y, and where?"). |
| **Global search** | Rank the community summaries, answer from each (map), then combine the answers (reduce). | Broad "summarise everything" questions. |
| **Text2Cypher / Text2SQL** | The LLM writes the graph query itself. | Counts, aggregations and exact filters ("how many…"). Run it as a read-only user with a timeout. |
| **Path explanation** | Shortest path between two entities. | "How is X related to Y?" |
| **Hybrid entry point** | Full-text (keyword) search instead of, or as well as, vectors. | Exact terms such as product codes, names and acronyms. |

> Microsoft's [GraphRAG](https://github.com/microsoft/graphrag) project introduced the *local* / *global* / *DRIFT* search names. [LightRAG](https://github.com/HKUDS/LightRAG), [neo4j-graphrag](https://github.com/neo4j/neo4j-graphrag-python), LlamaIndex `PropertyGraphIndex` and LangChain `GraphCypherQAChain` implement the same ideas, and most of them can use the databases in this folder as storage.

---

## 3. The Example Used in Every Guide

Every guide uses the same small knowledge base for a fictional airline, **SkyWays Air**, so the queries can be compared line by line.

### Graph model

```text
(:Chunk {text, embedding}) -[:MENTIONS]-> (:Entity {name, type, description}) -[:IN_COMMUNITY]-> (:Community {summary, embedding})
                                                 │
                     OFFERS · INCLUDES · CAN_PURCHASE · APPLIES_TO · AVAILABLE_AT · HUB_AT
                                                 ▼
                                             (:Entity)
```

### Entities (13)

| Type | Entities |
| --- | --- |
| Airline | SkyWays Air |
| FareFamily | Economy Light, Economy Classic, Business Flex |
| Ancillary | Checked Bag 23kg, Extra Legroom Seat, Lounge Pass, Onboard Wi-Fi |
| Policy | Change Policy, Refund Policy |
| LoyaltyTier | SkyMiles Gold |
| Airport | Dubai DXB, London LHR |

### Some of the 18 relationships

Each relationship carries a one-sentence `description` that the LLM can quote.

```text
Economy Light   -[:CAN_PURCHASE]-> Checked Bag 23kg   "Economy Light excludes checked bags; one can be bought as an add-on."
Economy Classic -[:INCLUDES]->     Checked Bag 23kg   "Economy Classic includes one 23 kg checked bag."
Business Flex   -[:INCLUDES]->     Lounge Pass        "Business Flex includes lounge access."
SkyMiles Gold   -[:INCLUDES]->     Lounge Pass        "SkyMiles Gold members get complimentary lounge access."
Lounge Pass     -[:AVAILABLE_AT]-> Dubai DXB          "The SkyWays lounge is available at Dubai DXB."
Change Policy   -[:APPLIES_TO]->   Economy Light      "Economy Light changes cost USD 75 plus fare difference."
```

### Chunks (8) and communities (3)

| Id | Text (abridged) | Mentions |
| --- | --- | --- |
| C01 | Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45 online… | Economy Light, Checked Bag |
| C02 | Economy Classic includes one 23 kg checked bag and standard seat selection… | Economy Classic, Checked Bag |
| C03 | Business Flex includes two 32 kg checked bags, lounge access and free onboard Wi-Fi. | Business Flex, Checked Bag, Lounge Pass, Wi-Fi |
| C04 | Extra Legroom seats cost USD 30 to 80… SkyMiles Gold members select them free. | Extra Legroom Seat, SkyMiles Gold |
| C05 | Lounge passes are sold for USD 55 at Dubai DXB and London LHR… | Lounge Pass, SkyMiles Gold, DXB, LHR |
| C06 | Economy Light tickets are non-refundable. Date changes cost USD 75… | Economy Light, Change/Refund Policy |
| C07 | Business Flex tickets are fully refundable and can be changed free of charge… | Business Flex, Change/Refund Policy |
| C08 | Onboard Wi-Fi costs USD 8 for 1 hour or USD 20 for the full flight… | Onboard Wi-Fi, Business Flex |

| Community | Members | Summary (abridged) |
| --- | --- | --- |
| Fares and baggage | SkyWays Air, the 3 fare families, Checked Bag | Three fare families; Economy Light has no checked bag (USD 45 add-on)… |
| Premium experience | Seats, Lounge, Wi-Fi, SkyMiles Gold, DXB, LHR | Lounge, seats and Wi-Fi are sold as add-ons; Gold and Business Flex get most free. |
| Ticket flexibility | Change Policy, Refund Policy | Economy Light non-refundable, changes USD 75; Business Flex refundable, free changes. |

### Embeddings

To keep the examples readable, the embeddings are **6-dimensional toy vectors**, one dimension per topic: `[baggage, seating, lounge, change_refund, loyalty, wifi]`. For example, chunk C01 is `[0.90, 0.10, 0.00, 0.20, 0.00, 0.00]`. Real embedding models produce 384–3072 dimensions; nothing else in the queries changes.

### The two example questions

| Type | Question | Question vector |
| --- | --- | --- |
| Local | *Can I take a checked bag on an Economy Light ticket and what does it cost?* | `[0.95, 0.05, 0, 0.10, 0, 0]` |
| Global | *What are the main ways to pay less or get extras for free when flying SkyWays Air?* | `[0.30, 0.40, 0.50, 0.20, 0.50, 0.20]` |

With these inputs, **every database returns the same answer**: chunks C01, C02 and C03 (in that order), and then the same graph facts. For example, the local search adds *"Change Policy -[APPLIES_TO]-> Economy Light: Economy Light changes cost USD 75 plus fare difference"*, which the question didn't ask about but a good answer should mention. Plain vector search would miss it.

---

## 4. Comparing the Five Databases

| | Neo4j | FalkorDB | Memgraph | Postgres + AGE + pgvector | ArangoDB |
| --- | --- | --- | --- | --- | --- |
| **Data model** | Property graph | Property graph (sparse matrices) | Property graph | Relational tables + property graph | JSON documents + edges |
| **Storage** | Disk (page cache) | In-memory | In-memory (snapshots + WAL) | Disk | Disk (RocksDB) |
| **Query language** | Cypher / GQL | Cypher | Cypher | SQL with openCypher inside | AQL |
| **Vector index** | HNSW, built in | HNSW, built in | HNSW (usearch), built in | pgvector HNSW / IVFFlat on a table | IVF (FAISS), opt-in startup flag |
| **Full-text index** | ✅ | ✅ | ✅ (text search) | ✅ Postgres full-text | ✅ ArangoSearch |
| **Vectors and graph in one query** | ✅ | ✅ | ✅ | ✅ via SQL join | ✅ |
| **Community detection in the DB** | GDS plugin (Leiden, Louvain) | ❌ | ✅ MAGE (Louvain, Leiden) | ❌ | ❌ (Pregel removed in 3.12) |
| **GraphRAG frameworks** | Most (LangChain, LlamaIndex, MS GraphRAG, neo4j-graphrag) | GraphRAG-SDK, LangChain, LlamaIndex | LangChain, LlamaIndex | Few; build it yourself | LangChain, LlamaIndex |
| **Managed cloud** | AuraDB | FalkorDB Cloud | Memgraph Cloud | Azure Database for PostgreSQL (AGE supported) | ArangoGraph |
| **License (free edition)** | GPLv3 🟢 | SSPL 🟢 internal use | BSL 1.1 🟢 not as DBaaS | Apache 2.0 + PostgreSQL 🟢 | Community License 🟢 up to 100 GB |

### Which one to pick

| If you… | Pick |
| --- | --- |
| Want the most GraphRAG tooling, examples and hiring pool | **Neo4j** |
| Need the lowest query latency for an LLM app, and the graph fits in RAM | **FalkorDB** or **Memgraph** |
| Want graph algorithms (communities, PageRank) or streaming updates inside the DB | **Memgraph** (or Neo4j + GDS) |
| Already run PostgreSQL and don't want another database | **Postgres + AGE + pgvector** |
| Need documents *and* graph in one engine | **ArangoDB** |
| Are on a hyperscaler and want a fully managed service | **Amazon Neptune Analytics** (openCypher + vectors), **Google Spanner Graph** (GQL + vectors), **Oracle 23ai** (SQL/PGQ + AI Vector Search) or **Azure Cosmos DB** (vectors, with the graph kept elsewhere). See [Graph Databases](../graph/README.MD). |

### Scores are not on the same scale

All five return the same chunks in the same order, but the numbers mean different things:

| Database | What the vector search returns | Score of the best chunk (C01) |
| --- | --- | --- |
| Neo4j | `(1 + cosine) / 2`, higher is better | 0.996 |
| FalkorDB | cosine **distance**, lower is better | 0.008 |
| Memgraph | both `similarity` and `distance` | 0.992 |
| pgvector | `<=>` is cosine distance; `1 - distance` gives similarity | 0.992 |
| ArangoDB | `APPROX_NEAR_COSINE`, cosine similarity | 0.992 |

👉 Normalise scores before you apply a threshold, or mix results from different stores.

---

## 5. From Demo to Production

| Step | What to do | Watch out for |
| --- | --- | --- |
| **Chunking** | 300–800 tokens with overlap; keep `source`, page and section on each chunk. | Chunk quality affects answers more than the database choice. |
| **Embeddings** | Use the **same model** for indexing and for questions. | Changing the model means re-embedding everything. |
| **Entity & relation extraction** | Give the LLM a **fixed schema** of allowed types. | Free-form extraction creates near-duplicate types (`INCLUDES`, `INCLUDE`, `HAS`). |
| **Entity resolution** | Merge duplicates before loading (`MERGE` on a normalised key). | Duplicates split the graph and hide connections. |
| **Communities** | Leiden or Louvain, then an LLM summary per community, then embed the summary. | Summaries go stale; rebuild them when the graph changes a lot. |
| **Provenance** | Keep `Chunk -[:MENTIONS]-> Entity`. | Answers without citations are hard to trust or debug. |
| **Retrieval limits** | Cap traversal depth (1–2 hops) and the number of facts per entity. | **Supernodes** (e.g. the airline node) pull in the whole graph. |
| **Text2Cypher safety** | Read-only user, query timeouts, row limits. | Prompt injection in documents can reach generated queries. |
| **Evaluation** | Build a question set with expected answers; measure retrieval recall and answer faithfulness (e.g. RAGAS). | Compare against plain vector RAG; GraphRAG costs more to build. |
