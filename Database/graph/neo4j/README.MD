# Neo4j: Installation Options, Hosting Models and Getting Started

Neo4j is the most widely used native property-graph database. It is queried with **Cypher** (and increasingly **GQL**, the ISO standard based on Cypher), and it has a built-in **vector index**, so one store can serve graph traversals, similarity search and GraphRAG. See [Vector Databases](../README.MD) and [Graph Databases](../../graph/README.MD) for background. For the free Enterprise developer/evaluation license and Docker Enterprise setup (dev, CI, single node), see [Getting Started](gettingstarted.md).

```text
Your app ──(Bolt protocol, port 7687 / neo4j+s://)──► Neo4j DBMS ──► databases (neo4j, system, ...)
Browser  ──(HTTP 7474 / HTTPS 7473)──────────────────► Neo4j Browser / Query UI
```

---

## 1. Editions

| | **Community Edition** | **Enterprise Edition** | **AuraDB (cloud)** |
| --- | --- | --- | --- |
| **What** | Open-source, single-instance server | Full self-managed server with clustering and security features | Fully managed Neo4j as a service (on AWS, Azure, GCP) |
| **License** | GPLv3 | Commercial | Subscription (SaaS) |
| **Cost** | 🟢 Free, including commercial use | 🔴 Paid for production. 🟡 Free for local development and evaluation (via Neo4j Desktop / developer license); a free **Startup Program** license exists for qualifying small companies | 🟡 **AuraDB Free** tier; paid tiers billed per GB of memory per hour |
| **Clustering / HA** | ❌ Single instance only | ✅ Autonomous clustering (primaries + secondaries) | ✅ Built in (on higher tiers) |
| **Multiple user databases** | ❌ One user database (+ `system`) | ✅ Many databases, composite databases (Fabric) | ✅ (tier-dependent) |
| **Online (hot) backup** | ❌ Offline dump only (`neo4j-admin database dump`) | ✅ Online full/incremental backup | ✅ Automatic snapshots |
| **Security** | Basic auth, TLS | + RBAC, fine-grained (property-level) access, LDAP/SSO (OIDC), auditing | + SSO, private networking, CMK (tier-dependent) |
| **Performance features** | Standard runtime | + Parallel runtime, advanced page cache and memory tuning, block storage format | Enterprise features managed for you |
| **Vector index** | ✅ | ✅ | ✅ |
| **Plugins** (APOC, Graph Data Science) | ✅ APOC Core; GDS Community (limited to 4 CPU cores) | ✅ + GDS Enterprise (paid) | ✅ APOC Core built in; GDS via **Aura Graph Analytics** / AuraDS |

> ⚠️ Pricing, tier names and limits change often. Check [neo4j.com/pricing](https://neo4j.com/pricing/) and the licensing page before choosing for production.

### Versions to know

- Since 2025 Neo4j uses **calendar versioning**: `2025.01`, `2025.02`, ... released roughly monthly. Each release is supported until the next one.
- **Neo4j 5.26** is the current **LTS** (long-term support) line of the older numbering, for teams that need a stable version.
- Neo4j 2025.x requires **Java 21** (Neo4j 5.x ran on Java 17 or 21). Docker images and Neo4j Desktop bundle Java for you.
- **Cypher 25** is the new Cypher version (GQL-aligned); older queries run as **Cypher 5** for compatibility.

---

## 2. Hosting Models

| Hosting model | Who runs it | Editions | Best for | Effort |
| --- | --- | --- | --- | --- |
| **Neo4j AuraDB (SaaS)** | Neo4j | Aura tiers | Most new projects: production without a DBA; fastest start | ⭐ Lowest |
| **Cloud marketplace (AWS / Azure / GCP)** | Aura via marketplace (billed on your cloud bill), or Enterprise VM/cluster templates you run | Aura or Enterprise | Using existing cloud commitments (e.g. Azure MACC) and procurement | ⭐⭐ |
| **Kubernetes (Helm charts)** | You | Community (single) or Enterprise (cluster) | Platform teams standardised on AKS/EKS/GKE/OpenShift | ⭐⭐⭐⭐ |
| **Self-managed VMs / bare metal** | You | Community or Enterprise | On-prem, data residency, full control of hardware | ⭐⭐⭐⭐ |
| **Docker container** | You | Community or Enterprise | Local development, CI, simple single-node deployments | ⭐⭐ |
| **Neo4j Desktop** | You (laptop) | Enterprise (dev license, free) | Local development and learning with a GUI | ⭐ |
| **Embedded (Java)** | Your JVM app | Community / Enterprise libraries | Java apps and tests needing an in-process graph (niche; client-server is recommended) | ⭐⭐⭐ |
| **Neo4j Sandbox / AuraDB Free** | Neo4j | Free | Trying Neo4j in minutes with sample datasets, no install | ⭐ Lowest |

### AuraDB tiers (overview)

| Tier | Typical use | Notes |
| --- | --- | --- |
| **AuraDB Free** | Learning, prototypes | Small, limited node/relationship counts; paused after inactivity and deleted if paused too long. No credit card. |
| **AuraDB Professional** | Small to medium production apps | Pay per GB memory/hour, choice of cloud and region, daily backups. |
| **AuraDB Business Critical** | Mission-critical production | Multi-zone clustering, 99.95% SLA, SSO, longer backup retention, private endpoints. |
| **AuraDB Virtual Dedicated Cloud** | Enterprises with strict isolation needs | Dedicated VPC/VNet, customer-managed keys, private networking, premium support. |
| **Aura Graph Analytics / AuraDS** | Graph Data Science (PageRank, communities, embeddings, similarity) | Serverless GDS sessions attached to AuraDB or other data sources, or a dedicated AuraDS instance. |

### Which one should I pick?

| Situation | Recommendation |
| --- | --- |
| Learning / first project | **AuraDB Free** or **Neo4j Desktop** |
| Local dev + CI tests | **Docker** (`neo4j:latest` or `neo4j:5.26`) + **Testcontainers** |
| Production, small team, no DBA | **AuraDB Professional** |
| Production, HA and SLA needed | **AuraDB Business Critical** or **Enterprise cluster** (3+ primaries) |
| Strict data residency / on-prem | **Enterprise** on VMs or Kubernetes |
| Budget = zero, production, single node acceptable | **Community Edition** (Docker or VM); accept no clustering and offline-only backups |
| Azure shop | **AuraDB via Azure Marketplace**, or Enterprise on **AKS** with the Helm chart |

---

## 3. Installation Options (Step by Step)

### Ports and defaults (all options)

| Port | Protocol | Purpose |
| --- | --- | --- |
| **7474** | HTTP | Neo4j Browser and HTTP API |
| **7473** | HTTPS | Browser/HTTP API over TLS (when enabled) |
| **7687** | Bolt | Driver connections (apps, Cypher Shell) |
| 6000, 7000, 7688 | Cluster | Enterprise clustering (discovery, Raft, routing) |
| 6362 | Backup | Enterprise online backup |

Default user is **`neo4j`**, default password `neo4j`, which you must change on first login (or set with `NEO4J_AUTH` / `neo4j-admin`).

---

### 3.1 Option A: AuraDB Free (no install, ~2 minutes)

1. Go to **[console.neo4j.io](https://console.neo4j.io)** and sign up (Google/GitHub/email).
2. Click **Create instance → AuraDB Free**.
3. **Download the credentials file** shown once (URI like `neo4j+s://xxxxxxxx.databases.neo4j.io`, user `neo4j`, generated password). It is not shown again.
4. Wait for the instance status to become **Running**, then click **Query** to open the query UI.
5. Connect your app with the `neo4j+s://` URI (TLS is always on in Aura).

> Aura Free instances pause after a period of inactivity; resume them from the console.

---

### 3.2 Option B: Neo4j Desktop (local GUI)

1. Download **Neo4j Desktop** from [neo4j.com/download](https://neo4j.com/download/) (macOS, Windows, Linux AppImage).
2. Install and open it. It bundles Java and includes an Enterprise developer license for local use.
3. Create a **local instance** (choose the version and set a password), then **Start** it.
4. Open the built-in **Query / Browser** tool and start writing Cypher.
5. Install plugins (APOC, Graph Data Science) from the instance's **Plugins** panel with one click.

Good for: learning, visual exploration, multiple local databases/versions side by side. Not for servers.

---

### 3.3 Option C: Docker (recommended for developers)

**Community Edition, single command:**

```bash
docker run -d --name neo4j \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/ChangeMe123! \
  -e NEO4J_PLUGINS='["apoc"]' \
  -v $HOME/neo4j/data:/data \
  -v $HOME/neo4j/logs:/logs \
  -v $HOME/neo4j/import:/import \
  neo4j:latest
```

Open **<http://localhost:7474>**, connect to `neo4j://localhost:7687` with `neo4j` / `ChangeMe123!`.

**Enterprise Edition (dev/evaluation):**

```bash
docker run -d --name neo4j-ee \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/ChangeMe123! \
  -e NEO4J_ACCEPT_LICENSE_AGREEMENT=yes \
  -e NEO4J_PLUGINS='["apoc", "graph-data-science"]' \
  -v $HOME/neo4j-ee/data:/data \
  neo4j:enterprise
```

**docker-compose.yml** (with memory settings):

```yaml
services:
  neo4j:
    image: neo4j:latest            # pin a version in real projects, e.g. neo4j:2025.08 or neo4j:5.26
    container_name: neo4j
    ports:
      - "7474:7474"
      - "7687:7687"
    environment:
      NEO4J_AUTH: neo4j/ChangeMe123!
      NEO4J_PLUGINS: '["apoc"]'
      # Config keys: replace "." with "_" and "_" with "__", prefix NEO4J_
      NEO4J_server_memory_heap_initial__size: 1G
      NEO4J_server_memory_heap_max__size: 1G
      NEO4J_server_memory_pagecache_size: 1G
      NEO4J_dbms_security_procedures_unrestricted: apoc.*
    volumes:
      - ./neo4j/data:/data
      - ./neo4j/logs:/logs
      - ./neo4j/import:/import
      - ./neo4j/plugins:/plugins
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://localhost:7474 || exit 1"]
      interval: 10s
      retries: 10
```

```bash
docker compose up -d
docker exec -it neo4j cypher-shell -u neo4j -p 'ChangeMe123!'   # CLI access
```

Tips:

- Always mount **`/data`**, or the database disappears with the container.
- Pin an image tag (`neo4j:5.26`, `neo4j:2025.x`) so upgrades are deliberate.
- On Apple Silicon, the official images are multi-arch (arm64) and run natively.

---

### 3.4 Option D: macOS (Homebrew)

```bash
brew install neo4j            # Community Edition, pulls in the right OpenJDK
neo4j-admin dbms set-initial-password ChangeMe123!
neo4j console                 # run in foreground (Ctrl+C to stop)
# or
brew services start neo4j     # run as a background service
```

Config file: `$(brew --prefix)/Cellar/neo4j/<version>/libexec/conf/neo4j.conf` (the path is printed by `brew info neo4j`).

---

### 3.5 Option E: Linux packages (Debian/Ubuntu and RHEL)

**Debian / Ubuntu (apt):**

```bash
sudo apt-get update && sudo apt-get install -y wget gnupg
wget -O - https://debian.neo4j.com/neotechnology.gpg.key | sudo gpg --dearmor -o /etc/apt/keyrings/neotechnology.gpg
echo 'deb [signed-by=/etc/apt/keyrings/neotechnology.gpg] https://debian.neo4j.com stable latest' \
  | sudo tee /etc/apt/sources.list.d/neo4j.list
sudo apt-get update
sudo apt-get install -y neo4j                 # Community
# sudo apt-get install -y neo4j-enterprise    # Enterprise (asks you to accept the license)

sudo neo4j-admin dbms set-initial-password ChangeMe123!
sudo systemctl enable --now neo4j
systemctl status neo4j
```

**RHEL / Rocky / Amazon Linux (yum/dnf):**

```bash
sudo rpm --import https://debian.neo4j.com/neotechnology.gpg.key
sudo tee /etc/yum.repos.d/neo4j.repo <<'EOF'
[neo4j]
name=Neo4j RPM Repository
baseurl=https://yum.neo4j.com/stable/latest
enabled=1
gpgcheck=1
EOF
sudo dnf install -y neo4j                      # or neo4j-enterprise
sudo systemctl enable --now neo4j
```

Key paths for package installs:

| What | Path |
| --- | --- |
| Config | `/etc/neo4j/neo4j.conf` |
| Data | `/var/lib/neo4j/data` |
| Logs | `/var/log/neo4j` |
| Import dir (for `LOAD CSV`) | `/var/lib/neo4j/import` |
| Plugins | `/var/lib/neo4j/plugins` |

To allow remote connections, set in `neo4j.conf`:

```properties
server.default_listen_address=0.0.0.0
```

---

### 3.6 Option F: Tarball / ZIP (any OS, no root)

```bash
# Requires Java 21 (e.g. Eclipse Temurin / Microsoft Build of OpenJDK)
java -version
tar -xzf neo4j-community-<version>-unix.tar.gz
cd neo4j-community-<version>
bin/neo4j-admin dbms set-initial-password ChangeMe123!
bin/neo4j console        # foreground
# bin/neo4j start        # background; bin/neo4j stop / status
```

**Windows (ZIP):** extract, then from PowerShell:

```powershell
bin\neo4j-admin dbms set-initial-password ChangeMe123!
bin\neo4j console                      # foreground
bin\neo4j windows-service install      # install as a Windows service
bin\neo4j start
```

---

### 3.7 Option G: Kubernetes (Helm)

```bash
helm repo add neo4j https://helm.neo4j.com/neo4j
helm repo update

# Single standalone instance
helm install my-neo4j neo4j/neo4j \
  --namespace neo4j --create-namespace \
  --set neo4j.name=my-neo4j \
  --set neo4j.password='ChangeMe123!' \
  --set volumes.data.mode=defaultStorageClass \
  --set neo4j.resources.cpu=2 --set neo4j.resources.memory=4Gi

kubectl -n neo4j port-forward svc/my-neo4j 7474:7474 7687:7687
```

**Enterprise cluster** (minimum 3 primaries for fault tolerance): install the same chart three times with `neo4j.edition=enterprise`, `neo4j.acceptLicenseAgreement=yes`, the same `neo4j.name` (cluster name) and `neo4j.minimumClusterSize=3`, then use the `neo4j/neo4j-load-balancer` chart (or the headless service) for client access. On AKS, use a Premium SSD storage class for `/data`.

---

### 3.8 Option H: Cloud marketplaces

| Cloud | What you get |
| --- | --- |
| **Azure Marketplace** | AuraDB (billed through Azure, counts toward MACC) and Neo4j Enterprise VM/cluster offers |
| **AWS Marketplace** | AuraDB, plus Enterprise AMIs / CloudFormation templates |
| **GCP Marketplace** | AuraDB, plus Enterprise deployment templates |

Marketplace Aura is the same service as buying direct; the difference is billing and procurement.

---

## 4. How to Start as a Developer

### Step 1: Get a database running (pick one)

- Fastest: **AuraDB Free** (3.1).
- Local: **Docker** (3.3), which also works for CI.

### Step 2: Learn Cypher basics in the Browser / Query UI

Create a small airline graph:

```cypher
// Create nodes and relationships
CREATE (alice:Customer {id: 'C1', name: 'Alice', tier: 'Gold'})
CREATE (bob:Customer   {id: 'C2', name: 'Bob',   tier: 'Silver'})
CREATE (ek501:Flight   {flightNo: 'EK501', origin: 'DXB', destination: 'BOM', date: date('2026-10-01')})
CREATE (lounge:Ancillary {code: 'LNG', type: 'Lounge access'})
CREATE (alice)-[:FRIEND_OF]->(bob)
CREATE (alice)-[:BOOKED {pnr: 'ABC123'}]->(ek501)
CREATE (bob)-[:BOOKED {pnr: 'XYZ789'}]->(ek501)
CREATE (alice)-[:PURCHASED]->(lounge);

// Read: who is on EK501?
MATCH (c:Customer)-[:BOOKED]->(f:Flight {flightNo: 'EK501'})
RETURN c.name, c.tier;

// Upsert pattern (idempotent loads)
MERGE (c:Customer {id: 'C3'})
  ON CREATE SET c.name = 'Carol', c.tier = 'Blue'
  ON MATCH  SET c.lastSeen = datetime();

// Recommendation: ancillaries bought by friends
MATCH (me:Customer {name: 'Bob'})<-[:FRIEND_OF]-(friend)-[:PURCHASED]->(a:Ancillary)
WHERE NOT (me)-[:PURCHASED]->(a)
RETURN a.type, count(*) AS friendsWhoBought
ORDER BY friendsWhoBought DESC;
```

Useful Browser commands: `:help`, `:play movies` (built-in guided tutorial), `:schema`, `:sysinfo`.

### Step 3: Add constraints and indexes (do this before loading data)

```cypher
CREATE CONSTRAINT customer_id IF NOT EXISTS FOR (c:Customer) REQUIRE c.id IS UNIQUE;
CREATE CONSTRAINT flight_key  IF NOT EXISTS FOR (f:Flight)   REQUIRE (f.flightNo, f.date) IS UNIQUE;
CREATE INDEX customer_tier    IF NOT EXISTS FOR (c:Customer) ON (c.tier);
SHOW INDEXES;
```

Unique constraints create an index automatically, and make `MERGE` fast and safe.

### Step 4: Load real data

> For the full guide — bulk import, APOC/JSON/JDBC, Kafka/Spark connectors, batching and performance tips — see [importdata.md](importdata.md).

| Method | Use for |
| --- | --- |
| `LOAD CSV` | Small/medium CSVs (up to a few million rows) into a running database |
| **Data Importer** (in Aura / Desktop UI) | Visual mapping of CSV files to nodes and relationships, no code |
| `neo4j-admin database import` | Initial bulk load of very large datasets (billions of rows) into an **empty, offline** database; very fast |
| APOC (`apoc.load.json`, `apoc.load.jdbc`) | JSON, APIs, relational sources |
| **Neo4j Connectors** (Kafka, Spark, BI/JDBC) | Streaming and big-data pipelines |

```cypher
// Put bookings.csv in the import folder (Docker: ./neo4j/import)
LOAD CSV WITH HEADERS FROM 'file:///bookings.csv' AS row
CALL (row) {
  MERGE (c:Customer {id: row.customerId}) ON CREATE SET c.name = row.name
  MERGE (f:Flight {flightNo: row.flightNo, date: date(row.flightDate)})
  MERGE (c)-[:BOOKED {pnr: row.pnr}]->(f)
} IN TRANSACTIONS OF 10000 ROWS;
```

### Step 5: Connect from code

Official drivers: **.NET, Java, Python, JavaScript/TypeScript, Go** (community drivers for Rust, PHP, Ruby and others). Use the **`neo4j://`** scheme (routing, works for single instances and clusters) or **`neo4j+s://`** for TLS (Aura).

**C# / .NET** (`dotnet add package Neo4j.Driver`):

```csharp
using Neo4j.Driver;

await using var driver = GraphDatabase.Driver(
    "neo4j://localhost:7687",                 // Aura: "neo4j+s://xxxx.databases.neo4j.io"
    AuthTokens.Basic("neo4j", "ChangeMe123!"));

await driver.VerifyConnectivityAsync();

var (records, summary, _) = await driver
    .ExecutableQuery(@"MATCH (c:Customer)-[:BOOKED]->(f:Flight {flightNo: $flightNo})
                       RETURN c.name AS name, c.tier AS tier")
    .WithParameters(new { flightNo = "EK501" })
    .WithConfig(new QueryConfig(database: "neo4j"))
    .ExecuteAsync();

foreach (var r in records)
    Console.WriteLine($"{r["name"]} ({r["tier"]})");
```

Register one `IDriver` as a **singleton** in ASP.NET Core DI (it is thread-safe and holds the connection pool); create sessions per request.

**Python** (`pip install neo4j`):

```python
from neo4j import GraphDatabase

URI = "neo4j://localhost:7687"          # Aura: "neo4j+s://xxxx.databases.neo4j.io"
AUTH = ("neo4j", "ChangeMe123!")

with GraphDatabase.driver(URI, auth=AUTH) as driver:
    driver.verify_connectivity()
    records, summary, keys = driver.execute_query(
        "MATCH (c:Customer)-[:BOOKED]->(f:Flight {flightNo: $flightNo}) "
        "RETURN c.name AS name, c.tier AS tier",
        flightNo="EK501",
        database_="neo4j",
    )
    for r in records:
        print(r["name"], r["tier"])
```

Driver rules of thumb:

- **Always use parameters** (`$flightNo`), never string concatenation: it prevents Cypher injection and lets Neo4j cache query plans.
- Create **one driver per application**, not per request.
- Use **read transactions** for reads (`executeRead` / `RoutingControl.Read`) so clusters can route them to secondaries.

### Step 6: Add vector search (for RAG / GraphRAG)

```cypher
// Vector index on chunk embeddings (1536 dims, cosine)
CREATE VECTOR INDEX policy_chunk_embedding IF NOT EXISTS
FOR (c:Chunk) ON c.embedding
OPTIONS { indexConfig: { `vector.dimensions`: 1536, `vector.similarity_function`: 'cosine' } };

// Store an embedding (computed by your app / embedding API)
MATCH (c:Chunk {id: $id})
CALL db.create.setNodeVectorProperty(c, 'embedding', $embedding);

// Similarity search + graph expansion
CALL db.index.vector.queryNodes('policy_chunk_embedding', 5, $questionEmbedding)
YIELD node AS chunk, score
MATCH (chunk)<-[:HAS_CHUNK]-(doc:Document)-[:APPLIES_TO]->(a:Ancillary)
RETURN doc.title, a.type, chunk.text, score
ORDER BY score DESC;
```

Python helpers: `pip install neo4j-graphrag` (retrievers such as `VectorRetriever`, `VectorCypherRetriever`, and a KG builder from documents). LangChain and LlamaIndex also have Neo4j integrations.

### Step 7: Developer tools

| Tool | Use |
| --- | --- |
| **Neo4j Browser / Query** | Run Cypher, see results as graph or table |
| **Explore (Bloom)** | Visual, search-based graph exploration for business users |
| **Cypher Shell** | CLI for scripts and automation (`cypher-shell -f script.cypher`) |
| **VS Code: Neo4j for VS Code** extension | Cypher syntax highlighting, autocomplete, run queries from the editor. Setup steps and other IDE options (JetBrains, DBeaver): [gettingstarted.md § IDE and Client Tools](gettingstarted.md#5-ide-and-client-tools) |
| **APOC** | ~450 utility procedures (import/export, refactoring, periodic batching, date/text helpers) |
| **Graph Data Science (GDS)** | Algorithms: PageRank, Louvain/Leiden communities, shortest path, node similarity, node embeddings |
| **Testcontainers** (`Testcontainers.Neo4j` for .NET, `testcontainers[neo4j]` for Python) | Throwaway Neo4j containers in integration tests |
| **Neo4j GraphQL Library** | Generate a GraphQL API from type definitions (JS/TS) |
| **Spring Data Neo4j / Neo4j OGM** | Object mapping for Java/Spring |

### Step 8: Learn

- **[GraphAcademy](https://graphacademy.neo4j.com)**: free self-paced courses with certificates. Suggested order: *Neo4j Fundamentals* → *Cypher Fundamentals* → *Graph Data Modeling Fundamentals* → *Importing Data* → driver course for your language (*.NET*, *Python*) → *Neo4j & GenerativeAI / GraphRAG*.
- **Neo4j Certified Professional**: free exam.
- `:play movies` and the sample datasets in Aura / Sandbox.
- Cypher manual and cheat sheet: [neo4j.com/docs/cypher-cheat-sheet](https://neo4j.com/docs/cypher-cheat-sheet/).

---

## 5. Moving to Production (Self-managed)

### 5.1 Memory

Neo4j memory = **heap** (query execution, transactions) + **page cache** (caches the graph store on disk) + OS headroom.

```bash
neo4j-admin server memory-recommendation --memory=16g
```

```properties
# neo4j.conf, example for a 16 GB dedicated server
server.memory.heap.initial_size=5g
server.memory.heap.max_size=5g          # set initial = max to avoid resizing pauses
server.memory.pagecache.size=7g         # ideally ≥ size of the store files (data + indexes)
db.memory.transaction.total.max=3g      # stop runaway queries from exhausting heap
```

Rule of thumb: size the **page cache to fit the store** (graph + native indexes). If it fits in RAM, traversals avoid disk.

### 5.2 Backup and restore

| Edition | Command | Notes |
| --- | --- | --- |
| Community | `neo4j-admin database dump neo4j --to-path=/backups` | Database must be **stopped** |
| Community | `neo4j-admin database load neo4j --from-path=/backups --overwrite-destination` | Restore a dump |
| Enterprise | `neo4j-admin database backup neo4j --to-path=/backups --from=host:6362` | **Online**, full + differential |
| Enterprise | `neo4j-admin database restore --from-path=/backups/neo4j-<timestamp>.backup neo4j` | Then `CREATE DATABASE` / `START DATABASE` |
| Aura | Automatic snapshots + on-demand snapshots in the console | Retention depends on tier |

### 5.3 Security checklist

- Change the default password; create **separate app users** with least privilege (RBAC is Enterprise/Aura).
- Enable **TLS** for Bolt and HTTPS (`server.bolt.tls_level=REQUIRED`, SSL policies in `neo4j.conf`).
- Don't expose 7474/7687 to the internet; use private networking, a VPN or private endpoints.
- Restrict procedures: allow only what you need in `dbms.security.procedures.unrestricted` / `allowlist`.
- Keep `/import` locked down (`server.directories.import`); `LOAD CSV` can read files from it.
- Enable query logging and (Enterprise) security/audit logging.

### 5.4 High availability (Enterprise)

- **Primaries** accept writes (Raft consensus); run **3** to tolerate 1 failure (5 to tolerate 2).
- **Secondaries** are asynchronous read replicas that scale reads.
- Drivers using `neo4j://` automatically route writes to the leader and reads to followers/secondaries.
- Community Edition has **no clustering**: for HA, use Enterprise or Aura.

### 5.5 Monitoring

- Metrics: Prometheus endpoint (Enterprise: `server.metrics.prometheus.enabled=true`), JMX, CSV.
- Logs: `neo4j.log`, `debug.log`, `query.log` (slow query threshold `db.logs.query.threshold=1s`).
- In Cypher: `SHOW TRANSACTIONS`, `SHOW SERVERS`, `SHOW DATABASES`, `PROFILE <query>` to see the plan and db hits.
- Aura: built-in metrics dashboards and log export.

---

## 6. Summary

- **Just learning?** Use **AuraDB Free** or **Neo4j Desktop**, then take the free GraphAcademy courses.
- **Building an app?** Develop against **Docker** locally (with Testcontainers in CI), and use the official driver (`Neo4j.Driver` for .NET, `neo4j` for Python) with parameters and a singleton driver.
- **Production?** **AuraDB** is the lowest-effort path (via Azure Marketplace if you're an Azure shop). Self-manage **Enterprise** on VMs/Kubernetes when you need on-prem, data residency or full control.
- **Community Edition** is free for commercial use but single-instance, with offline-only backups and no RBAC. Fine for small internal workloads; plan for Enterprise/Aura if you need HA.
- Neo4j's **vector index + graph traversal** makes it a strong single store for **GraphRAG**.
