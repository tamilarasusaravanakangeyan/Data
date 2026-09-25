// ============================================================================
// Connecting Workforce — sample dataset import
// Domain: local trade/gig-work marketplace (plumbing, electrical, HVAC,
// handyman, ...) — short jobs (an hour, an emergency callout) and long
// jobs (a multi-week renovation) side by side.
//
// Graph model: JobSeeker -[:HAS_SKILL]-> Skill <-[:REQUIRES_SKILL]- Job <-[:POSTED]- Hirer
//              JobSeeker -[:APPLIED_TO]-> Job
//              JobSeeker -[:HIRED_FOR]-> Job
//              Hirer     -[:RATED]-> JobSeeker
//
// Run with cypher-shell, piping this whole file, e.g.:
//   docker exec -i neo4j-ee cypher-shell -u neo4j -p 'ChangeMe123!' -d neo4j < import.cypher
//
// Prerequisite: copy every *.csv in this folder into the server's import
// directory first (see README.md § 3 for the exact command for this repo's
// Docker setup).
// ============================================================================

// ---------------------------------------------------------------------------
// 1. Constraints (create indexes and enforce uniqueness before loading)
// ---------------------------------------------------------------------------
CREATE CONSTRAINT job_seeker_id IF NOT EXISTS FOR (js:JobSeeker) REQUIRE js.id IS UNIQUE;
CREATE CONSTRAINT hirer_id      IF NOT EXISTS FOR (h:Hirer)      REQUIRE h.id IS UNIQUE;
CREATE CONSTRAINT job_id        IF NOT EXISTS FOR (j:Job)        REQUIRE j.id IS UNIQUE;
CREATE CONSTRAINT skill_name    IF NOT EXISTS FOR (s:Skill)      REQUIRE s.name IS UNIQUE;

CREATE INDEX job_seeker_trade IF NOT EXISTS FOR (js:JobSeeker) ON (js.trade);
CREATE INDEX job_status       IF NOT EXISTS FOR (j:Job)        ON (j.status);
CREATE INDEX job_urgency      IF NOT EXISTS FOR (j:Job)        ON (j.urgency);

// ---------------------------------------------------------------------------
// 2. Node loads
// ---------------------------------------------------------------------------

// Skills / trades (load first — everything else references them)
LOAD CSV WITH HEADERS FROM 'file:///skills.csv' AS row
MERGE (s:Skill {name: row.name})
SET s.category = row.category;

// Job seekers (skilled tradespeople offering short-term / gig work)
LOAD CSV WITH HEADERS FROM 'file:///job_seekers.csv' AS row
MERGE (js:JobSeeker {id: row.jobSeekerId})
SET js.name             = row.name,
    js.email             = row.email,
    js.trade             = row.trade,
    js.location          = row.location,
    js.yearsExperience   = toInteger(row.yearsExperience),
    js.hourlyRate        = toFloat(row.hourlyRate),
    js.licensed          = toBoolean(row.licensed),
    js.availability      = row.availability,
    js.rating            = toFloat(row.rating);

// Hirers (homeowners, businesses, property managers who need work done)
LOAD CSV WITH HEADERS FROM 'file:///hirers.csv' AS row
MERGE (h:Hirer {id: row.hirerId})
SET h.name         = row.name,
    h.contactName  = row.contactName,
    h.type         = row.type,
    h.email        = row.email,
    h.location     = row.location,
    h.phone        = row.phone;

// Jobs (the work to get done — also connects to the hirer that posted it)
LOAD CSV WITH HEADERS FROM 'file:///jobs.csv' AS row
MERGE (j:Job {id: row.jobId})
SET j.title           = row.title,
    j.description     = row.description,
    j.category        = row.category,
    j.urgency         = row.urgency,        // Emergency / Scheduled / Flexible
    j.jobType         = row.jobType,        // OneTime / LongTerm / Recurring
    j.estimatedHours  = toFloat(row.estimatedHours),
    j.budget          = toFloat(row.budget),
    j.status          = row.status,         // Open / In Progress / Completed / Closed
    j.postedDate      = date(row.postedDate),
    j.location        = row.location
WITH j, row
MATCH (h:Hirer {id: row.hirerId})
MERGE (h)-[:POSTED]->(j);

// ---------------------------------------------------------------------------
// 3. Relationship loads
// ---------------------------------------------------------------------------

// JobSeeker -[:HAS_SKILL]-> Skill
LOAD CSV WITH HEADERS FROM 'file:///rel_has_skill.csv' AS row
CALL (row) {
  MATCH (js:JobSeeker {id: row.jobSeekerId})
  MATCH (s:Skill {name: row.skillName})
  MERGE (js)-[r:HAS_SKILL]->(s)
  SET r.proficiency = row.proficiency,
      r.yearsUsed    = toInteger(row.yearsUsed)
} IN TRANSACTIONS OF 500 ROWS;

// Job -[:REQUIRES_SKILL]-> Skill
LOAD CSV WITH HEADERS FROM 'file:///rel_requires_skill.csv' AS row
CALL (row) {
  MATCH (j:Job {id: row.jobId})
  MATCH (s:Skill {name: row.skillName})
  MERGE (j)-[r:REQUIRES_SKILL]->(s)
  SET r.importance = row.importance
} IN TRANSACTIONS OF 500 ROWS;

// JobSeeker -[:APPLIED_TO]-> Job
LOAD CSV WITH HEADERS FROM 'file:///rel_applied.csv' AS row
CALL (row) {
  MATCH (js:JobSeeker {id: row.jobSeekerId})
  MATCH (j:Job {id: row.jobId})
  MERGE (js)-[r:APPLIED_TO]->(j)
  SET r.appliedDate   = date(row.appliedDate),
      r.proposedRate  = toFloat(row.proposedRate),
      r.status        = row.status
} IN TRANSACTIONS OF 500 ROWS;

// JobSeeker -[:HIRED_FOR]-> Job (confirmed engagements)
LOAD CSV WITH HEADERS FROM 'file:///rel_hired.csv' AS row
CALL (row) {
  MATCH (js:JobSeeker {id: row.jobSeekerId})
  MATCH (j:Job {id: row.jobId})
  MERGE (js)-[r:HIRED_FOR]->(j)
  SET r.scheduledDate = date(row.scheduledDate),
      r.completedDate = CASE WHEN row.completedDate IS NULL OR row.completedDate = '' THEN null ELSE date(row.completedDate) END,
      r.laborCost     = toFloat(row.laborCost)
} IN TRANSACTIONS OF 500 ROWS;

// Hirer -[:RATED]-> JobSeeker (post-job feedback)
LOAD CSV WITH HEADERS FROM 'file:///rel_rated.csv' AS row
CALL (row) {
  MATCH (h:Hirer {id: row.hirerId})
  MATCH (js:JobSeeker {id: row.jobSeekerId})
  MERGE (h)-[r:RATED]->(js)
  SET r.rating    = toInteger(row.rating),
      r.comment   = row.comment,
      r.ratedDate = date(row.ratedDate)
} IN TRANSACTIONS OF 500 ROWS;

// ---------------------------------------------------------------------------
// 4. Sanity checks — expected counts: 15 JobSeeker, 8 Hirer, 18 Skill, 20 Job
// ---------------------------------------------------------------------------
MATCH (n) RETURN labels(n)[0] AS label, count(*) AS count ORDER BY label;
MATCH ()-[r]->() RETURN type(r) AS relType, count(*) AS count ORDER BY relType;
