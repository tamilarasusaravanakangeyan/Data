# Getting Started with Neo4j Enterprise Edition (Free for Developers)

This guide covers two things from the [Neo4j overview](README.MD):

1. **How to get Neo4j Enterprise Edition legally for free**: the developer license (Neo4j Desktop), the evaluation license (Docker, packages, tarball) and the Startup Program.
2. **How to run Enterprise Edition in Docker** for local development, CI pipelines and a simple single-node deployment.

> ⚠️ License terms, eligibility rules and image tags change. Before you rely on any of this for real work, read the current agreement text at [neo4j.com/licensing](https://neo4j.com/licensing/) and the tags on [Docker Hub](https://hub.docker.com/_/neo4j).

---

## 1. Which License Do I Need?

| Scenario | Edition | License | Cost |
| --- | --- | --- | --- |
| Learning, prototyping on **my laptop** | Enterprise | **Developer license** (bundled with Neo4j Desktop) | 🟢 Free |
| Trying Enterprise features (clustering, RBAC, online backup) in **Docker / a VM** | Enterprise | **Evaluation license** (`NEO4J_ACCEPT_LICENSE_AGREEMENT=eval`) | 🟢 Free, time-limited, **not for production** |
| **CI pipelines / automated tests** with Enterprise features | Enterprise | Evaluation license (or your commercial license) | 🟢 Free for evaluation / 🔴 commercial for ongoing use in a company pipeline (check terms) |
| **Production** at a qualifying startup | Enterprise | **Startup Program** license | 🟢 Free (if eligible) |
| **Production** at any other company | Enterprise | **Commercial license** (`NEO4J_ACCEPT_LICENSE_AGREEMENT=yes`) | 🔴 Paid, contact Neo4j sales |
| Production, no Enterprise features needed | **Community** | GPLv3 | 🟢 Free, including commercial use |
| Production, don't want to run servers | **AuraDB** | SaaS subscription | 🟡 Free tier, then paid |

**Key point:** "free Enterprise" means **development and evaluation**. Running Enterprise Edition in production needs either a commercial license or a Startup Program license. If you need a free production database, use **Community Edition** or **AuraDB Free**.

### What Enterprise gives you over Community (why you might want it locally)

- Multiple databases on one server (`CREATE DATABASE bookings`)
- Clustering (primaries + secondaries), to test failover and read routing
- Role-based access control (RBAC) and fine-grained security, to test your app's permissions
- Online backup (`neo4j-admin database backup`)
- Parallel runtime, block storage format and other performance features
- Composite databases (querying across databases)

If your production target is **Enterprise or AuraDB**, develop against Enterprise locally so you don't discover missing features late.

---

## 2. How to Get the Enterprise License for Free

### Option 1: Developer license via Neo4j Desktop (easiest)

The developer license comes **with Neo4j Desktop**. There is no separate key to request.

1. Go to **[neo4j.com/download](https://neo4j.com/download/)**.
2. Fill in the short form (name, email, company) and download **Neo4j Desktop** for macOS, Windows or Linux.
3. Install and open it. If it asks you to sign in or enter an activation key, use the one shown on the download page or sent by email (older Desktop versions used an activation key; newer versions ask you to sign in).
4. Create a local instance. Instances in Desktop run **Enterprise Edition** under the developer license.
5. Check the edition in the query window:

   ```cypher
   CALL dbms.components() YIELD name, versions, edition
   RETURN name, versions, edition;
   // edition = "enterprise"
   ```

**What the developer license allows:** one developer using Enterprise Edition **on their own machine** for development, prototyping and testing.

**What it does not allow:** servers, shared team databases, production workloads or running it for other people.

### Option 2: Evaluation license (Docker, Linux packages, tarball)

For Enterprise Edition outside Desktop, you accept the **Neo4j Evaluation Agreement** when you start the server. There is no key file; you accept the agreement through an environment variable or a command.

| Install type | How to accept the evaluation license |
| --- | --- |
| **Docker** | `-e NEO4J_ACCEPT_LICENSE_AGREEMENT=eval` |
| **Tarball / ZIP** | `bin/neo4j-admin server license --accept-evaluation` |
| **Debian / RPM package** | The installer prompts you; or run `neo4j-admin server license --accept-evaluation` afterwards |
| **Kubernetes (Helm)** | `--set neo4j.edition=enterprise --set neo4j.acceptLicenseAgreement=eval` |

Values for `NEO4J_ACCEPT_LICENSE_AGREEMENT`:

| Value | Means | Use when |
| --- | --- | --- |
| `eval` | You accept the **Evaluation Agreement** (free, time-limited, non-production) | Local dev, POCs, trying features |
| `yes` | You declare that you hold a **commercial (or Startup Program) license** | Production under a signed agreement |
| *(not set)* | The Enterprise image refuses to start | — |

Notes:

- The evaluation period is **time-limited**. The server logs a warning as the end approaches; check the agreement for the exact period and what happens afterwards.
- Setting `yes` without holding a license is a license breach, not a technical workaround. Use `eval` for development.
- Older Neo4j versions (before the `eval` option existed) only accepted `yes`; with those, the free route was the Desktop developer license or a trial arranged with Neo4j.

### Option 3: Startup Program (free Enterprise for production)

1. Go to **[neo4j.com/startup-program](https://neo4j.com/startup-program/)**.
2. Check the eligibility rules. Historically these have been along the lines of a **small company (under ~50 employees)** with **limited annual revenue (a few million USD)**; confirm the current numbers on the page.
3. Fill in the application form (company details, use case).
4. After approval, you receive a license agreement that allows Enterprise Edition in production on a limited number of servers (plus dev/test instances), and usually access to community support.
5. Deploy with `NEO4J_ACCEPT_LICENSE_AGREEMENT=yes`, because you now hold a real license.

### Option 4: Enterprise features without installing anything

- **AuraDB Free**: managed Neo4j at [console.neo4j.io](https://console.neo4j.io). It runs Enterprise internally, although some admin features (multiple databases, custom RBAC) come only with paid tiers.
- **AuraDB paid-tier trial**: Aura sometimes offers trial credits for Professional/Business Critical tiers.
- **Neo4j Sandbox** ([sandbox.neo4j.com](https://sandbox.neo4j.com)): free, temporary instances with sample datasets.

### Graph Data Science (GDS) Enterprise is separate

The **GDS plugin** has its own license. GDS Community is free (limited to 4 CPU cores, no model catalog persistence, etc.). **GDS Enterprise** needs a separate license key file from Neo4j, even for evaluation. Ask Neo4j sales for a trial key, then:

```bash
-e NEO4J_gds_enterprise_license__file=/licenses/gds.license \
-v $HOME/neo4j/licenses:/licenses
```

---

## 3. Docker: Enterprise Edition

### 3.1 Image tags

| Tag | What |
| --- | --- |
| `neo4j:enterprise` | Latest Enterprise release (moving tag; avoid in real projects) |
| `neo4j:5.26-enterprise` | 5.26 LTS line, stable |
| `neo4j:2025.xx-enterprise` | A specific calendar release (e.g. `2025.08-enterprise`, or `2025.08.0-enterprise` for an exact patch) |
| `neo4j:latest` / `neo4j:5.26` (no suffix) | **Community** Edition |

👉 **Pin a tag** that matches the version you'll use in production (Aura or self-managed), so local tests behave the same.

### 3.2 Local development

#### Quick start (single command)

```bash
docker run -d --name neo4j-ee \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/ChangeMe123! \
  -e NEO4J_ACCEPT_LICENSE_AGREEMENT=eval \
  -e NEO4J_PLUGINS='["apoc"]' \
  -v $HOME/neo4j-ee/data:/data \
  -v $HOME/neo4j-ee/logs:/logs \
  -v $HOME/neo4j-ee/import:/import \
  neo4j:5.26-enterprise
```

- Browser: <http://localhost:7474>
- Bolt (for apps): `neo4j://localhost:7687`, user `neo4j`, password `ChangeMe123!`

**docker-compose.yml for development** (save next to your app):

```yaml
services:
  neo4j:
    image: neo4j:5.26-enterprise
    container_name: neo4j-ee
    ports:
      - "7474:7474"   # Browser / HTTP
      - "7687:7687"   # Bolt
    environment:
      NEO4J_AUTH: neo4j/ChangeMe123!
      NEO4J_ACCEPT_LICENSE_AGREEMENT: eval
      NEO4J_PLUGINS: '["apoc"]'
      # Config: "." → "_" and "_" → "__", with the NEO4J_ prefix
      NEO4J_server_memory_heap_initial__size: 1G
      NEO4J_server_memory_heap_max__size: 1G
      NEO4J_server_memory_pagecache_size: 1G
      NEO4J_dbms_security_procedures_unrestricted: apoc.*
      NEO4J_db_logs_query_enabled: INFO
      NEO4J_db_logs_query_threshold: 500ms
    volumes:
      - ./neo4j/data:/data
      - ./neo4j/logs:/logs
      - ./neo4j/import:/import       # files for LOAD CSV
      - ./neo4j/plugins:/plugins
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://localhost:7474 >/dev/null || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 12
```

```bash
docker compose up -d
docker compose logs -f neo4j                                   # wait for "Started."
docker exec -it neo4j-ee cypher-shell -u neo4j -p 'ChangeMe123!'
```

**Try Enterprise-only features** in `cypher-shell` or the Browser:

```cypher
// Confirm the edition
CALL dbms.components() YIELD edition RETURN edition;          // "enterprise"

// Multiple databases (Enterprise only)
CREATE DATABASE bookings IF NOT EXISTS WAIT;
SHOW DATABASES;
:use bookings

// RBAC (Enterprise only): a read-only app user
CREATE USER reporting SET PASSWORD 'Report123!' CHANGE NOT REQUIRED;
CREATE ROLE reader_bookings IF NOT EXISTS;
GRANT ACCESS ON DATABASE bookings TO reader_bookings;
GRANT MATCH {*} ON GRAPH bookings TO reader_bookings;
GRANT ROLE reader_bookings TO reporting;
SHOW USERS;
```

**Online backup (Enterprise only)** while the database is running:

```bash
docker exec neo4j-ee neo4j-admin database backup bookings --to-path=/data/backups
```

**Seed data automatically:** put `.cypher` files in a folder and run them once the container is healthy:

```bash
docker exec -i neo4j-ee cypher-shell -u neo4j -p 'ChangeMe123!' -d neo4j < ./seed/01-constraints.cypher
docker exec -i neo4j-ee cypher-shell -u neo4j -p 'ChangeMe123!' -d neo4j < ./seed/02-sample-data.cypher
```

**Reset everything:** `docker compose down && rm -rf ./neo4j/data`.

### 3.3 CI pipelines

Keep CI containers **throwaway**: no volumes, fixed image tag, evaluation license (or your commercial one).

#### GitHub Actions: service container

```yaml
# .github/workflows/integration-tests.yml
name: integration-tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      neo4j:
        image: neo4j:5.26-enterprise
        env:
          NEO4J_AUTH: neo4j/TestPassword123!
          NEO4J_ACCEPT_LICENSE_AGREEMENT: eval
          NEO4J_PLUGINS: '["apoc"]'
        ports:
          - 7474:7474
          - 7687:7687
        options: >-
          --health-cmd "wget -qO- http://localhost:7474 || exit 1"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 12
    env:
      NEO4J_URI: neo4j://localhost:7687
      NEO4J_USER: neo4j
      NEO4J_PASSWORD: TestPassword123!
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet test --configuration Release
```

#### Azure DevOps: run the container in a step

```yaml
steps:
  - script: |
      docker run -d --name neo4j -p 7687:7687 -p 7474:7474 \
        -e NEO4J_AUTH=neo4j/TestPassword123! \
        -e NEO4J_ACCEPT_LICENSE_AGREEMENT=eval \
        neo4j:5.26-enterprise
      for i in $(seq 1 30); do curl -sf http://localhost:7474 && break; sleep 2; done
    displayName: Start Neo4j Enterprise
  - script: dotnet test --configuration Release
    displayName: Integration tests
    env:
      NEO4J_URI: neo4j://localhost:7687
      NEO4J_PASSWORD: TestPassword123!
```

**Testcontainers for .NET** (each test run gets its own container; works the same on laptops and CI):

```bash
dotnet add package Testcontainers.Neo4j
dotnet add package Neo4j.Driver
```

```csharp
using Neo4j.Driver;
using Testcontainers.Neo4j;
using Xunit;

public sealed class Neo4jFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder()
        .WithImage("neo4j:5.26-enterprise")
        .WithEnvironment("NEO4J_ACCEPT_LICENSE_AGREEMENT", "eval")
        .Build();

    public IDriver Driver { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // The Testcontainers module disables auth by default (NEO4J_AUTH=none)
        Driver = GraphDatabase.Driver(_container.GetConnectionString(), AuthTokens.None);
        await Driver.VerifyConnectivityAsync();
    }

    public async Task DisposeAsync()
    {
        await Driver.DisposeAsync();
        await _container.DisposeAsync();
    }
}

public class BookingGraphTests : IClassFixture<Neo4jFixture>
{
    private readonly IDriver _driver;
    public BookingGraphTests(Neo4jFixture fixture) => _driver = fixture.Driver;

    [Fact]
    public async Task Enterprise_edition_is_running()
    {
        var (records, _, _) = await _driver
            .ExecutableQuery("CALL dbms.components() YIELD edition RETURN edition")
            .ExecuteAsync();
        Assert.Equal("enterprise", records[0]["edition"].As<string>());
    }
}
```

#### Python (pytest + Testcontainers)

```python
# pip install "testcontainers[neo4j]" neo4j pytest
from testcontainers.neo4j import Neo4jContainer

def test_enterprise_edition():
    with Neo4jContainer("neo4j:5.26-enterprise") \
            .with_env("NEO4J_ACCEPT_LICENSE_AGREEMENT", "eval") as neo4j:
        with neo4j.get_driver() as driver:
            records, _, _ = driver.execute_query(
                "CALL dbms.components() YIELD edition RETURN edition")
            assert records[0]["edition"] == "enterprise"
```

CI tips:

- Enterprise images are larger and start in roughly 10–30 s; **reuse one container per test class/suite**, not per test.
- Clean data between tests with `MATCH (n) DETACH DELETE n`, or (Enterprise) `CREATE OR REPLACE DATABASE test WAIT` for a guaranteed empty database.
- Keep test datasets small; bulk loads belong in separate performance tests.
- Store passwords as pipeline secrets even for throwaway containers, so the pattern is right when it matters.

### 3.4 Simple single-node deployment

> ⚠️ **This is production.** Running Enterprise Edition here needs a **commercial or Startup Program license** (`NEO4J_ACCEPT_LICENSE_AGREEMENT=yes`). Without one, use `neo4j:5.26` (**Community**) with the same file: drop the license variable and the Enterprise-only settings.

A single node has **no high availability**: if the host goes down, the database is down. Fine for internal tools, small apps and non-critical workloads; for HA, use a 3-primary cluster or AuraDB.

#### docker-compose.prod.yml

```yaml
services:
  neo4j:
    image: neo4j:5.26.12-enterprise        # pin the exact patch version
    container_name: neo4j
    restart: unless-stopped
    ports:
      - "127.0.0.1:7474:7474"              # Browser only on localhost; reach it via SSH tunnel/VPN
      - "7687:7687"                        # Bolt for applications (restrict with firewall/NSG)
    environment:
      NEO4J_AUTH_FILE: /run/secrets/neo4j_auth      # contents: neo4j/<strong-password>
      NEO4J_ACCEPT_LICENSE_AGREEMENT: "yes"         # only with a commercial / Startup license
      NEO4J_PLUGINS: '["apoc"]'
      # Memory for a host with 16 GB RAM dedicated to Neo4j
      NEO4J_server_memory_heap_initial__size: 5G
      NEO4J_server_memory_heap_max__size: 5G
      NEO4J_server_memory_pagecache_size: 7G
      NEO4J_db_memory_transaction_total_max: 3G
      # TLS for Bolt (certificates mounted under /ssl/bolt)
      NEO4J_dbms_ssl_policy_bolt_enabled: "true"
      NEO4J_dbms_ssl_policy_bolt_base__directory: /ssl/bolt
      NEO4J_server_bolt_tls__level: REQUIRED
      # Monitoring (Enterprise)
      NEO4J_server_metrics_prometheus_enabled: "true"
      NEO4J_server_metrics_prometheus_endpoint: 0.0.0.0:2004
      NEO4J_db_logs_query_threshold: 1s
    secrets:
      - neo4j_auth
    volumes:
      - /srv/neo4j/data:/data
      - /srv/neo4j/logs:/logs
      - /srv/neo4j/backups:/backups
      - /srv/neo4j/ssl:/ssl:ro
      - /srv/neo4j/import:/import
    ulimits:
      nofile:
        soft: 40000
        hard: 40000
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://localhost:7474 >/dev/null || exit 1"]
      interval: 30s
      retries: 5

secrets:
  neo4j_auth:
    file: ./secrets/neo4j_auth.txt
```

#### Nightly online backup (cron on the host)

```bash
0 2 * * * docker exec neo4j neo4j-admin database backup neo4j --to-path=/backups --keep-full=7 \
  && rsync -a /srv/neo4j/backups/ backup-host:/neo4j-backups/
```

Copy backups **off the host** (Azure Blob / S3 / another server); a backup on the same disk doesn't survive a disk failure.

**Upgrades:** take a backup → change the pinned tag → `docker compose pull && docker compose up -d` → check `SHOW DATABASES` and the logs. Read the release notes first: upgrades across major lines (e.g. 5.x → 2025.x) may need a migration step.

#### Single-node checklist

- [ ] Valid license for Enterprise in production (or switch to Community)
- [ ] Exact image version pinned
- [ ] Strong password via Docker secret, not in the compose file
- [ ] Browser (7474) not exposed publicly; Bolt restricted by firewall
- [ ] TLS on Bolt; apps connect with `neo4j+s://` or `bolt+s://`
- [ ] Memory set (heap + page cache ≤ ~75% of host RAM)
- [ ] Data on a persistent, fast disk (SSD / Azure Premium SSD)
- [ ] Nightly online backup, copied off the host, **restore tested**
- [ ] Metrics and logs collected (Prometheus/Grafana, Azure Monitor)
- [ ] App users with least-privilege roles (not the `neo4j` admin user)

---

## 4. Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Container exits with a message about accepting the license | `NEO4J_ACCEPT_LICENSE_AGREEMENT` not set on an Enterprise image | Add `eval` (dev/test) or `yes` (licensed production) |
| `Unsupported authentication token` / auth failure | Password changed in an existing `/data` volume; `NEO4J_AUTH` only applies on **first** start | Use the original password, or delete the data volume in dev |
| `NEO4J_AUTH` password rejected at start-up | Password shorter than 8 characters | Use a longer password |
| `edition = community` when you expected enterprise | Image tag has no `-enterprise` suffix | Use `neo4j:<version>-enterprise` |
| `CREATE DATABASE` fails with "unsupported" | Running Community Edition | Switch to the Enterprise image |
| Plugins not loading | Plugin version doesn't match the Neo4j version, or no internet in CI | Use `NEO4J_PLUGINS` (auto-matches versions) or mount the matching JARs into `/plugins` |
| `apoc.*` procedures blocked | Procedures not allowed | Set `NEO4J_dbms_security_procedures_unrestricted: apoc.*` |
| Container killed / OOM | Heap + page cache larger than container memory | Lower memory settings or give the container more RAM |
| App can't connect from another container | Using `localhost` inside Docker | Use the service name, e.g. `neo4j://neo4j:7687` |
| Permission errors on mounted volumes (Linux) | Host folder owned by root | Run with `--user $(id -u):$(id -g)` or `chown` the folders |

---

## 5. IDE and Client Tools

This section covers editors and database clients you can point at Neo4j to write Cypher, browse the graph and run queries — as opposed to the server-side install options in the [README](README.MD).

### 5.1 Comparison

| Tool | What it is | Protocol | Best for | Cost |
| --- | --- | --- | --- | --- |
| **Neo4j Browser / Neo4j Query** | Built-in web UI, ships with every server and Aura instance | Bolt (HTTP-proxied) | Zero-install quick queries, learning, `:play` guided tutorials | 🟢 Free |
| **Neo4j Desktop** | Local GUI app that manages instances + a built-in query tool | Bolt | Local development with multiple databases/versions side by side (see [README §3.2](README.MD)) | 🟢 Free |
| **VS Code** ("Neo4j for VS Code" extension) | Official Microsoft/Neo4j extension | Bolt | Daily Cypher development alongside your app code; `.cypher` files under source control | 🟢 Free |
| **JetBrains IDEs** (IntelliJ IDEA Ultimate, DataGrip, Rider, PyCharm Professional, WebStorm) | Built-in **Database Tools** plugin with native Neo4j/Bolt support | Bolt (driver auto-downloaded on first connect) | Teams already using JetBrains for SQL/other databases who want Neo4j in the same tool window | 🟡 Included in DataGrip and Ultimate-tier IDEs; **not** in the free Community Edition of IntelliJ/PyCharm |
| **DBeaver** | General-purpose database client | Bolt (via community driver) | A free, non-JetBrains option that also talks to your relational databases | 🟢 Community edition is free |
| **Cypher Shell** | CLI, bundled with every Neo4j install | Bolt | Scripting, CI, `cypher-shell -f script.cypher` (see [README §4.7](README.MD)) | 🟢 Free |
| **Neo4j Bloom / Explore** | Visual, search-driven graph exploration (not a code editor) | Bolt | Business users exploring the graph without writing Cypher (see [README §4.7](README.MD)) | 🟡 Aura: included on paid tiers. Self-managed: Enterprise only |

> ⚠️ JetBrains tier availability and DBeaver's exact plugin/bundling story change between releases — check each vendor's current plugin marketplace page before assuming Neo4j support is included.

### 5.2 Connect VS Code to a local Docker instance

1. Install **"Neo4j for VS Code"** from the VS Code Marketplace (search "Neo4j").
2. Open the Neo4j icon in the Activity Bar → **Add Connection**.
3. Fill in the connection using the credentials from the [Docker setup](#32-local-development) earlier in this guide:
   - **URI:** `neo4j://localhost:7687`
   - **Username:** `neo4j`
   - **Password:** `ChangeMe123!` (or whatever you set in `NEO4J_AUTH`)
4. Click **Connect**, then create a new `.cypher` file and run a query (▶ button, or `Cmd/Ctrl+Enter`). Results show as a table or graph view.

### 5.3 Connect JetBrains (DataGrip / IntelliJ Ultimate) to AuraDB

1. Open the **Database** tool window → **+ → Data Source → Neo4j**.
2. On first use, JetBrains prompts to download the Neo4j JDBC/Bolt driver files — accept.
3. Enter the connection details from your Aura credentials file (see [README §3.1](README.MD)):
   - **Host/URI:** `neo4j+s://xxxxxxxx.databases.neo4j.io`
   - **User:** `neo4j`
   - **Password:** the generated Aura password

   > ⚠️ Use **`neo4j+s://`**, not `neo4j://`. Aura requires TLS, and the `+s` suffix is what tells the driver to encrypt the connection and validate the certificate. Leaving it off is the most common connection failure against Aura.
4. Click **Test Connection**, then **OK**. The schema (labels, relationship types, property keys) appears in the tree; open a console to run Cypher.

### 5.4 Connect DBeaver

1. **Database → Driver Manager**, confirm a Neo4j driver is listed (bundled in recent DBeaver versions, or install the community Neo4j plugin if not).
2. **Database → New Database Connection → Neo4j**.
3. Enter the same Bolt URI/user/password pattern as above (`neo4j://` for local/Docker, `neo4j+s://` for Aura).
4. **Test Connection**, then **Finish**. Use the SQL editor to run Cypher against the connection.

### 5.5 Connection string cheat-sheet

| Scheme | Encrypted | Routing (cluster-aware) | Use for |
| --- | --- | --- | --- |
| `neo4j://` | ❌ No | ✅ Yes | Local Docker/dev, self-managed single instance without TLS configured |
| `neo4j+s://` | ✅ Yes, full certificate validation | ✅ Yes | **AuraDB** (required), self-managed with TLS and a trusted certificate |
| `neo4j+ssc://` | ✅ Yes, self-signed certificates accepted | ✅ Yes | Self-managed with TLS using a self-signed cert (dev/test only) |
| `bolt://` | ❌ No | ❌ No (direct to one server) | Single-instance servers, when you don't want driver-side routing |
| `bolt+s://` / `bolt+ssc://` | ✅ Yes | ❌ No | Same as above, encrypted |

### 5.6 IDE-specific troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| IDE rejects or can't parse `neo4j+s://` | Driver/plugin bundled with the IDE is too old to support the routing scheme | Update the Neo4j extension/plugin to the latest version |
| Certificate / SSL errors connecting to Aura | Used `neo4j://` instead of `neo4j+s://` | Switch to `neo4j+s://`; don't disable certificate validation to work around it |
| "Connection refused" in the IDE, but `cypher-shell` inside the container works | IDE is on the host, pointed at the Docker **service name** instead of `localhost` | Use `neo4j://localhost:7687` (or the host's mapped port) from tools running outside Docker |
| JetBrains: "Neo4j" not offered as a data source type | Community Edition of the IDE (Neo4j support needs DataGrip or an Ultimate-tier IDE) | Use DataGrip, VS Code, or DBeaver instead |

### 5.7 Which tool to pick

- **Quick one-off query, no install:** Neo4j Browser.
- **Daily Cypher development next to your app code:** VS Code + the Neo4j extension.
- **Already using JetBrains for SQL databases and want Neo4j alongside them:** DataGrip or an Ultimate-tier JetBrains IDE.
- **Want one free client for Neo4j and every other database:** DBeaver.
- **Scripting, CI, automation:** Cypher Shell.

---

## 6. Summary

- **Free Enterprise for one developer on a laptop:** download **Neo4j Desktop**; the developer license is included.
- **Free Enterprise in Docker for dev and evaluation:** set `NEO4J_ACCEPT_LICENSE_AGREEMENT=eval` on a `-enterprise` image.
- **Free Enterprise in production:** only through the **Startup Program** if you qualify; otherwise a commercial license (`=yes`).
- **Free production with no license questions:** **Community Edition** or **AuraDB Free**.
- **Docker Enterprise:** use `eval` for local dev and CI (with Testcontainers or service containers). A single-node production deployment needs a license, TLS, secrets, memory settings and off-host backups, and has no HA.


https://neo4j.com/docs/desktop/current/
https://graphacademy.neo4j.com/
https://neo4j.com/developer/