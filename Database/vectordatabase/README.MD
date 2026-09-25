# Vector Databases

A vector database stores **embeddings**: arrays of numbers (e.g. 768 or 1536 floats) that an ML model produces from text, images, audio or any other data. Items with similar meaning end up close together in that vector space, so the core query is **"find the K items nearest to this vector"** (k-nearest-neighbour, kNN). This is what powers semantic search, RAG for LLMs, recommendations and image similarity.

```text
"cheap flight to Dubai"  --embedding model-->  [0.12, -0.83, 0.44, ... 1536 dims]
                                                          |
                                                          v
                                   Vector index (HNSW / IVF / DiskANN ...)
                                                          |
                                                          v
          Top-K nearest:  "low-fare DXB offers" (0.91), "budget UAE trips" (0.88), ...
```

A keyword index matches **words**. A vector index matches **meaning**. "Cheap flight" and "low-fare ticket" share no words but land next to each other.

---

## 1. Core Concepts

### 1.1 Embeddings

| Term | What it means |
| --- | --- |
| **Embedding model** | Converts data to vectors. Examples: OpenAI `text-embedding-3-small/large`, Cohere Embed, Voyage, Google `gemini-embedding`, open models such as BGE, E5, Nomic, Jina, and CLIP for images. |
| **Dimensions** | Length of the vector. Common sizes: 384, 768, 1024, 1536, 3072. More dimensions = more nuance, but more memory and slower search. |
| **Dense vector** | Every position has a value. Captures semantic meaning. The usual "vector search". |
| **Sparse vector** | Mostly zeros, one position per vocabulary term (BM25, SPLADE). Captures exact keywords. |
| **Multi-vector** | Several vectors per document (e.g. ColBERT, one per token). Higher accuracy, more storage. |
| **Chunking** | Splitting long documents into pieces (e.g. 300–800 tokens with overlap) before embedding. Chunk quality affects RAG results more than the choice of database. |

> ⚠️ You **must** query with the same embedding model (and version) that you indexed with. Changing the model means re-embedding everything.

### 1.2 Distance / similarity metrics

| Metric | Formula idea | Use when |
| --- | --- | --- |
| **Cosine similarity** | Angle between vectors, ignores length | Default for text embeddings. |
| **Dot product (inner product)** | Angle × length | Vectors are already normalised (then equals cosine, and is faster), or the model was trained for it. Common in recommendations. |
| **Euclidean (L2)** | Straight-line distance | Image/computer-vision embeddings, some older models. |
| **Hamming** | Count of differing bits | Binary-quantized vectors. |
| **Jaccard** | Set overlap | Sparse/binary set data. |

👉 Use the metric the embedding model's documentation recommends. With normalised vectors, cosine, dot product and L2 give the same ranking.

### 1.3 Exact vs approximate search

- **Exact kNN (brute force / "flat")** compares the query with every vector. 100% recall, but cost grows linearly: fine for up to ~100K vectors, too slow for hundreds of millions.
- **Approximate nearest neighbour (ANN)** uses an index to check only a small fraction of vectors. Typically **95–99% recall** at **100–1000× less work**. Every production vector database relies on ANN.
- **Recall@K** = what fraction of the true top-K results the ANN index returned. It is *the* quality metric; always measure it against a brute-force baseline on your own data.

---

## 2. Vector Search Algorithms (Index Types)

### 2.1 The algorithms

| Algorithm | Family | How it works (one-liner) | Strengths | Weaknesses |
| --- | --- | --- | --- | --- |
| **Flat / brute force** | Exact | Compares the query with every vector. | 100% recall, no build time, no tuning. | Linear cost; slow beyond ~1M vectors. |
| **HNSW** (Hierarchical Navigable Small World) | Graph | A multi-layer graph: coarse "express" layers on top, a dense graph at the bottom; search greedily hops towards the query. | Best all-round recall/latency; supports inserts. **The industry default.** | Memory-hungry (whole index in RAM); slow to build; deletes leave tombstones that need cleanup. |
| **IVF** (Inverted File, IVF-Flat) | Clustering | k-means splits vectors into `nlist` clusters; a query searches only the `nprobe` nearest clusters. | Fast build, lower memory than HNSW, simple. | Lower recall than HNSW at the same speed; clusters drift as data changes (needs retraining). |
| **PQ** (Product Quantization), **IVF-PQ** | Compression | Splits each vector into sub-vectors and replaces each with a 1-byte code, e.g. 6 KB → 64–128 bytes. Usually combined with IVF. | 10–64× less memory; billions of vectors on one machine. | Lossy, so lower recall; usually needs a **re-ranking** step on full vectors. |
| **SQ / BQ** (Scalar / Binary Quantization) | Compression | Stores each float32 as int8 (SQ, 4× smaller) or a single bit (BQ, 32× smaller). Applied on top of HNSW/IVF. | Big memory savings; BQ makes distance calculations very fast. | Recall loss (BQ mainly works with high-dimension models); needs re-scoring with full vectors. |
| **DiskANN / Vamana** | Graph (SSD) | Graph index kept on **SSD**, with compressed vectors in RAM to guide the search. From Microsoft Research. | Billion-scale on one node at a fraction of the RAM; good filtered search variants. | Relies on fast NVMe SSD; build is heavy. |
| **ScaNN** | Clustering + quantization | Google's algorithm: partitioning + anisotropic quantization tuned for inner product. | Very high throughput per CPU. | Mostly available in Google products (Vertex AI, AlloyDB) and the ScaNN library. |
| **SPANN / SPFresh** | Clustering (SSD) | Cluster centroids in RAM, posting lists on disk; SPFresh adds in-place updates. | Cheap billion-scale; underpins object-storage designs. | Higher latency than in-memory HNSW. |
| **CAGRA** (NVIDIA cuVS) | Graph (GPU) | A graph index built and searched on GPU. | Extremely high throughput and fast builds. | Needs GPUs; best for large batch workloads. |
| **LSH** (Locality-Sensitive Hashing) | Hashing | Random hash functions put similar vectors in the same bucket. | Simple, streaming-friendly, theory guarantees. | Poor recall/memory trade-off compared with HNSW/IVF; rarely used now. |
| **Annoy** | Trees | A forest of random-projection trees (Spotify). | Read-only memory-mapped index, small footprint. | Static (rebuild to add data); largely superseded. |

### 2.2 How they compare

| | Flat | HNSW | IVF-Flat | IVF-PQ | DiskANN | GPU (CAGRA) |
| --- | --- | --- | --- | --- | --- | --- |
| **Recall** | 100% | ⭐⭐⭐⭐⭐ (95–99%+) | ⭐⭐⭐⭐ | ⭐⭐⭐ (⭐⭐⭐⭐ with re-rank) | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Query latency** | 🐢 Linear | ⚡ ~1–10 ms | ⚡ Low | ⚡ Low | ⚡ ~few ms (SSD) | ⚡⚡ Very high throughput |
| **Memory** | Raw vectors | 🔴 High (vectors + graph in RAM) | 🟡 Medium | 🟢 Very low | 🟢 Low (data on SSD) | 🔴 GPU memory |
| **Build time** | None | 🐢 Slow | ⚡ Fast | Medium (training) | 🐢 Slow | ⚡⚡ Very fast |
| **Inserts/updates** | ✅ Trivial | ✅ Good | ⚠️ Clusters drift | ⚠️ Clusters drift | ✅ (FreshDiskANN variants) | ⚠️ Mostly batch |
| **Sweet spot** | < 100K vectors, or a heavily pre-filtered subset | 100K – ~100M, low latency | Millions, memory-conscious | 100M – billions, cost-conscious | 100M – billions on one node | Very high QPS or huge batch jobs |

### 2.3 Tuning knobs you will meet

| Index | Knob | Effect |
| --- | --- | --- |
| HNSW | `M` (links per node, typically 16–64) | Higher = better recall, more memory, slower build. |
| HNSW | `ef_construction` (typically 100–500) | Higher = better graph quality, slower build. |
| HNSW | `ef_search` / `ef` (query time) | Higher = better recall, slower query. **The main runtime knob.** |
| IVF | `nlist` (number of clusters, ~√N to 4√N) | More clusters = smaller lists to scan, but more centroids to compare. |
| IVF | `nprobe` (clusters searched per query) | Higher = better recall, slower query. |
| PQ | `m` (sub-vectors), `nbits` | More sub-vectors/bits = better accuracy, more memory. |
| Any quantized index | `rescore` / `oversample` | Fetch e.g. 3× K candidates with compressed vectors, then re-rank them with full vectors. Recovers most lost recall cheaply. |

---

## 3. Vector Database Products

There are three kinds of product:

1. **Purpose-built vector databases** (Pinecone, Milvus, Qdrant, Weaviate, Chroma, LanceDB ...).
2. **Existing databases with vector support** (PostgreSQL + pgvector, Oracle 23ai, SQL Server 2025, MongoDB, Cosmos DB, Redis, Elasticsearch/OpenSearch ...).
3. **Libraries** you embed in your own code (FAISS, hnswlib, ScaNN, cuVS). No server, no persistence, no filtering API.

| Product | Type | Index algorithms supported | Hybrid (keyword + vector) | Deployment | When to use (one-liner) | License | Free to use (commercial vs non-commercial)? |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Pinecone** | Purpose-built | Proprietary (managed; serverless architecture on object storage) | ✅ Sparse + dense | SaaS only (AWS/Azure/GCP); BYOC for enterprise | Zero-ops managed vector search when you don't want to tune indexes. | Proprietary | 🟡 **Starter (free) tier** with storage/usage limits, usable commercially. Beyond that, pay-as-you-go with a monthly minimum. |
| **Milvus** / **Zilliz Cloud** | Purpose-built, distributed | FLAT, IVF_FLAT, IVF_SQ8, IVF_PQ, HNSW (+SQ/PQ/PRQ), **DiskANN**, SCANN, GPU_CAGRA, GPU_IVF_FLAT/PQ, sparse (inverted/WAND), BIN_FLAT/BIN_IVF | ✅ Full-text BM25 + dense + sparse | Self-managed (Lite, Standalone, Distributed on K8s); Zilliz Cloud | Billion-scale self-hosted vector search; the widest choice of index types. | Apache 2.0 | 🟢 **Yes**, Milvus is free for any use. Zilliz Cloud has a free tier, then paid. |
| **Qdrant** | Purpose-built | HNSW (filter-aware), flat; scalar, product, binary quantization; sparse vectors; multi-vector | ✅ Dense + sparse/BM25 fusion | Self-managed, Qdrant Cloud, Hybrid Cloud | Fast, filter-heavy search with a small operational footprint (Rust). | Apache 2.0 | 🟢 **Yes**, free for any use. Qdrant Cloud has a free 1 GB cluster, then paid. |
| **Weaviate** | Purpose-built | HNSW, flat, dynamic (flat → HNSW automatically); PQ, SQ, BQ, RQ compression | ✅ BM25 + vector, built-in | Self-managed, Weaviate Cloud | Hybrid search with built-in embedding "vectorizer" modules and multi-tenancy. | BSD-3-Clause | 🟢 **Yes**, free for any use. Weaviate Cloud: free sandbox (time-limited), then paid. |
| **Chroma** | Purpose-built, lightweight | HNSW (hnswlib); SPANN in distributed/cloud version | ✅ (full-text + regex filters) | Embedded (Python/JS), client-server, Chroma Cloud | Prototypes, notebooks and small-to-medium RAG apps; easiest start. | Apache 2.0 | 🟢 **Yes**, free for any use. Chroma Cloud is paid (with free credits). |
| **LanceDB** | Embedded, columnar (Lance format) | IVF_PQ, IVF_HNSW_SQ/PQ, IVF_FLAT, flat; GPU index build | ✅ Full-text (Tantivy/native) + vector | Embedded (in-process), on S3/local disk; LanceDB Cloud/Enterprise | Serverless or edge apps and multimodal data lakes; vectors stored on cheap object storage. | Apache 2.0 | 🟢 **Yes**, OSS is free for any use. Cloud/Enterprise are paid. |
| **pgvector** (PostgreSQL extension) | Extension | Flat (exact), **IVFFlat**, **HNSW**; `halfvec` (float16), `bit` (binary), `sparsevec` | ✅ With Postgres full-text search + RRF in SQL | Anywhere Postgres runs: self-managed, Azure/AWS/GCP managed Postgres, Supabase, Neon | You already use Postgres and have up to tens of millions of vectors. **The most common enterprise starting point.** | PostgreSQL License (permissive) | 🟢 **Yes**, free for any use. Managed Postgres services cost normal DB prices. |
| **pgvectorscale** (Timescale) | Extension on pgvector | **StreamingDiskANN** with Statistical Binary Quantization; label-based filtering | ✅ (via Postgres) | Self-managed Postgres, Timescale Cloud | pgvector at larger scale (tens–hundreds of millions) with lower RAM. | PostgreSQL License | 🟢 **Yes**, free for any use. |
| **Oracle Database 23ai / 26ai AI Vector Search** | Converged DB | Exact; **HNSW** (in-memory neighbor graph), **IVF** (neighbor partitions); hybrid vector indexes | ✅ Oracle Text + vector hybrid index | On-prem, OCI, Autonomous DB, Exadata | Data already in Oracle; join vectors with relational data in one SQL statement. | Proprietary; AI Vector Search included at no extra cost | 🟡 **Oracle Database Free** is free (limits: 2 CPU, 2 GB RAM, 12 GB data). Otherwise a normal DB license. |
| **SQL Server 2025 / Azure SQL** | Converged DB | `VECTOR` data type, exact `VECTOR_DISTANCE`; **DiskANN** approximate index (`VECTOR_SEARCH`; check preview/GA status) | ✅ With full-text search | On-prem, Azure SQL DB / MI | Vector search inside an existing SQL Server/.NET app. | Proprietary | 🟡 **Express** free for commercial use (size-limited); **Developer** free for non-production. Otherwise paid. |
| **Azure Cosmos DB for NoSQL** | Converged (document) | flat, quantizedFlat, **DiskANN** (incl. filtered DiskANN) | ✅ Full-text + vector (RRF) | Azure managed | Globally distributed apps and AI agent memory next to operational JSON data. | Proprietary (Azure) | 🟡 **Free tier:** first 1000 RU/s + 25 GB per account; free emulator for dev. Otherwise paid. |
| **Azure DocumentDB** (Cosmos DB for MongoDB vCore) | Converged (document) | IVF, HNSW, DiskANN | ✅ | Azure managed | MongoDB-API apps on Azure that need vectors. | Proprietary (engine based on open-source DocumentDB, MIT) | 🟡 Free tier cluster available; otherwise paid. |
| **Azure AI Search** | Search engine | **HNSW**, exhaustive KNN; scalar/binary quantization | ✅ BM25 + vector + **semantic ranker** | Azure managed | Enterprise RAG over documents with built-in chunking/vectorization, security trimming and re-ranking. | Proprietary (Azure) | 🟡 **Free tier** (tiny: ~50 MB storage, shared), for dev only. Production is billed per search unit. |
| **Elasticsearch** | Search engine | **HNSW** (Lucene), flat; int8/int4 and **BBQ** (Better Binary Quantization); DiskBBQ | ✅ BM25 + vector + RRF, ELSER sparse | Self-managed, Elastic Cloud | You already run Elastic for search/logs and want hybrid search in one engine. | Triple license: AGPLv3 / SSPL / Elastic License 2.0 | 🟢 **Yes** for self-managed use (AGPL or ELv2 terms). Some advanced features (e.g. RRF, ML at scale) need paid tiers — check the subscription matrix. Elastic Cloud is paid. |
| **OpenSearch** | Search engine | **HNSW** and **IVF** via Faiss or Lucene engines; PQ, SQ, binary quantization; disk-optimized mode; GPU build | ✅ BM25 + vector, neural sparse | Self-managed, Amazon OpenSearch Service / Serverless | Open-source alternative to Elastic; the natural choice on AWS. | Apache 2.0 | 🟢 **Yes**, free for any use. AWS managed service is paid. |
| **Redis** (Redis Stack / Redis 8 Query Engine) | In-memory DB | FLAT, **HNSW**, **SVS-VAMANA** (Intel SVS, with compression); also `Vector Sets` data type | ✅ Full-text + vector | Self-managed, Redis Cloud, Azure Managed Redis | Ultra-low-latency search, semantic caching of LLM responses, session/agent memory. | Redis 8: AGPLv3 / RSALv2 / SSPL (choice) | 🟢 **Yes** self-managed under AGPLv3 (or RSAL for internal use). Redis Cloud: free 30 MB tier, then paid. |
| **MongoDB Atlas Vector Search** | Converged (document) | **HNSW**, exact (ENN); scalar/binary quantization | ✅ With Atlas Search (`$rankFusion`) | Atlas (cloud); Community/Enterprise self-managed (check current status) | Vectors next to your operational MongoDB documents. | Proprietary (Atlas); Community under SSPL | 🟡 **Atlas M0 free cluster** (512 MB). Production is paid. |
| **Google Vertex AI Vector Search** | Managed service | **ScaNN** (tree-AH), brute force | ✅ Dense + sparse | GCP managed | Very high QPS at large scale on GCP. | Proprietary (GCP) | 🔴 **No**; paid (GCP free-trial credits only). |
| **AlloyDB / Cloud SQL for PostgreSQL** | Converged (Postgres) | pgvector (IVFFlat, HNSW) + **ScaNN index** (AlloyDB) | ✅ | GCP managed | Postgres on GCP with Google's ScaNN performance. | Proprietary service; AlloyDB Omni downloadable | 🔴 Paid managed service; AlloyDB Omni has a free developer edition. |
| **Amazon S3 Vectors** | Object-storage vector store | Managed (proprietary) | ❌ (vector + metadata filter) | AWS managed | Very cheap storage for huge, rarely-queried vector sets (archives, long-term agent memory); pair with OpenSearch for hot data. | Proprietary (AWS) | 🔴 Pay-per-use; very low storage cost, higher query latency (sub-second, not milliseconds). |
| **Vespa** | Search + ranking engine | **HNSW**, exact; binary/int8 tensors; multi-vector (ColBERT) | ✅ Advanced, multi-phase ranking | Self-managed, Vespa Cloud | Large-scale search and recommendation with complex custom ranking. | Apache 2.0 | 🟢 **Yes**, free for any use. Vespa Cloud is paid. |
| **Turbopuffer** | Purpose-built, object-storage-first | Proprietary (centroid-based ANN on S3/GCS with SSD/RAM cache) | ✅ BM25 + vector | SaaS | Very large, multi-tenant corpora where most data is cold (cost over latency). | Proprietary | 🔴 Paid (usage-based, with a minimum). |
| **ClickHouse** | Analytics DB | Exact; HNSW `vector_similarity` index (usearch) | ⚠️ Partial | Self-managed, ClickHouse Cloud | Vector search mixed with big analytical queries. | Apache 2.0 | 🟢 **Yes**, free for any use. Cloud is paid. |
| **FAISS** (Meta) | Library | Flat, IVF, PQ, IVF-PQ, HNSW, LSH, OPQ, NSG, GPU (+ cuVS/CAGRA) | ❌ | In-process (C++/Python) | Research, batch jobs, or building your own service; the reference implementation of most algorithms. | MIT | 🟢 **Yes**, free for any use. |
| **hnswlib / USearch / ScaNN / Annoy** | Libraries | HNSW / HNSW (many types) / ScaNN / Annoy trees | ❌ | In-process | Embedding a fast ANN index in an app without a DB. | Apache 2.0 | 🟢 **Yes**, free for any use. |

**Legend:** 🟢 free for commercial use · 🟡 free tier or edition with limits · 🟠 free for non-commercial use only · 🔴 paid (trial only)

> Unlike graph databases, almost no vector database is "free for non-commercial use only". The popular open-source engines (Milvus, Qdrant, Weaviate, Chroma, pgvector, OpenSearch, FAISS) are under permissive licenses (Apache 2.0 / BSD / MIT / PostgreSQL), so they are **free for commercial use too**. The cost is infrastructure and operations, not licensing.
>
> ⚠️ Pricing, free-tier limits, index support and preview/GA status change often. Check the vendor's current pages, and your legal team for licenses such as AGPL/SSPL.

### License types in plain English

| License | Can I use it free in a commercial product? | Catch |
| --- | --- | --- |
| **Apache 2.0 / MIT / BSD / PostgreSQL** | ✅ Yes | None; keep the notices. |
| **AGPLv3** (Redis 8, Elasticsearch option) | ✅ Yes, internally | If you modify it *and* let users interact with it over a network, you must publish your modifications. |
| **SSPL** (MongoDB Community, Redis/Elastic option) | ✅ Yes, internally | If you offer it *as a service*, you must open-source your whole service stack. |
| **Elastic License 2.0** | ✅ Yes | You can't offer it as a managed service or bypass license-key features. |
| **Cloud managed services** | ❌ Pay-per-use | Free tiers exist, production costs money. |

---

## 4. Performance

### 4.1 What drives performance

| Factor | Impact |
| --- | --- |
| **Number of vectors (N)** | Brute force grows linearly; HNSW grows roughly logarithmically. |
| **Dimensions (d)** | Each distance calculation costs O(d). 3072-d is ~2× slower and ~2× bigger than 1536-d. |
| **Index type + parameters** | `ef_search` / `nprobe` trade recall for latency. The same data can go from 0.90 to 0.99 recall by changing one knob. |
| **Does the index fit in RAM?** | The biggest cliff. HNSW that spills to disk becomes dramatically slower. If it can't fit, use quantization or a disk index (DiskANN, IVF-PQ). |
| **Metadata filtering** | Filters like `airline = 'EK' AND date > ...` can wreck recall or latency if the engine filters *after* the ANN search. Prefer engines with **filter-aware / pre-filtering** (Qdrant, Weaviate, Milvus, Cosmos DB filtered DiskANN, pgvector iterative scans). |
| **Write rate** | Heavy inserts/deletes slow HNSW builds and fragment indexes; plan compaction/re-indexing. |
| **Top-K size** | K = 10 is cheap; K = 1000 is not. |
| **Network + embedding call** | Embedding the query via an API often takes 50–300 ms, far more than the vector search itself (1–10 ms). |

### 4.2 Memory sizing rule of thumb

```text
raw vector size   = dimensions × bytes per value
float32 1536-d    = 1536 × 4  = 6 KB per vector
1M vectors        ≈ 6 GB      10M ≈ 61 GB      100M ≈ 614 GB   (raw, before index)
HNSW overhead     ≈ M × 2 × 4 bytes per vector (M=16 → ~128 B) + metadata
```

| Compression for 10M × 1536-d | Size | Typical recall impact |
| --- | --- | --- |
| float32 (none) | ~61 GB | baseline |
| float16 / halfvec | ~31 GB | negligible |
| int8 scalar quantization | ~15 GB | small (~1%), near zero with re-scoring |
| Product quantization (96 bytes) | ~1 GB | noticeable; needs re-ranking |
| Binary quantization (1 bit) | ~2 GB | large alone; good with 3–10× oversampling + re-scoring on high-dim models |
| Smaller embedding dims (Matryoshka, e.g. 1536 → 512) | ~20 GB | small for models trained for it (OpenAI v3, Nomic, etc.) |

### 4.3 Typical numbers (order of magnitude, single node, in-memory HNSW)

| Scale | Latency p95 | Throughput | Notes |
| --- | --- | --- | --- |
| 1M vectors, 768–1536 d | 1–5 ms | 1,000s QPS per node | Almost any engine, including pgvector, handles this well. |
| 10M–50M | 5–20 ms | 100s–1,000s QPS | RAM planning and quantization start to matter. |
| 100M–1B+ | 10–50 ms | Scale out or use GPU | Needs sharding (Milvus, Vespa, Elastic/OpenSearch), disk indexes (DiskANN) or quantization. |

These are rough guides at ~95% recall. Real numbers depend on hardware, filters and tuning, so benchmark on **your** data with tools like **VectorDBBench** (Zilliz) or **ann-benchmarks.com**. Treat vendor-published benchmarks with scepticism: each vendor tunes for its own engine.

### 4.4 Performance tips

- Start with **HNSW**; move to quantization or DiskANN only when RAM costs hurt.
- Normalise vectors and use **dot product**; it is the cheapest metric.
- Use **quantization + re-scoring** before adding more hardware.
- Keep hot data in the index and archive the rest (S3 Vectors, LanceDB on object storage).
- **Batch** inserts and queries; build the index *after* bulk loading where possible (pgvector, Milvus).
- Use **partitions / namespaces / tenants** (per customer, per language) so a query searches a smaller set.
- Tune `ef_search` / `nprobe` against a measured recall target, not by guessing.
- Use a **re-ranker** (cross-encoder such as Cohere Rerank, BGE-reranker, Azure semantic ranker) on the top 50–100 results; it usually improves answer quality more than a better ANN index.

---

## 5. Suitable Use Cases (with Examples)

| Use case | Example | Why vectors fit | Suggested stack |
| --- | --- | --- | --- |
| **RAG (Retrieval-Augmented Generation)** | Airline agent chatbot answers "Can I carry a power bank on EK flights?" from baggage policy PDFs. | Finds the relevant policy paragraph even when the wording differs. | Azure AI Search, pgvector, Qdrant, Weaviate |
| **Semantic search** | Staff search "passenger denied boarding compensation" and find the EU261 procedure titled "Involuntary offload handling". | Matches meaning, not keywords. | Elasticsearch/OpenSearch hybrid, Azure AI Search |
| **Recommendations** | Recommend ancillaries (lounge, extra legroom, Wi-Fi) similar to what comparable travellers bought. | User and product embeddings; nearest neighbours = likely purchases. | Milvus, Redis, Vespa, Pinecone |
| **Semantic caching for LLMs** | "What's the baggage allowance to London?" and "How much luggage can I take to LHR?" hit the same cached LLM answer. | Similar questions → same answer; saves LLM cost and latency. | Redis, Cosmos DB, Azure Managed Redis |
| **AI agent memory** | An agent remembers past customer interactions and preferences across sessions. | Recall relevant past conversations by similarity. | Cosmos DB, Postgres + pgvector, Redis |
| **Duplicate / near-duplicate detection** | Detect duplicate customer complaints or duplicated product listings. | Near-identical vectors = duplicates. | FAISS (batch), Milvus |
| **Image / multimodal search** | Find damaged-baggage photos similar to a new claim image; search a catalogue by photo. | CLIP-style embeddings put images and text in one space. | Milvus, LanceDB, Weaviate |
| **Anomaly / fraud detection** | Flag a booking whose behaviour embedding is far from any normal cluster. | Distance from normal = suspicious. | Milvus, OpenSearch |
| **Code / document classification & routing** | Route incoming emails to the right team by similarity to labelled examples. | kNN classification without training a model. | pgvector, Qdrant |
| **Entity resolution / MDM** | Match "Tamil S." and "T. Saravana" customer records across systems. | Fuzzy similarity on names/addresses. | pgvector, Elasticsearch |

### When **NOT** to use a vector database

- Exact lookups (`WHERE pnr = 'ABC123'`), aggregations and reporting: use SQL.
- Exact keyword/ID/code search (part numbers, error codes): keyword search wins; use **hybrid** if you need both.
- A few thousand vectors: an in-memory array or brute force in your existing DB is enough.
- Multi-hop relationship questions ("which suppliers of our delayed parts also supply our competitor?"): vectors retrieve similar text but not connections. Use a graph or **GraphRAG** (section 8).

---

## 6. Cost

### 6.1 Where the money goes

| Cost driver | Notes |
| --- | --- |
| **Embedding generation** | Paid per token for API models (one-off for indexing, plus every query). Re-embedding a large corpus when you change model can be a big bill. Open-source models on your own GPU/CPU avoid per-token costs. |
| **Memory (RAM)** | The main infrastructure cost for HNSW. Quantization and disk indexes cut it 4–30×. |
| **Storage** | Raw vectors + index + metadata + replicas. Object storage is ~10–50× cheaper than RAM. |
| **Compute / read & write units** | Serverless products bill per query and per write (Pinecone RUs/WUs, Cosmos DB RUs, S3 Vectors per query). |
| **Replicas / HA** | Production usually needs 2–3 replicas, multiplying cost. |
| **Operations** | Self-hosting is licence-free but needs people to run K8s, upgrades, backups and monitoring. |

### 6.2 Cost model by product type

| Option | Pricing model | Relative cost | Best for |
| --- | --- | --- | --- |
| **pgvector on existing Postgres** | You already pay for the DB | 💲 Lowest extra cost | Up to tens of millions of vectors in an existing Postgres estate |
| **Oracle 23ai / SQL Server 2025 / Cosmos DB / MongoDB** vectors | Included in the DB you already license | 💲 Low extra cost | Keeping vectors next to operational data |
| **Self-hosted OSS** (Milvus, Qdrant, Weaviate, OpenSearch) | Infrastructure only (VMs/K8s) | 💲💲 Low licence, higher ops | Large scale, data-sovereignty needs, strong platform team |
| **Serverless managed** (Pinecone, Zilliz serverless, Weaviate/Qdrant Cloud, Turbopuffer) | Storage + reads + writes | 💲💲 at small/spiky scale; 💲💲💲 at high steady QPS | Teams without ops capacity; variable traffic |
| **Dedicated managed clusters** (Azure AI Search, Elastic Cloud, OpenSearch Service, Vertex AI) | Per node / search unit per hour | 💲💲💲 | Enterprise search + RAG with SLAs and security integration |
| **Object-storage vector stores** (S3 Vectors, LanceDB on S3) | Storage (cheap) + per query | 💲 For large cold data | Archives, very large rarely-queried corpora |

### 6.3 Cost-saving checklist

- Use the **smallest embedding dimension** that meets quality (Matryoshka truncation, e.g. 3072 → 1024).
- Turn on **int8 or binary quantization** with re-scoring.
- Start with **pgvector or your existing database** before buying a new product.
- Use **serverless** for spiky or low traffic; **provisioned** for steady high QPS.
- Don't embed everything: skip boilerplate, deduplicate chunks, and set a sensible chunk size.
- Cache query embeddings and LLM answers (semantic cache).
- Tier data: hot index in RAM, cold vectors on object storage.

---

## 7. Things You Should Know

### 7.1 Hybrid search is the norm

Pure vector search misses exact terms (flight numbers, product codes, names). Production RAG usually combines **BM25 keyword + vector** results, merges them with **Reciprocal Rank Fusion (RRF)**, then applies a **re-ranker**. Most engines now support this natively.

### 7.2 Filtering is harder than it looks

"Nearest vectors **where** `region = 'EU'` **and** `valid_to > today`" is where engines differ most. Post-filtering can return fewer than K results; pre-filtering can fall back to brute force. Test with your real filters, especially very selective ones.

### 7.3 Security and multi-tenancy

- Apply **document-level security** (security trimming) so RAG never retrieves documents the user can't see. Store ACL/tenant IDs as metadata and filter on them.
- Embeddings can leak information (text can be partially reconstructed from them); protect them like the source data.
- Use namespaces/partitions/tenants for per-customer isolation.

### 7.4 Operations

- Plan **re-indexing** for model upgrades: run old and new indexes side by side, then switch.
- Track **recall, latency p95/p99, index freshness and RAM usage**.
- Back up the source text and metadata: you can always rebuild vectors, but not the other way round.
- Deletes in HNSW are often soft (tombstones); schedule compaction/optimisation.

### 7.5 Evaluation

Retrieval quality matters more than database choice. Build a small **golden set** (questions + expected documents) and measure **recall@K, MRR, nDCG**; for RAG, also measure answer faithfulness (e.g. RAGAS, Azure AI Foundry evaluations, DeepEval).

---

## 8. GraphRAG

### 8.1 What it is

**GraphRAG** = Retrieval-Augmented Generation that retrieves from a **knowledge graph** (entities and relationships), usually combined with vector search, instead of only retrieving similar text chunks.

| | Vector RAG (baseline) | GraphRAG |
| --- | --- | --- |
| **Retrieves** | Top-K text chunks similar to the question | Entities, their relationships, neighbourhood facts, community summaries + supporting chunks |
| **Good at** | "What does policy X say about Y?" (answer sits in one or two chunks) | **Multi-hop** ("Which suppliers of our delayed parts also supply our competitor?") and **global** questions ("What are the main themes in 5,000 complaints?") |
| **Weak at** | Connecting facts spread across many documents; summarising a whole corpus | Simple look-ups (overkill); fast-changing data (graph must be rebuilt/updated) |
| **Indexing cost** | Low (embedding only) | **High**: an LLM reads every chunk to extract entities and relations |
| **Explainability** | Shows source chunks | Shows the **path of facts** used (entity → relation → entity), easier to audit |
| **Storage** | Vector DB | Graph DB (Neo4j, Neptune, FalkorDB, Cosmos DB, Spanner) + vector index, or files (Parquet) |

See also [Graph Databases](../graph/README.MD) (section 4.6).

### 8.2 How it works

```text
INDEXING (offline, LLM-heavy)
Documents ──► Chunk ──► LLM extracts entities & relationships ──► Knowledge graph
                │                                                     │
                └──► Embed chunks & entities ──► Vector index         ├──► Community detection (Leiden)
                                                                      └──► LLM writes community summaries

QUERY (online)
Question ──► Vector search finds entry entities/chunks
         ──► Graph traversal expands to related entities (1–3 hops)
         ──► (Global questions: map-reduce over community summaries)
         ──► Build context (facts + chunks + summaries) ──► LLM answer with citations
```

**Two ways to build the graph:**

1. **LLM-extracted graph** from unstructured text (Microsoft GraphRAG, LightRAG, Neo4j LLM Graph Builder). Flexible, but costly and noisy; needs entity de-duplication.
2. **Existing structured graph** from your systems (customers, bookings, flights, suppliers) plus text linked to nodes. Cheaper and more accurate; often the better enterprise route.

### 8.3 Query modes (Microsoft GraphRAG terminology)

| Mode | What it does | Use for |
| --- | --- | --- |
| **Local search** | Finds entities related to the question, then pulls their neighbours, relationships and source chunks. | Questions about specific entities: "What did customer X complain about and which flights were involved?" |
| **Global search** | Map-reduce over community summaries across the whole dataset. | Corpus-wide questions: "What are the top themes in this year's complaints?" |
| **DRIFT search** | Starts global, then drills into local searches with follow-up questions. | Broad questions that also need specific detail. |
| **Basic search** | Plain vector RAG over chunks. | Baseline comparison. |

### 8.4 GraphRAG frameworks and products

| Framework / product | What it is | License / cost |
| --- | --- | --- |
| **Microsoft GraphRAG** | The reference implementation: LLM entity extraction, Leiden communities, local/global/DRIFT search. Outputs Parquet files; can load into a graph DB. | MIT (free). You pay LLM tokens, which can be significant for indexing. |
| **LazyGraphRAG** (Microsoft) | Defers LLM summarisation to query time; indexing cost similar to vector RAG. Available in Microsoft Discovery / Azure offerings. | Check availability. |
| **LightRAG** | Lightweight GraphRAG with dual-level (low/high) retrieval and incremental updates; much cheaper than full GraphRAG. | MIT (free) |
| **HippoRAG 2** | Graph + Personalized PageRank retrieval inspired by human memory; strong on multi-hop QA. | MIT (free) |
| **Neo4j GraphRAG (Python package) / LLM Graph Builder** | Build KGs from documents; retrievers combining vector index + Cypher traversal (`VectorCypherRetriever`, `Text2CypherRetriever`). | Apache 2.0 package; Neo4j Community free, Enterprise/Aura paid |
| **LlamaIndex PropertyGraphIndex** / **LangChain LLMGraphTransformer + LangGraph** | Framework building blocks for KG extraction and graph retrieval. | MIT (free) |
| **Amazon Bedrock Knowledge Bases GraphRAG** | Managed GraphRAG using Neptune Analytics. | Paid (AWS) |
| **FalkorDB GraphRAG SDK**, **Memgraph**, **Cognee**, **Graphiti (Zep)** | Graph-backed RAG and agent memory; Graphiti builds a temporal knowledge graph for agents. | Mostly open source; check each license |

### 8.5 Example: airline disruption analysis

**Question:** *"Which Gold-tier customers were affected by delays caused by the A350 engine issue, and which ancillaries did they buy?"*

- **Vector RAG** finds chunks mentioning "A350 engine issue" but can't reliably connect them to specific flights, then customers, then tier, then ancillary orders.
- **GraphRAG** uses vector search to find the `Issue` entity, then traverses the graph:

```cypher
// Neo4j: vector search to find entry nodes, then graph traversal
CALL db.index.vector.queryNodes('issue_embedding', 5, $questionEmbedding)
YIELD node AS issue, score
MATCH (issue)-[:CAUSED]->(d:Delay)-[:ON_FLIGHT]->(f:Flight)
      <-[:BOOKED_ON]-(o:Order)<-[:PLACED]-(c:Customer {tier: 'Gold'})
OPTIONAL MATCH (o)-[:INCLUDES]->(a:Ancillary)
RETURN c.name, f.flightNo, d.minutes, collect(DISTINCT a.type) AS ancillaries, score
ORDER BY score DESC, d.minutes DESC
LIMIT 50;
```

The result rows (plus the source incident report chunks) are passed to the LLM, which writes the answer with traceable facts.

### 8.6 When to use GraphRAG

| Use GraphRAG when | Stay with vector RAG (+ hybrid + re-ranker) when |
| --- | --- |
| Questions need **multi-hop** reasoning across entities | Answers usually live in one or two passages |
| You need **corpus-wide summaries** / themes | Documents are independent (FAQs, manuals, policies) |
| You already have **structured relationships** (CRM, bookings, supply chain, org charts) | Data changes constantly and re-extraction would be expensive |
| **Explainability / audit** of reasoning paths matters (finance, pharma, compliance) | Budget or latency is tight |

### 8.7 Watch out for

- **Indexing cost:** full LLM extraction over a large corpus can cost far more than embedding it. Try on a sample first, use a cheaper model for extraction, or use LazyGraphRAG/LightRAG.
- **Graph quality:** duplicate entities ("Emirates", "EK", "Emirates Airline") must be resolved, or traversals break. Define an **ontology/schema** (allowed entity and relation types) up front.
- **Freshness:** new documents require incremental extraction and community re-computation.
- **Latency:** traversal + summaries + larger context add time and tokens compared with plain RAG.
- **Evaluate:** compare GraphRAG against a strong hybrid-search baseline on your own questions; the baseline often wins on simple look-ups.

---

## 9. Summary

- **HNSW** is the default ANN algorithm; use **quantization** (int8/binary + re-scoring) or **DiskANN / IVF-PQ** when memory becomes the cost problem.
- **Start with what you have:** pgvector, Oracle 23ai, SQL Server 2025, Cosmos DB or MongoDB handle most workloads up to tens of millions of vectors.
- Move to a **purpose-built** engine (Milvus, Qdrant, Weaviate, Pinecone) for very large scale, heavy filtering or high QPS.
- Most popular open-source vector DBs are **free for commercial use** (Apache 2.0/BSD/MIT/PostgreSQL); you pay for infrastructure, embeddings and operations.
- **Hybrid search + re-ranking** matters more than the choice of database for RAG quality.
- **GraphRAG** adds relationships on top of vectors for multi-hop and corpus-wide questions, at a higher indexing cost. Use it when relationships matter; otherwise, well-tuned vector RAG is enough.
