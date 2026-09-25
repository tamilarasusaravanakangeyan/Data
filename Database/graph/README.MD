# Graph Databases

A graph database stores data as **nodes** (entities), **edges** (relationships) and **properties** (attributes on either). Relationships are stored as first-class data, so following them ("who knows who, who bought what, which account sent money to which") is cheap. In a relational database the same query needs a chain of JOINs that gets slower with every hop.

```text
(Customer:Alice) -[:BOOKED]-> (Flight:EK501) -[:OPERATED_BY]-> (Airline:Emirates)
        |                                              ^
   [:FRIEND_OF]                                        |
        v                                              |
(Customer:Bob)   -[:BOOKED]-> (Flight:EK502) ----------+
```

---

## 1. Types of Graph Data Models

| Model | What it is | Query language(s) | When to use (one-liner) | Enterprise adoption |
| --- | --- | --- | --- | --- |
| **Labeled Property Graph (LPG)** | Nodes and edges carry labels plus key-value properties. The most common model. | Cypher / openCypher, **GQL** (ISO standard), Gremlin | Use it for application workloads such as fraud, recommendations, network/IT topology and identity/access graphs. | **Very high.** This is the mainstream choice. |
| **RDF / Triple Store** | Data is stored as `subject → predicate → object` triples using global URIs, with ontologies (OWL/RDFS) layered on top. | SPARQL | Use it when you need semantic interoperability, ontologies, reasoning/inference and linked data across organizations. | **Medium, niche but deep.** Common in pharma, life sciences, government, publishing and finance reference data. |
| **Hypergraph** | One edge can connect **more than two** nodes. | Vendor-specific | Use it for complex many-to-many relationships such as a transaction that involves buyer, seller, broker and bank at once. | **Low.** Mostly academic or specialized; you can usually model this in LPG with an intermediate node. |
| **Multi-model (graph + doc/KV)** | A single engine that exposes graph alongside document, key-value or relational models. | AQL, Gremlin, SQL extensions | Use it when graph is *one* of several access patterns and you don't want to run another database. | **High,** mainly through cloud/relational vendors. |
| **Graph on relational (SQL/PGQ)** | You define a property-graph *view* over existing relational tables and query it with graph syntax. | SQL/PGQ (SQL:2023), SQL Server `MATCH` | Use it when the data already lives in Oracle, SQL Server or Postgres and you only need occasional graph queries. | **Growing fast.** It's the lowest-friction way in for enterprises. |

### Native vs non-native graph storage

| | Native graph | Non-native (graph layer on another store) |
| --- | --- | --- |
| **How** | Storage is built around adjacency: each node points directly to its neighbours ("index-free adjacency"). | Graph API on top of a KV, document, column or relational engine. |
| **Examples** | Neo4j, TigerGraph, Memgraph | JanusGraph (on Cassandra/HBase), Cosmos DB Gremlin, SQL Server Graph |
| **Pros** | Fastest deep traversals (3+ hops) | Reuses existing scale, ops and backup tooling |
| **Cons** | Another engine to run and learn | Deep traversals can be slower and costlier |

---

## 2. Graph Database Products

| Product | Model | Query language | Deployment | When to use (one-liner) | Widely used in enterprise? | License | Free to use (commercial vs non-commercial)? |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Neo4j** | LPG (native) | Cypher, GQL | Self-managed, AuraDB (cloud) | The default pick for general-purpose graph OLTP, with the largest community and tooling. | ✅ **Yes, market leader** | Community: GPLv3 (open source). Enterprise: commercial | 🟢 **Yes, Community Edition** is free for commercial use. Clustering, RBAC and hot backup need a paid Enterprise license (free only for dev/eval). AuraDB has a free cloud tier. |
| **Amazon Neptune** | LPG + RDF | Gremlin, openCypher, SPARQL | AWS managed | Use it if you're on AWS and want a managed graph without running servers. Neptune Analytics covers in-memory algorithms. | ✅ **Yes** (AWS shops) | Proprietary (AWS service) | 🔴 **No.** Pay-as-you-go, with a limited AWS free trial. |
| **Azure Cosmos DB for Apache Gremlin** | LPG (non-native) | Gremlin | Azure managed | Use it for Azure-native apps that need a globally distributed, elastic graph with modest traversal depth. | ⚠️ **Moderate.** Check the current Microsoft roadmap before new builds. | Proprietary (Azure service) | 🟡 **Free tier:** the first 1000 RU/s and 25 GB per account are free, and the local emulator is free for dev. Beyond that it is paid. |
| **Microsoft Fabric Graph** | LPG over OneLake | GQL | Fabric SaaS | Use it for graph analytics over data already in the Fabric/OneLake lakehouse. | 🆕 **Emerging** | Proprietary (Fabric SaaS) | 🔴 **No.** Needs Fabric capacity; a trial is available. |
| **Oracle Database (23ai) Property Graph** | LPG over relational | SQL/PGQ, PGQL | On-prem, OCI, Autonomous DB | Use it when data is already in Oracle and you want graph queries without moving it. | ✅ **Yes** (Oracle estates) | Proprietary. Graph is included in all editions at no extra cost | 🟡 **Oracle Database Free** is free under Oracle Free Use terms but resource-limited (2 CPU, 2 GB RAM, 12 GB data). Otherwise it needs a normal DB license. |
| **SQL Server / Azure SQL Graph** | Node/edge tables | T-SQL `MATCH` | On-prem, Azure SQL | Use it for light graph queries (1–3 hops) inside an existing SQL Server app. | ⚠️ **Moderate.** It's used, but rarely for heavy graph work. | Proprietary. Graph is included in all editions | 🟡 **Express** is free for commercial use but size-limited. **Developer** is free for non-production only. Otherwise paid. |
| **Google Spanner Graph** | LPG over Spanner | GQL, SQL | GCP managed | Use it for globally consistent, horizontally scaled graph plus relational data on GCP. | 🆕 **Growing** | Proprietary (GCP service) | 🔴 **No.** Paid, with a free-trial instance. |
| **TigerGraph** | LPG (native, MPP) | GSQL, openCypher/GQL | Self-managed, cloud | Use it for very large graphs with deep-link analytics (fraud rings, AML, supply chain) at scale. | ✅ **Yes** (banking, telco) | Proprietary | 🟡 **Community Edition** is free with graph-size limits; check its terms for commercial production. Enterprise is paid, and Savanna cloud has a free tier. |
| **ArangoDB** | Multi-model (doc + graph + KV) | AQL | Self-managed, ArangoGraph cloud | Use it when you need document *and* graph in one engine. | ⚠️ **Moderate** | Source: BSL 1.1. Community binaries: ArangoDB Community License | 🟡 **Community** is free, including commercial use, up to a **100 GB** dataset, and it can't be resold as a managed service. Larger datasets need a paid license. |
| **JanusGraph** | LPG (on Cassandra/HBase/ScyllaDB) | Gremlin | Self-managed (open source) | Use it for open-source, massive-scale graphs when you already run Cassandra or HBase. | ⚠️ **Moderate.** It needs strong in-house ops skills. | Apache 2.0 | 🟢 **Yes.** Fully open source, free for any use. |
| **Memgraph** | LPG (in-memory, native) | Cypher | Self-managed, cloud | Use it for real-time and streaming graph analytics (Kafka in, low-latency answers out). | ⚠️ **Growing** | Community: BSL 1.1. Enterprise: commercial | 🟢 **Community** is free, including commercial use, but you can't offer it as a DBaaS. Enterprise features are paid. |
| **NebulaGraph** | LPG (distributed) | nGQL, openCypher | Self-managed, cloud | Use it for distributed graphs with billions of edges. Popular in APAC internet companies. | ⚠️ **Moderate** (strong in Asia) | Core: Apache 2.0. Enterprise: commercial | 🟢 **Yes, core** is free for any use. Enterprise tools and features are paid. |
| **FalkorDB** (RedisGraph successor) | LPG (sparse-matrix, in-memory) | Cypher | Self-managed, cloud | Use it for low-latency GraphRAG and knowledge-graph lookups for LLM apps. | 🆕 **Emerging** | SSPL v1 | 🟢 **Free**, including commercial internal use. Offering it as a public service triggers SSPL obligations. |
| **Apache AGE** (PostgreSQL extension) | LPG over Postgres | openCypher inside SQL | Anywhere Postgres runs | Use it to add graph queries to an existing PostgreSQL database. | ⚠️ **Niche/growing** | Apache 2.0 | 🟢 **Yes.** Fully open source, free for any use. |
| **Ontotext GraphDB** | RDF | SPARQL | Self-managed, cloud | Use it for enterprise knowledge graphs with ontologies and reasoning. | ✅ **Yes** (semantic/KG niche) | Proprietary (vendor is now Graphwise) | 🟡 **Free edition** is usable commercially but limited in concurrency and features. Enterprise is paid. |
| **Stardog** | RDF + virtual graph | SPARQL, GraphQL | Self-managed, cloud | Use it for data-fabric scenarios that virtualize graph over existing sources without copying data. | ⚠️ **Moderate** | Proprietary | 🟠 **Free license is for non-commercial, academic or evaluation use.** Commercial use is paid. Stardog Cloud has a free tier. |
| **AllegroGraph** | RDF + vector | SPARQL, Prolog | Self-managed, cloud | Use it for semantic KGs combined with neuro-symbolic AI. | ⚠️ **Niche** | Proprietary | 🟡 **Free edition** is limited to 5 million triples; check its terms for commercial use. Otherwise paid. |
| **Dgraph** | Native graph (distributed) | DQL, GraphQL | Self-managed, cloud | Use it for a GraphQL-first app backend. | ⚠️ **Niche** | Apache 2.0 | 🟢 **Yes.** Open source, free for any use. Paid cloud and support are optional. |

**Legend:** 🟢 free for commercial use · 🟡 free tier or edition with limits · 🟠 free for non-commercial use only · 🔴 paid (trial only)

### License types in plain English

| License | Can I use it free in a commercial product? | Catch |
| --- | --- | --- |
| **Apache 2.0** | ✅ Yes | None. Keep the notices. The most permissive option. |
| **GPLv3** (Neo4j Community) | ✅ Yes, for running it as a database server | If you *modify and distribute* the database itself, you must share your changes. Just connecting your app to it over a driver doesn't trigger this. |
| **BSL 1.1** (Memgraph, ArangoDB source) | ✅ Yes, for internal and product use | You can't offer it as a competing hosted/managed database service. Each release converts to open source after a set period (usually 4 years). |
| **SSPL** (FalkorDB) | ✅ Yes, for internal use | If you offer it *as a service* to others, you must open-source your whole service stack. Not OSI-approved. |
| **Proprietary with free edition** | ⚠️ Depends on the edition's terms | Usually capped by size, CPU or features. Some free editions are **non-commercial only** (e.g. Stardog Free). |
| **Cloud managed services** | ❌ Pay-per-use | Free tiers and trials exist, but production costs money. |

> ⚠️ Licensing terms change often (ArangoDB, Neo4j and Dgraph have all changed theirs in recent years). Check the vendor's current license page, and your legal team for production use.

---

**Quick rule of thumb**

- Starting fresh with a general graph use case → **Neo4j**
- Already on AWS / Azure / GCP → **Neptune / Cosmos-or-Fabric / Spanner Graph**
- Data already in Oracle / SQL Server / Postgres → **SQL/PGQ, SQL Graph or Apache AGE** first, and move to a dedicated graph DB only if you outgrow them
- Ontologies, standards, cross-org data sharing → **RDF (GraphDB, Stardog, Neptune SPARQL)**
- Billions of edges and deep analytics → **TigerGraph / NebulaGraph**

---

## 3. Common Use Cases

| Use case | Why a graph fits |
| --- | --- |
| **Fraud detection / AML** | Finds rings of accounts, devices and addresses sharing identifiers, i.e. patterns across many hops. |
| **Recommendations** | "Customers who flew this route also bought lounge access" is a neighbourhood traversal. |
| **Customer 360 / Master Data (MDM)** | Resolves and links identities across systems. |
| **Knowledge graphs & GraphRAG** | Gives LLMs structured facts and relationships, which reduces hallucination and supports multi-hop answers. |
| **Network / IT / cloud topology** | Impact analysis: "if this switch fails, which services go down?" |
| **Identity & access management** | "Can user X reach resource Y through any group or role chain?" |
| **Supply chain** | Multi-tier supplier dependency and risk propagation. |
| **Data lineage / governance** | Tracks table → pipeline → report lineage. Microsoft Purview and most catalogs are graph-backed internally. |

---

## 4. Things You Should Know About Graph DBs

### 4.1 When **NOT** to use a graph database
- Your queries are mostly **aggregations, reporting or scans** (`SUM`, `GROUP BY` over millions of rows). Use a relational DB or warehouse.
- Relationships are shallow (1–2 JOINs) and stable. RDBMS handles this fine.
- It's simple key-value or document lookup by ID.
- **Test:** if you can't phrase your key questions as "find paths or patterns between things", you probably don't need a graph.

### 4.2 Query language standards are converging
- **GQL (ISO/IEC 39075:2024)** is the first new ISO database language since SQL. It's heavily based on Cypher. Neo4j, Google Spanner, Microsoft Fabric and others are adopting it.
- **SQL/PGQ (part of SQL:2023)** lets you run graph pattern matching (`GRAPH_TABLE ... MATCH`) inside plain SQL. Oracle 23ai supports it.
- **Gremlin** (Apache TinkerPop) is imperative and traversal-style. It's widely supported but losing mindshare to declarative GQL/Cypher.
- **SPARQL** remains the standard for RDF.
- 👉 Learning **Cypher/GQL** gives you the most transferable skill.

### 4.3 OLTP vs OLAP graph workloads
- **Graph OLTP:** many small, real-time traversals (e.g. "check this transaction for fraud in 50 ms"). Neo4j, Neptune, Memgraph.
- **Graph analytics/OLAP:** whole-graph algorithms such as PageRank, community detection, shortest path and centrality. Neo4j GDS, Neptune Analytics, TigerGraph, Spark GraphFrames.
- Many teams run both: a transactional graph for the app, plus periodic exports to an analytics engine.

### 4.4 Data modeling tips
- **Nodes = nouns, edges = verbs.** `(Customer)-[:BOOKED]->(Flight)`.
- If a relationship has many attributes of its own or connects more than 2 things, **promote it to a node** (e.g. `Booking`).
- Use **specific relationship types** (`:BOOKED`, `:CANCELLED`) instead of a generic `:RELATED_TO {type:'booked'}`. Traversals filter on type very efficiently.
- Model for your **queries**, not for normalization.
- Index the properties you use as **entry points** (e.g. `customerId`). Traversal from there needs no index.

### 4.5 Watch out for
- **Supernodes:** a node with millions of edges (e.g. "Country = USA", a popular airport). Traversing through it explodes the query. Mitigate by bucketing, adding intermediate nodes, or avoiding such hubs as traversal paths.
- **Sharding is hard:** graphs don't partition cleanly, and cross-partition hops are expensive. Many "distributed" graph DBs still perform best when the working set fits on one machine or in memory.
- **Unbounded traversals:** `MATCH (a)-[*]->(b)` without a depth limit can scan the entire graph. Always cap the depth (`[*1..4]`).
- **Skills and ops:** it's a new language, new tuning and new backup/DR model. Factor in team learning time.
- **Vendor lock-in:** proprietary languages (GSQL, AQL, nGQL) make migration harder. Prefer GQL/openCypher/SPARQL where possible.
- **Licensing:** check open-source vs enterprise editions. For example, clustering and RBAC in Neo4j require Enterprise.

### 4.6 Graph + AI (GraphRAG)
- **Knowledge graphs + vector search** is now a major enterprise driver for graph adoption.
- Plain vector RAG retrieves *similar text chunks*. GraphRAG also retrieves **connected facts**, which answers multi-hop questions such as "Which suppliers of our delayed parts also supply our competitor?"
- Most graph DBs now ship **vector indexes** (Neo4j, Neptune Analytics, FalkorDB, Memgraph, Spanner), so one store can serve both similarity and relationship queries.
- See [GraphRAG Databases](../graphrag/README.MD) for the same GraphRAG dataset running on Neo4j, FalkorDB, Memgraph, PostgreSQL + AGE and ArangoDB.
- Frameworks: LangChain/LlamaIndex graph integrations and Microsoft GraphRAG.

### 4.7 Performance intuition

| Query | Relational (JOINs) | Graph |
| --- | --- | --- |
| Direct lookup by key | ⚡ Fast | ⚡ Fast |
| 1–2 hops | ⚡ Fast | ⚡ Fast |
| 3–6 hops (friends-of-friends-of-friends) | 🐢 Grows sharply with each JOIN | ⚡ Roughly proportional to the part of the graph actually touched |
| Variable/unknown depth (shortest path, "any path") | 😖 Recursive CTEs, painful | ⚡ Native |
| Large aggregations / reporting | ⚡ Excellent | 🐢 Not its strength |

---

## 5. Example: Same Query in Different Languages

*"Find customers who booked a flight that a friend of Alice also booked."*

**Cypher / GQL (Neo4j, Memgraph, Neptune openCypher, Fabric, Spanner)**
```cypher
MATCH (alice:Customer {name: 'Alice'})-[:FRIEND_OF]->(f:Customer)-[:BOOKED]->(fl:Flight)<-[:BOOKED]-(c:Customer)
WHERE c <> alice
RETURN DISTINCT c.name;
```

**Gremlin (Cosmos DB, Neptune, JanusGraph)**
```groovy
g.V().has('Customer','name','Alice').as('alice')
 .out('FRIEND_OF').out('BOOKED').in('BOOKED')
 .where(neq('alice')).dedup().values('name')
```

**SQL/PGQ (Oracle 23ai)**
```sql
SELECT DISTINCT c_name
FROM GRAPH_TABLE ( travel_graph
  MATCH (a IS customer WHERE a.name = 'Alice') -[IS friend_of]-> (f IS customer)
        -[IS booked]-> (fl IS flight) <-[IS booked]- (c IS customer)
  WHERE c.id <> a.id
  COLUMNS (c.name AS c_name)
);
```

**SQL Server Graph**
```sql
SELECT DISTINCT c.name
FROM Customer a, FriendOf fo, Customer f, Booked b1, Flight fl, Booked b2, Customer c
WHERE MATCH(a-(fo)->f-(b1)->fl<-(b2)-c)
  AND a.name = 'Alice' AND c.name <> 'Alice';
```

**SPARQL (RDF stores)**
```sparql
SELECT DISTINCT ?name WHERE {
  ?alice :name "Alice" ; :friendOf ?f .
  ?f :booked ?flight .
  ?c :booked ?flight ; :name ?name .
  FILTER (?c != ?alice)
}
```

---

## 6. Summary

- **Property graph (LPG)** is the enterprise mainstream, and **Neo4j** is the most widely used product.
- **RDF** is the choice when semantics, ontologies and data sharing matter more than raw traversal speed.
- **Cloud vendors** (AWS Neptune, Azure Cosmos/Fabric, GCP Spanner Graph) are the easiest path if you're already on that cloud.
- **Relational DBs now speak graph** (SQL/PGQ, SQL Server `MATCH`, Apache AGE). Try this first before adding a new database.
- **GQL** is the new ISO standard, so bet on Cypher/GQL skills.
- The biggest current adoption driver is **GraphRAG / knowledge graphs for AI**.
