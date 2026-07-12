---
sessionId: session-260712-163247-1t5l
---

# Vertical Slice Options

### Overview
Based on the current implementation state (core agent loop with in-memory storage) and the requirements in `SYSTEM-DESIGN.md`, two foundational structural gaps exist. Closing either of these would constitute a high-value vertical slice.

### Option 1: SQLite Persistence (Recommended)
This slice replaces the volatile in-memory repositories with the persistent SQLite topology defined in the system design.

#### Technical Design
- **Topology:** Uses a federated approach with shared databases for global state and per-session databases for message history.
  - `sessions.db` (Shared): Session metadata and status.
  - `config.db` (Shared): Global defaults and signal-specific overrides.
  - `sessions/{sessionId}.db` (Per-Session): Message history and agent iterations.
- **Key Components:**
  - `SharedSqliteSessionRepository`: Implements `ISessionRepository`.
  - `SharedSqliteSignalSourceConfigRepository`: Implements `ISignalSourceConfigRepository`.
  - `PerSessionSqliteMessageStore`: Implements `IMessageStore` (or the `MemoryStore` port from design).
- **Rationale:** Foundation for session continuation (multi-turn), audit logs, and production-grade stability.

### Option 2: Signal Queuing & Worker Pool
This slice implements the `SignalQueue` to decouple signal ingestion from execution, moving away from the current direct-call model.

#### Technical Design
- **New Port:** `ISignalQueue` in the Domain layer (`EnqueueAsync`, `DequeueAsync`).
- **Adapter:** `SqliteSignalQueueStore` using a shared `signal_queue.db`.
- **Orchestration:**
  - `HaasEngine` enqueues incoming signals to the queue.
  - A `QueueWorker` (implemented as a `BackgroundService`) polls the queue and executes `RunSessionUseCase`.
- **Rationale:** Enables the system to handle signal bursts gracefully, provides a mechanism for retries, and allows for controlled concurrency (e.g., limiting the number of active LLM sessions).

### Recommendation
I recommend **Option 1: SQLite Persistence** as the next slice. It is a strict prerequisite for "Session Continuation" (a key feature in the design) and establishes the data management patterns that other layers (like Auth and Governance) will rely on for persistence.

# Delivery Steps

### ✓ Step 1: Setup SQLite Infrastructure & Shared Repositories
Establish the infrastructure for SQLite storage and implement the shared repositories.

- Add `Microsoft.Data.Sqlite` to `HaaS.Adapters` and `HaaS.Infrastructure`.
- Implement `SharedSqliteSessionRepository` in `HaaS.Adapters` targeting `sessions.db`.
- Implement `SharedSqliteConfigRepository` in `HaaS.Adapters` targeting `config.db`.
- Add table creation logic to ensure the schema is applied on startup.

### ✓ Step 2: Implement Per-Session Message Store
Implement the per-session message storage as defined in the system topology.

- Implement `PerSessionSqliteMessageStore` in `HaaS.Adapters`.
- This store must dynamically resolve database file paths (e.g., `sessions/{sessionId}.db`) based on the session ID.
- Ensure the session-specific directory exists before creating the database file.

### ✓ Step 3: Integration and Persistence Verification
Wire the new persistent adapters into the application and verify the end-to-end flow.

- Update `ServiceCollectionExtensions` in `HaaS.Infrastructure` to register the SQLite adapters instead of in-memory ones.
- Configure default database paths in `appsettings.json` or via `HaasBuilder`.
- Verify that session state and message history persist across application restarts using the CLI host.