# Use Case: Connecting Workforce — Local Trades / Gig-Work Marketplace

A knowledge-base entry describing the **business use case** behind the sample data and database work in this folder: what the platform is, who it serves, how the data is modeled, and — the question that came up while building it — whether a graph database is actually the right primary store for it.

---

## 1. What This Platform Is

**Mission:** bridge the gap between skills and demand for the modern workforce — linking talented side-hustlers directly to immediate project opportunities, and seamlessly aligning skilled job seekers with businesses seeking specialized, short-term expertise.

```text
Skilled Worker / Job Seeker  <──────►  Connecting Workforce  <──────►  Hirer
short-term / freelance / side hustle        (the platform)        employers, clients, hiring managers
```

### It's a trades/services gig platform, not a typical job board

This was clarified partway through building it, and it changes the data model and the database decision, so it's worth stating plainly:

- **Job seekers** here are **skilled tradespeople** doing short-term work — a plumber, electrician, AC technician, carpenter, painter, handyman — not software/knowledge-work freelancers.
- **Hirers** need a specific piece of work **done**, not a role filled. A homeowner with a leaking faucet and a property manager who needs a unit repainted are both "hirers" here.
- **Jobs range from an hour to months**: a 1-hour emergency lockout, a same-day AC repair, or a 3-week deck build/renovation, all in the same system.
- **The primary goal is task completion**, not a hiring funnel — closer to Thumbtack/TaskRabbit/Angi than to a job board. It "takes most of the functionality" of a job search site (post work, apply, get hired, rate) but repurposes it for gig/trade work rather than employment.

---

## 2. Personas

| Persona | Who | What they need |
| --- | --- | --- |
| **Skilled Worker (Job Seeker)** | Plumber, electrician, HVAC tech, carpenter, painter, appliance repair tech, locksmith, mason, pest control tech, etc. | Find nearby jobs matching their trade, quote/apply, get hired, get paid, build a rating |
| **Residential Hirer** | A homeowner | Get a specific repair or project done reliably, often urgently |
| **Commercial / Property Management Hirer** | A café, an apartment complex, an HOA | Recurring or larger-scale work; often values a proven, repeat relationship with a worker |

See the modeled sample of both in [data/job_seekers.csv](data/job_seekers.csv) and [data/hirers.csv](data/hirers.csv).

---

## 3. What Makes This Domain Different (and Why It Matters for the Model)

| Property | Why it exists | Generic job board equivalent |
| --- | --- | --- |
| **`urgency`** (Emergency / Scheduled / Flexible) | A same-day AC repair on a 100°F day needs a completely different matching path (who's available *now*) than a scheduled renovation. | Doesn't really exist — job postings aren't time-critical in that sense. |
| **`jobType`** (OneTime / LongTerm / Recurring) | A single repair, a multi-week project, and a recurring contract (e.g. quarterly pest control) need different matching, pricing and scheduling logic, even though they share the same graph shape. | Roughly maps to contract vs. full-time, but without the "get it done today" pressure. |
| **`licensed`** | Plumbing and electrical work often legally requires a license; filtering to licensed-only workers is a safety-relevant, not just a preference, query. | No equivalent. |
| **Location as a first-class concern** | Work happens at the hirer's address — it is fundamentally **local**, not remote. Matching without geo-proximity doesn't work in this domain. | Remote work is normal on a knowledge-work board; irrelevant here. |
| **`HIRED_FOR.laborCost` vs. `Job.budget`** | The quoted budget and the actual final labor cost often differ (scope changes are common in trade work). | A software gig's budget and final payment are usually closer together. |

Full property-by-property detail is in [data/README.md § 1](data/README.md).

---

## 4. Data Model (Summary)

```text
(:JobSeeker)-[:HAS_SKILL {proficiency, yearsUsed}]->(:Skill)<-[:REQUIRES_SKILL {importance}]-(:Job)<-[:POSTED]-(:Hirer)
      |                                                                        ^
      |-[:APPLIED_TO {appliedDate, proposedRate, status}]--------------------->|
      |-[:HIRED_FOR {scheduledDate, completedDate, laborCost}]---------------->|
      ^
(:Hirer)-[:RATED {rating, comment, ratedDate}]-->(:JobSeeker)
```

`Skill` here means **trade** (Plumbing Repair, Electrical Wiring, AC Repair, Carpentry, ...) and is the hub connecting worker supply to job demand.

The full node/relationship property tables, the working sample dataset (15 workers, 8 hirers, 20 jobs, 18 trades), the tested import script, and a set of verified example queries all live in **[data/README.md](data/README.md)** — this document doesn't repeat them, it explains the *why* behind the model and the surrounding architecture decision.

---

## 5. Is a Graph Database the Right Fit? (Architecture Decision)

This question came up directly while building the model, and it's worth recording the answer here rather than only in chat.

### Verdict: not as the primary store — the core queries are shallow joins

Using the same test from [Database/graph/README.MD § 4.1](../../graph/README.MD): *"if you can't phrase your key questions as finding paths or patterns between things, you probably don't need a graph."*

Every query this platform actually needs day-to-day is **1–2 hops**:

| Core query | Hops | Relational equivalent |
| --- | --- | --- |
| Workers matching a job's required trades | 2 (JobSeeker→Skill←Job) | `JOIN` through a `worker_skills` / `job_skills` table |
| Recommend open jobs to a worker | 2 | Same join, filtered |
| A hirer's job history + ratings | 2 | `JOIN jobs, hires, ratings` |
| Licensed workers available for an emergency job | 2 + a filter | `JOIN` + `WHERE licensed = true` |
| Average labor cost by trade | 1 (aggregation) | `GROUP BY` — relational's strength, not graph's |

None of these need variable-length traversal, "friend of a friend," or "any path between X and Y" — what a graph engine is actually built for.

### What this domain needs *more* than deep traversal

- **Geo-proximity** ("workers within 15 miles of this job") — a geospatial index problem (PostGIS), not a graph problem.
- **Transactional integrity** for bookings, scheduling and payments — relational databases are stronger here.
- **Aggregation/reporting** (revenue by trade, demand by category) — exactly what the graph doc flags as relational's strength.

### Where a graph genuinely earns its keep — the features that would justify one

| Feature | Why it's a real graph problem |
| --- | --- |
| **Trust / referral network** — "workers vouched for by hirers who other trusted hirers also used" | Multi-hop, weight-and-path-dependent |
| **Fraud / collusion detection** — fake reviews, sockpuppet accounts, shared devices/payment methods across many accounts | Graph databases' classic strength |
| **Crew / subcontracting chains** — a lead contractor who subcontracts to others who subcontract further | "Who's ultimately doing this job" is a path query |
| **Hybrid GraphRAG matching** — a hirer types "my sink is leaking and making a weird noise" and gets matched via semantic similarity *and* the trade/skill graph in one query | Genuinely needs both vector search and graph traversal together (see [../README.MD § 8](../README.MD)) |

### Recommendation

Build the **core marketplace on a relational database** — Postgres, with **PostGIS** for location/geo-matching and, if the semantic-search feature is built, **pgvector** for it. Keep a graph engine (Neo4j, or a graph layer like Apache AGE on top of the same Postgres) **specifically for the trust-network or fraud-detection feature**, if and when it's built — that's the piece that actually justifies a graph engine, not the everyday "find me a plumber" matching.

This mirrors the "quick rule of thumb" already in the graph doc: data that starts relational should try SQL-based graph features first, and move to a dedicated graph engine only once you outgrow it.

---

## 6. Key Business Questions This Platform Must Answer

These are the questions the data model and (eventually) the production database need to serve. Each is demonstrated with a tested Cypher query in [data/README.md § 4](data/README.md#4-example-queries); in a relational build, each becomes a SQL query with a `JOIN` through `worker_skills`/`job_skills`.

- Which workers can do this job, and how well do they match (by trade, license, rating)?
- Which open jobs should be recommended to a given worker?
- Is a licensed worker available right now for an emergency job?
- What's a hirer's history — who did they hire, and how did it go?
- What's the average labor cost per trade, and which trades are most in demand?
- (Future) Which workers can be trusted based on the hirer network that's used them before?
- (Future) Given a free-text description of a problem, which trade and which workers best match it?

---

## 7. Open Extensions (Not Yet Built)

Carried over and expanded from [data/README.md § 5](data/README.md#5-extending-the-model):

1. **Geo-matching** — replace plain location strings with `zipCode`/lat-lon and a service-area radius per worker.
2. **Pricing model** — a flat callout/trip fee plus hourly, which is how most real trade platforms price emergency work.
3. **License detail** — license number, issuing state, expiry, and verification against a licensing board.
4. **Materials vs. labor billing** — separate `materialsCost` from `laborCost`.
5. **Multi-worker jobs / crews** — a renovation might need a crew, not one worker; model with a `role` (lead/helper) on multiple `HIRED_FOR` relationships to the same job.
6. **Trust network** (see §5 above) — the strongest candidate for actually adopting a graph engine.
7. **Semantic job matching (GraphRAG)** — natural-language problem descriptions matched against trades and worker history.

---

## 8. Related Documents

| Doc | What it covers |
| --- | --- |
| [data/README.md](data/README.md) | Full data model, file list, load instructions, and tested example queries |
| [importdata.md](importdata.md) | General-purpose guide to every way of getting data into Neo4j |
| [README.MD](README.MD) | Neo4j editions, hosting, installation, and a general getting-started walkthrough |
| [gettingstarted.md](gettingstarted.md) | Free Enterprise licensing, Docker setup, and IDE/client tools |
| [../README.MD](../README.MD) | Vector databases and GraphRAG background |
| [../../graph/README.MD](../../graph/README.MD) | Graph database products, licensing, and the "when NOT to use a graph" test referenced in §5 above |
