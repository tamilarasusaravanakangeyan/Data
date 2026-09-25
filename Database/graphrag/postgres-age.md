# GraphRAG on PostgreSQL (Apache AGE + pgvector)

If you already run PostgreSQL, you may not need a separate graph database. Two extensions add the pieces GraphRAG needs:

- **pgvector** adds a `vector` column type and approximate nearest-neighbour indexes (HNSW, IVFFlat).
- **Apache AGE** adds a property graph that you query with **openCypher inside SQL**.

Chunk vectors live in ordinary tables, and entities and relationships live in the AGE graph. They share ids, so **one SQL statement can run vector search and graph expansion together**, next to your relational data and under one backup. This guide uses the SkyWays Air example from the [GraphRAG overview](README.MD).

| | |
| --- | --- |
| **Model** | Relational tables + property graph (AGE stores it in Postgres tables) |
| **Query language** | SQL, with openCypher in a `cypher()` function |
| **Vector index** | pgvector HNSW or IVFFlat, on a normal table |
| **Full-text** | Postgres full-text search (`tsvector`) |
| **Communities** | Not built in; compute outside and write back |
| **License** | PostgreSQL License + Apache 2.0 (AGE) + PostgreSQL License (pgvector): free for any use |

---

## 1. Data Model

```text
 pgvector tables (public schema)                 AGE graph 'skyways'
 ┌──────────────────────────────────┐             ┌──────────────────────────────────────────────┐
 │ chunk_embeddings                 │   same id   │ (:Chunk {id, source})                          │
 │   id, source, text, embedding ───┼─────────────┼──► -[:MENTIONS]-> (:Entity {id, name, type,    │
 │ community_embeddings             │             │                       description})            │
 │   id, title, summary, embedding ─┼─────────────┼──► (:Community {id, title}) <-[:IN_COMMUNITY]-  │
 └──────────────────────────────────┘             └──────────────────────────────────────────────┘
```

- AGE allows **one label per vertex**, so the entity type is a property (`type: 'FareFamily'`), not a second label as in Neo4j.
- The graph keeps only ids and small properties for chunks; the text and vectors stay in the tables, where pgvector can index them.

---

## 2. Building the Knowledge Base

### Setup (every session needs the last two lines)

```sql
CREATE EXTENSION IF NOT EXISTS age;
CREATE EXTENSION IF NOT EXISTS vector;
LOAD 'age';                                   -- or shared_preload_libraries = 'age'
SET search_path = ag_catalog, "$user", public;
```

### Vector tables

```sql
CREATE TABLE public.chunk_embeddings (
    id        text PRIMARY KEY,
    source    text,
    text      text,
    embedding vector(6)                         -- e.g. vector(1536) for real models
);
CREATE INDEX ON public.chunk_embeddings USING hnsw (embedding vector_cosine_ops);

INSERT INTO public.chunk_embeddings VALUES
  ('C01', 'baggage-policy.pdf',
   'Economy Light fares include one 7 kg cabin bag only. A 23 kg checked bag can be added for USD 45 online or USD 70 at the airport.',
   '[0.90, 0.10, 0.00, 0.20, 0.00, 0.00]');
```

### Graph

```sql
SELECT create_graph('skyways');

SELECT * FROM cypher('skyways', $$
    CREATE (:Entity {id: 'E02', name: 'Economy Light', type: 'FareFamily',
                     description: 'Cheapest fare family: cabin bag only, no free changes, non-refundable.'})
$$) AS (v agtype);

SELECT * FROM cypher('skyways', $$
    MATCH (a:Entity {id: 'E02'}), (b:Entity {id: 'E05'})
    CREATE (a)-[:CAN_PURCHASE {description: 'Economy Light excludes checked bags; one can be bought as an add-on.'}]->(b)
$$) AS (e agtype);

SELECT * FROM cypher('skyways', $$
    MATCH (c:Chunk {id: 'C01'}), (e:Entity {id: 'E02'}) CREATE (c)-[:MENTIONS]->(e)
$$) AS (e agtype);
```

Every `cypher()` call must declare its output columns (`AS (v agtype)`), even for writes.

---

## 3. Retrieval Patterns

### 3.1 Vector RAG (baseline)

`<=>` is cosine **distance**, so similarity is `1 - distance`:

```sql
SELECT id,
       round((1 - (embedding <=> '[0.95, 0.05, 0.00, 0.10, 0.00, 0.00]'))::numeric, 3) AS similarity,
       text
FROM public.chunk_embeddings
ORDER BY embedding <=> '[0.95, 0.05, 0.00, 0.10, 0.00, 0.00]'
LIMIT 3;
```

| id | similarity | text |
| --- | --- | --- |
| C01 | 0.992 | Economy Light fares include one 7 kg cabin bag only… |
| C02 | 0.888 | Economy Classic includes one 23 kg checked bag… |
| C03 | 0.563 | Business Flex includes two 32 kg checked bags… |

`ORDER BY embedding <=> ... LIMIT k` is the form that uses the HNSW index.

### 3.2 Local search: pgvector + Cypher in one SQL statement

```sql
WITH top_chunks AS (
    SELECT id, 1 - (embedding <=> '[0.95, 0.05, 0.00, 0.10, 0.00, 0.00]') AS similarity
    FROM public.chunk_embeddings
    ORDER BY embedding <=> '[0.95, 0.05, 0.00, 0.10, 0.00, 0.00]'
    LIMIT 3
),
graph AS (
    SELECT * FROM cypher('skyways', $$
        MATCH (c:Chunk)-[:MENTIONS]->(e:Entity)
        OPTIONAL MATCH (e)-[r]-(n:Entity)
        RETURN c.id, e.name,
               startNode(r).name + ' -[' + type(r) + ']-> ' + endNode(r).name + ': ' + r.description
    $$) AS (chunk_id agtype, entity agtype, fact agtype)
)
SELECT g.entity::text AS entity,
       round(max(t.similarity)::numeric, 3) AS similarity,
       array_agg(DISTINCT g.fact::text) AS facts
FROM top_chunks t
JOIN graph g ON g.chunk_id::text = t.id
GROUP BY g.entity::text
ORDER BY similarity DESC, entity;
```

| entity | similarity | facts (first one shown) |
| --- | --- | --- |
| Checked Bag 23kg | 0.992 | Business Flex -[INCLUDES]-> Checked Bag 23kg: Business Flex includes two 32 kg checked bags. |
| Economy Light | 0.992 | Change Policy -[APPLIES_TO]-> Economy Light: Economy Light changes cost USD 75 plus fare difference. |
| Economy Classic | 0.888 | Economy Classic -[INCLUDES]-> Checked Bag 23kg: … |
| Business Flex | 0.563 | … |

Cypher results have the type **`agtype`** (AGE's JSON-like type). Cast with `::text` to get plain text for joins and display.

⚠️ This form expands the *whole* graph and then joins, which is fine for learning and for small graphs. For large graphs, use the parameterised form below so only the top chunks are expanded.

### 3.3 Parameterised Cypher (what an application runs)

AGE takes Cypher parameters as **one agtype map**, and only through a prepared statement:

```sql
PREPARE expand_chunks(agtype) AS
SELECT * FROM cypher('skyways', $$
    MATCH (c:Chunk)-[:MENTIONS]->(e:Entity)
    WHERE c.id IN $ids
    RETURN DISTINCT e.name, e.type
$$, $1) AS (entity agtype, type agtype);

EXECUTE expand_chunks('{"ids": ["C01", "C02"]}');
```

Result: Economy Light, Checked Bag 23kg, Economy Classic.

### 3.4 Multi-hop: answered by the graph alone

```sql
SELECT * FROM cypher('skyways', $$
    MATCH (p:Entity)-[:INCLUDES]->(:Entity {name: 'Lounge Pass'})-[:AVAILABLE_AT]->(a:Entity)
    RETURN p.name, p.type, collect(a.name)
$$) AS (product agtype, type agtype, airports agtype)
ORDER BY product;
```

Result: `"Business Flex"` and `"SkyMiles Gold"`, both at `["Dubai DXB", "London LHR"]`. Note `(:Entity {name: ...})` instead of `(:Ancillary ...)`, because of the one-label rule.

### 3.5 Global search: community summaries

```sql
WITH members AS (
    SELECT * FROM cypher('skyways', $$
        MATCH (e:Entity)-[:IN_COMMUNITY]->(k:Community)
        RETURN k.id, collect(e.name)
    $$) AS (community_id agtype, members agtype)
)
SELECT c.title AS community,
       round((1 - (c.embedding <=> '[0.30, 0.40, 0.50, 0.20, 0.50, 0.20]'))::numeric, 3) AS similarity,
       c.summary,
       m.members
FROM public.community_embeddings c
JOIN members m ON m.community_id::text = c.id
ORDER BY c.embedding <=> '[0.30, 0.40, 0.50, 0.20, 0.50, 0.20]';
```

Order: Premium experience (0.894), Fares and baggage (0.509), Ticket flexibility (0.251).

---

## 4. From an Application (Python)

```python
import json
import psycopg

with psycopg.connect("postgresql://postgres:<password>@localhost:5432/graphrag") as conn:
    conn.execute("LOAD 'age'")
    conn.execute('SET search_path = ag_catalog, "$user", public')

    q = "[" + ",".join(map(str, embed(question))) + "]"
    chunks = conn.execute(
        "SELECT id, 1 - (embedding <=> %s::vector), text FROM public.chunk_embeddings "
        "ORDER BY embedding <=> %s::vector LIMIT 3", (q, q)).fetchall()

    facts = conn.execute(
        """SELECT fact::text FROM cypher('skyways', $$
               MATCH (c:Chunk)-[:MENTIONS]->(e:Entity)-[r]-(:Entity)
               WHERE c.id IN $ids
               RETURN DISTINCT type(r) + ': ' + r.description AS fact
           $$, %s) AS (fact agtype)""",
        (json.dumps({"ids": [c[0] for c in chunks]}),),
        prepare=True,   # AGE only accepts parameters in a prepared statement
    ).fetchall()
```

---

## 5. Things to Know

- **Session setup:** every connection needs `LOAD 'age'` (unless it is in `shared_preload_libraries`) and `ag_catalog` on the `search_path`.
- **Managed services:** pgvector is available on almost every managed Postgres. **AGE is not**: Azure Database for PostgreSQL Flexible Server supports it, but many others (e.g. Amazon RDS) don't. Check before choosing this route.
- AGE implements a **subset of openCypher**. There are no graph algorithms, and variable-length paths on big graphs can be slow. Keep traversals to 1–2 hops.
- The same questions can often be answered with **plain SQL** and recursive CTEs over `entities` and `relations` tables, without AGE. Use AGE when Cypher's pattern syntax makes the queries much clearer.
- Oracle 23ai (SQL/PGQ + AI Vector Search) and SQL Server graph tables follow the same "graph inside the relational database" idea.

## 6. Strengths and Weaknesses

| Strengths | Weaknesses |
| --- | --- |
| No new database: one backup, security model and team skill set | AGE isn't available on many managed Postgres services |
| Vectors, graph **and** relational data (customers, bookings) in one query | Clunkier syntax: `cypher()` wrappers, `agtype` casts, prepared-statement parameters |
| Fully open source, free for any use | No built-in graph algorithms; slower deep traversals than native graph DBs |
| pgvector is mature and widely supported | Few GraphRAG frameworks support AGE directly |
