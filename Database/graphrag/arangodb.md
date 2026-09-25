# GraphRAG on ArangoDB

ArangoDB is a **multi-model** database. Chunks, entities and communities are **JSON documents**, relationships are **edge documents**, and one query language, **AQL**, handles document filters, graph traversals and (since 3.12.4) **vector search**. This suits GraphRAG when chunks carry rich, nested metadata that you want to filter on alongside the graph. This guide uses the SkyWays Air example from the [GraphRAG overview](README.MD).

| | |
| --- | --- |
| **Model** | Documents + graph (edges are documents with `_from` and `_to`) + key-value |
| **Query language** | AQL |
| **Vector index** | IVF (FAISS), 3.12.4+, enabled with a startup option |
| **Full-text** | ArangoSearch (inverted indexes, BM25) |
| **Communities** | Not built in for Community Edition (Pregel was removed in 3.12); compute outside |
| **License** | Community Edition: free, including commercial use, up to a 100 GB dataset. Enterprise and ArangoGraph (cloud): paid |

---

## 1. Data Model

```text
 document collections           edge collections
 ┌──────────────┐   mentions    ┌──────────────┐   relations {type, description}
 │ chunks       │──────────────►│ entities     │◄────────────────────────────┐
 │ _key, source │               │ _key, name,  │─────────────────────────────┘
 │ text,        │               │ type,        │   in_community
 │ embedding    │               │ description  │──────────────────► communities {_key, title, summary, embedding}
 └──────────────┘               └──────────────┘
```

- Chunks, entities and communities are separate **document collections**; `mentions`, `relations` and `in_community` are **edge collections**.
- All entity-to-entity relationships share one `relations` collection with a `type` attribute (`INCLUDES`, `CAN_PURCHASE`, …). That keeps traversals simple; add a persistent index on `type` for filtering.
- A **named graph** (`skyways`) declares which edge collections connect which document collections. The web UI uses it to draw the graph.

---

## 2. Building the Knowledge Base

In `arangosh` (the JavaScript shell):

```js
const graphModule = require('@arangodb/general-graph');
db._createDatabase('graphrag');
db._useDatabase('graphrag');

graphModule._create('skyways', [
  graphModule._relation('relations',    ['entities'], ['entities']),
  graphModule._relation('mentions',     ['chunks'],   ['entities']),
  graphModule._relation('in_community', ['entities'], ['communities']),
], ['chunks', 'communities']);

db.entities.insert({ _key: 'E02', name: 'Economy Light', type: 'FareFamily',
                     description: 'Cheapest fare family: cabin bag only, no free changes, non-refundable.' });

db.chunks.insert({ _key: 'C01', source: 'baggage-policy.pdf',
                   text: 'Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45 online or USD 70 at the airport.',
                   embedding: [0.90, 0.10, 0.00, 0.20, 0.00, 0.00] });

db.relations.insert({ _from: 'entities/E02', _to: 'entities/E05', type: 'CAN_PURCHASE',
                      description: 'Economy Light excludes checked bags; one can be bought as an add-on.' });
db.mentions.insert({ _from: 'chunks/C01', _to: 'entities/E02' });

db.relations.ensureIndex({ type: 'persistent', fields: ['type'] });
```

### Vector index: create it after loading

The vector index is **IVF**: it clusters the existing vectors when it is created. So load the data first, then create the index.

```js
db.chunks.ensureIndex({
  name: 'chunk_embedding', type: 'vector', fields: ['embedding'],
  params: { metric: 'cosine', dimension: 6, nLists: 2 }
});
```

- `nLists` is the number of clusters. It must not exceed the number of documents; about `sqrt(N)` is a sensible start on real data.
- Vector indexes are **off by default**. The server must start with `--vector-index true`, or `ensureIndex` fails.

---

## 3. Retrieval Patterns

### 3.1 Vector RAG (baseline)

```aql
FOR c IN chunks
  LET similarity = APPROX_NEAR_COSINE(c.embedding, @q)
  SORT similarity DESC
  LIMIT 3
  RETURN { id: c._key, similarity: ROUND(similarity * 1000) / 1000, text: c.text }
```

With `@q = [0.95, 0.05, 0.00, 0.10, 0.00, 0.00]`:

```json
{"id":"C01","similarity":0.992,"text":"Economy Light fares include one 7 kg cabin bag only. ..."}
{"id":"C02","similarity":0.888,"text":"Economy Classic includes one 23 kg checked bag ..."}
{"id":"C03","similarity":0.563,"text":"Business Flex includes two 32 kg checked bags ..."}
```

`APPROX_NEAR_COSINE` uses the index only when it is followed directly by `SORT ... DESC` and `LIMIT`. Recall can be tuned with the `nProbe` option (how many clusters are searched).

### 3.2 Local search: vector entry point + graph traversal

```aql
FOR c IN chunks
  LET similarity = APPROX_NEAR_COSINE(c.embedding, @q)
  SORT similarity DESC
  LIMIT 3
  FOR e IN 1..1 OUTBOUND c mentions
    COLLECT entity = e INTO hits = similarity
    LET facts = (
      FOR n, r IN 1..1 ANY entity relations
        RETURN CONCAT(DOCUMENT(r._from).name, ' -[', r.type, ']-> ', DOCUMENT(r._to).name, ': ', r.description)
    )
    SORT MAX(hits) DESC, entity.name
    RETURN { entity: entity.name, similarity: ROUND(MAX(hits) * 1000) / 1000, facts }
```

`FOR vertex, edge IN min..max DIRECTION startVertex edgeCollection` is AQL's traversal: here, one hop `OUTBOUND` from each chunk along `mentions`, then one hop in `ANY` direction along `relations`. The result has the same entities and facts as the other databases.

### 3.3 Multi-hop: answered by the graph alone

```aql
FOR lounge IN entities FILTER lounge.name == 'Lounge Pass'
  FOR p, r IN 1..1 INBOUND lounge relations
    FILTER r.type == 'INCLUDES'
    LET airports = (
      FOR a, r2 IN 1..1 OUTBOUND lounge relations FILTER r2.type == 'AVAILABLE_AT' RETURN a.name
    )
    SORT p.name
    RETURN { product: p.name, type: p.type, airports }
```

```json
{"product":"Business Flex","type":"FareFamily","airports":["London LHR","Dubai DXB"]}
{"product":"SkyMiles Gold","type":"LoyaltyTier","airports":["London LHR","Dubai DXB"]}
```

### 3.4 Global search: community summaries

```aql
FOR k IN communities
  LET similarity = APPROX_NEAR_COSINE(k.embedding, @q)
  SORT similarity DESC
  LIMIT 3
  LET members = (FOR e IN 1..1 INBOUND k in_community RETURN e.name)
  RETURN { community: k.title, similarity: ROUND(similarity * 1000) / 1000, summary: k.summary, members }
```

Order: Premium experience (0.894), Fares and baggage (0.509), Ticket flexibility (0.251).

### 3.5 Path explanation: how are two things connected?

```aql
FOR v, e IN ANY SHORTEST_PATH 'entities/E02' TO 'entities/E13' relations
  RETURN { node: v.name, via: e.type }
```

```json
{"node":"Economy Light","via":null}
{"node":"Lounge Pass","via":"CAN_PURCHASE"}
{"node":"London LHR","via":"AVAILABLE_AT"}
```

Useful context for "why" and "how is X related to Y" questions: Economy Light passengers can *buy* a lounge pass, and the lounge is available at LHR.

---

## 4. From an Application (Python)

```python
from arango import ArangoClient

db = ArangoClient(hosts="http://localhost:8529").db("graphrag", username="root", password="<password>")
chunks = list(db.aql.execute(
    """FOR c IN chunks
         LET score = APPROX_NEAR_COSINE(c.embedding, @q)
         SORT score DESC LIMIT 3
         RETURN [c._key, score, c.text]""",
    bind_vars={"q": embed(question)},
))
```

LangChain (`ArangoGraph`, `ArangoVector`) and LlamaIndex have ArangoDB integrations.

---

## 5. Things to Know

- `--vector-index true` must be set at server start (3.12.4+). On ArangoGraph (the managed cloud), check that vector indexes are enabled for your deployment.
- IVF needs **training data**: create the index after a representative load, and rebuild it if the data changes a lot.
- **Communities:** Pregel was removed in 3.12. Export the `relations` edges to Python (NetworkX, `igraph` Leiden), then write `in_community` edges back.
- Documents are schemaless by default; add JSON Schema validation on collections to keep extracted entities consistent.
- AQL is ArangoDB-specific; queries don't port to Cypher databases.

## 6. Strengths and Weaknesses

| Strengths | Weaknesses |
| --- | --- |
| Documents, graph, vectors and full-text in one engine and one query | AQL is proprietary, so there is more lock-in than with Cypher/GQL |
| Rich JSON metadata on chunks, filterable in the same query | Vector index is IVF (needs training) and opt-in |
| Built-in shortest path and k-paths traversals | No built-in community detection in 3.12 |
| Free Community Edition, up to 100 GB | Community Edition license limits dataset size |
