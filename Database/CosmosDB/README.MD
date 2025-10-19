## Azure Cosmos DB

Brief:
Azure Cosmos DB is a fully managed, globally distributed, multi-model NoSQL database service
that provides low-latency reads and writes, elastic scalability of throughput and storage,
and comprehensive SLAs for availability, latency, throughput and consistency.

Key capabilities:

- Multi-model and multi-API: supports document, key-value, column-family and graph models
  and interoperates with SQL (Core), MongoDB, Cassandra, Gremlin and Table APIs.
- Global distribution: transparently replicate data to any number of Azure regions with
  configurable read/write region topology and multi-master support.
- Tunable consistency: five consistency levels (Strong, Bounded Staleness, Session, Consistent Prefix, Eventual).
- Elastic scale: provisioned throughput (RUs) or serverless, automatic partitioning of data,
  and dynamic scale of RU/s and storage.
- Rich platform features: automatic indexing, TTL, change feed, integrated backups, and enterprise-grade security.

When to use Cosmos DB:

- You need predictable low-latency reads and writes across one or multiple geographic regions.
- Your application requires seamless global distribution or active-active (multi-master) writes.
- You need a multi-model solution or want to use one of the supported APIs (e.g., MongoDB or Cassandra API)
  without managing database servers.
- Your workload demands elastic, fine-grained throughput control and strong SLAs for latency/availability.
- Use cases: real-time personalization, IoT telemetry ingestion, user/session stores, gaming leaderboards,
  e-commerce catalogs, social graphs, event sourcing with change feed consumers, global SaaS applications.

* When NOT to use Cosmos DB:

- Your workload is primarily relational with complex multi-row ACID transactions and joins across many tables
  (consider a relational database like Azure SQL or PostgreSQL).
- You have small, cost-sensitive, single-region workloads where a simpler/cheaper store (e.g., Azure Table Storage,
  managed MongoDB, or a single-node database) is a better fit.
- Heavy analytical queries over large historical datasets are the primary need (use data warehouses or analytics stores).

Operational considerations:

- Cost model is driven by provisioned throughput (RUs) and storage; design partition keys and indexing policies
  to minimize RU consumption and cost.
- Choose appropriate consistency level to balance correctness vs latency/cost.
- Consider serverless vs provisioned throughput and autoscale options based on traffic patterns.

Summary:
Use Cosmos DB when you need a managed, globally distributed, low-latency NoSQL service with flexible data models,
strong SLAs and elasticity. Avoid it for classic relational OLTP/OLAP or when a lower-cost, simpler store suffices.
