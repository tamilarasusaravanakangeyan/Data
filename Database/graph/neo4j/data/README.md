# Sample Dataset: "Connecting Workforce" — Local Trades / Gig-Work Marketplace

A small, self-contained dataset for a **local trades and home-services marketplace** — plumbing, electrical, HVAC, carpentry, appliance repair and similar. It connects **skilled tradespeople** (short-term/gig workers — a plumber, electrician, AC technician) with **hirers** who need specific work done — a homeowner with a leaking faucet, a property manager who needs a unit repainted, an HOA that needs recurring pest control.

This is **not** a knowledge-work/office job board. Work here ranges from a **1-hour emergency callout** (unclog a drain, change a lock) to a **multi-week project** (rewire a house, build a deck) — the primary goal for a hirer is simply to **get the job done**, not to fill a long-term role. `Skill` (a trade) is the hub connecting supply (what a tradesperson can do) to demand (what a job requires) — the same "connecting workforce" idea as the platform diagram.

Load it with [import.cypher](import.cypher) into the Neo4j instance from [../docker-compose.yml](../docker-compose.yml), or use it as a template for your own domain. Also see the general-purpose guide in [../importdata.md](../importdata.md).

---

## 1. Graph Model

```text
(:JobSeeker)-[:HAS_SKILL {proficiency, yearsUsed}]->(:Skill)<-[:REQUIRES_SKILL {importance}]-(:Job)<-[:POSTED]-(:Hirer)
      |                                                                        ^
      |-[:APPLIED_TO {appliedDate, proposedRate, status}]--------------------->|
      |-[:HIRED_FOR {scheduledDate, completedDate, laborCost}]---------------->|
      ^
(:Hirer)-[:RATED {rating, comment, ratedDate}]-->(:JobSeeker)
```

`APPLIED_TO` and `HIRED_FOR` capture the funnel from interest to engagement; `RATED` captures reputation feedback after a job is done — important in this domain since trust (is this person licensed, reliable, good work) matters as much as price.

### Nodes

| Label | Key property | Other properties |
| --- | --- | --- |
| **JobSeeker** | `id` (unique) | `name`, `email`, `trade` (e.g. Plumber, Electrician, HVAC Technician), `location`, `yearsExperience`, `hourlyRate`, `licensed` (boolean), `availability` (e.g. "Same-day / Emergency", "Weekdays", "Flexible"), `rating` |
| **Hirer** | `id` (unique) | `name`, `contactName`, `type` (`Residential`/`Commercial`/`PropertyManagement`), `email`, `location`, `phone` |
| **Job** | `id` (unique) | `title`, `description`, `category` (matches a `Skill.category`), `urgency` (`Emergency`/`Scheduled`/`Flexible`), `jobType` (`OneTime`/`LongTerm`/`Recurring`), `estimatedHours`, `budget`, `status` (`Open`/`In Progress`/`Completed`/`Closed`), `postedDate`, `location` |
| **Skill** | `name` (unique) | `category` (Plumbing, Electrical, HVAC, Carpentry, Painting & Finishing, Appliance Repair, Masonry & Structural, Specialty Trades) |

### Relationships

| Relationship | Direction | Properties | Meaning |
| --- | --- | --- | --- |
| `HAS_SKILL` | JobSeeker → Skill | `proficiency`, `yearsUsed` | A trade skill the worker offers |
| `REQUIRES_SKILL` | Job → Skill | `importance` (`Required`/`Preferred`) | A trade skill the job needs |
| `POSTED` | Hirer → Job | — | Who posted the job |
| `APPLIED_TO` | JobSeeker → Job | `appliedDate`, `proposedRate`, `status` (`Applied`/`Shortlisted`/`Hired`/`Rejected`) | A worker expressing interest / quoting a job |
| `HIRED_FOR` | JobSeeker → Job | `scheduledDate`, `completedDate` (null if still in progress), `laborCost` | A confirmed, scheduled engagement |
| `RATED` | Hirer → JobSeeker | `rating` (1–5), `comment`, `ratedDate` | Feedback left after the job is done |

### Why `urgency`, `jobType` and `licensed` matter here

These three properties are what make the model fit *this* domain instead of a generic job board:

- **`urgency`** — an emergency AC repair on a 100°F day is a fundamentally different match than a scheduled kitchen remodel; a marketplace needs to surface available workers fast for the former.
- **`jobType`** — `OneTime` (a single repair), `LongTerm` (a renovation spanning weeks), and `Recurring` (quarterly pest control) all need different matching and pricing logic even though they use the same graph shape.
- **`licensed`** — plumbing and electrical work often legally requires a license; a hirer filtering for licensed-only workers is a common, safety-relevant query (see §4).

### Dataset size

15 job seekers · 8 hirers · 20 jobs · 18 skills · 30 `HAS_SKILL` · 23 `REQUIRES_SKILL` · 25 `APPLIED_TO` · 9 `HIRED_FOR` · 6 `RATED`.

---

## 2. Files

| File | Contents |
| --- | --- |
| [job_seekers.csv](job_seekers.csv) | 15 tradespeople (plumbers, electricians, HVAC techs, handyman, carpenter, painter, appliance repair, locksmith, mason, pest control, flooring) |
| [hirers.csv](hirers.csv) | 8 hirers — a mix of residential homeowners, a café, and property managers/HOA |
| [skills.csv](skills.csv) | 18 trade skills across 8 categories |
| [jobs.csv](jobs.csv) | 20 jobs, each with a `hirerId` foreign key — from a 1-hour lockout to a 3-week deck build |
| [rel_has_skill.csv](rel_has_skill.csv) | JobSeeker → Skill |
| [rel_requires_skill.csv](rel_requires_skill.csv) | Job → Skill |
| [rel_applied.csv](rel_applied.csv) | JobSeeker → Job applications/quotes |
| [rel_hired.csv](rel_hired.csv) | JobSeeker → Job confirmed engagements |
| [rel_rated.csv](rel_rated.csv) | Hirer → JobSeeker feedback |
| [import.cypher](import.cypher) | Constraints + `LOAD CSV` statements for every file above, in the right order |

---

## 3. Load It

Using the Docker setup from [../docker-compose.yml](../docker-compose.yml) (container `neo4j-ee`, import folder mounted at `./neo4j/import` relative to that compose file):

```bash
cd Database/vectordatabase/neo4j

# 1. Copy the CSVs into the container's import folder
cp data/*.csv neo4j/import/

# 2. Run the import script
docker exec -i neo4j-ee cypher-shell -u neo4j -p 'ChangeMe123!' -d neo4j < data/import.cypher
```

The script prints node and relationship counts at the end — check them against §1's dataset size.

**Neo4j Desktop:** copy the CSVs into the instance's import folder (Desktop → instance → **Open Folder → Import**), then run `import.cypher` from the Query pane or `cypher-shell` (see [gettingstarted.md § IDE and Client Tools](../gettingstarted.md#5-ide-and-client-tools)).

**AuraDB:** `LOAD CSV` needs a URL, not a local file — either push these CSVs to a public/pre-signed URL and change `file:///...` to `https://...` in `import.cypher`, or use the **Data Importer** UI instead (see [../importdata.md § 4](../importdata.md)).

To start over: `MATCH (n) DETACH DELETE n;` then re-run `import.cypher`.

> This exact sequence was run and verified against the Docker container in this repo: node/relationship counts and every query in §4 below were checked live, not just written from memory.

---

## 4. Example Queries

**Find licensed workers who can do an emergency job (e.g. a furnace that won't start):**

```cypher
MATCH (j:Job {id: 'J8'})-[:REQUIRES_SKILL {importance: 'Required'}]->(s:Skill)
MATCH (js:JobSeeker)-[:HAS_SKILL]->(s)
WHERE js.licensed = true
RETURN js.name, js.trade, js.hourlyRate, js.availability, js.rating
ORDER BY js.rating DESC;
```

**Which job seekers match a job's required skills?**

```cypher
MATCH (j:Job {id: 'J1'})-[:REQUIRES_SKILL {importance: 'Required'}]->(s:Skill)
MATCH (js:JobSeeker)-[:HAS_SKILL]->(s)
RETURN js.name, js.trade, js.hourlyRate, js.rating, collect(s.name) AS matchedSkills, count(s) AS matchCount
ORDER BY matchCount DESC, js.rating DESC;
```

**Open long-term projects (renovations, not quick repairs), with required trades and estimated hours:**

```cypher
MATCH (j:Job {jobType: 'LongTerm', status: 'Open'})-[:REQUIRES_SKILL]->(s:Skill)
RETURN j.title, j.estimatedHours, j.budget, collect(s.name) AS requiredSkills
ORDER BY j.estimatedHours DESC;
```

**Recommend open jobs to a worker, based on their skills (excluding jobs they've already applied to):**

```cypher
MATCH (js:JobSeeker {id: 'JS14'})-[:HAS_SKILL]->(s:Skill)<-[:REQUIRES_SKILL]-(j:Job {status: 'Open'})
WHERE NOT (js)-[:APPLIED_TO]->(j)
RETURN j.title, j.urgency, j.budget, collect(s.name) AS overlappingSkills, count(s) AS overlap
ORDER BY overlap DESC, j.budget DESC;
```

> With `JS1` (Carlos Mendez) instead, this returns nothing — he's already applied to every open job matching his skills. That's expected, not a bug: it's what "recommend jobs they haven't seen yet" should do once someone's found everything relevant.

**A hirer's job history and who they rated:**

```cypher
MATCH (h:Hirer {id: 'H6'})-[:POSTED]->(j:Job)<-[:HIRED_FOR]-(js:JobSeeker)
OPTIONAL MATCH (h)-[r:RATED]->(js)
RETURN j.title, js.name, js.trade, r.rating, r.comment;
```

**Average labor cost by trade category, for completed/closed jobs:**

```cypher
MATCH (js:JobSeeker)-[r:HIRED_FOR]->(j:Job)
WHERE j.status IN ['Completed', 'Closed']
RETURN j.category, avg(r.laborCost) AS avgLaborCost, count(*) AS jobsDone
ORDER BY avgLaborCost DESC;
```

**Most in-demand trades across open jobs:**

```cypher
MATCH (j:Job {status: 'Open'})-[:REQUIRES_SKILL]->(s:Skill)
RETURN s.name, s.category, count(j) AS demand
ORDER BY demand DESC;
```

**Vector-search-ready extension:** add a `description` embedding to `Job` and a `bio`/reviews embedding to `JobSeeker` (see [../README.MD § Step 6](../README.MD)) so a hirer can type "my AC is dripping water and not cooling" in plain language and match it against both structured trade skills and free-text job history — a small-scale GraphRAG pattern (see [../../README.MD § 8](../../README.MD)).

---

## 5. Extending the Model

Ideas for making this closer to a real product, not implemented in this sample:

- **Geo-matching:** replace the plain `location` string with a `zipCode` or lat/lon on `Hirer`/`Job`, and a `serviceAreaZipcodes` list (or `serviceRadiusMiles`) on `JobSeeker`, so you can query "workers who actually cover this job's area" instead of an exact string match.
- **Pricing model:** trade platforms often mix a flat **callout/trip fee** with an hourly rate for anything beyond the first N minutes — add `calloutFee` to `JobSeeker` and `Job` if you need to model that.
- **Licensing detail:** `licensed` is a simple boolean here; a real system would store `licenseNumber`, `licenseState`, and an expiry date, and verify it against a state licensing board.
- **Materials vs. labor:** `budget`/`laborCost` here are labor-only; add a `materialsCost` property on `Job`/`HIRED_FOR` if the platform also handles parts and materials billing.
- **Multi-worker jobs:** a deck build or full rewiring might need a crew, not one worker — model with multiple `HIRED_FOR` relationships from different `JobSeeker` nodes to the same `Job`, optionally with a `role` property (lead/helper).
