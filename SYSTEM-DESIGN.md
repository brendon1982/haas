# System Design — Enterprise AI Harness (HaaS)

## 1. Problem Statement

Enterprise customers need an on-premise AI orchestration platform that:

- Accepts signals from diverse input sources (webhooks, Slack, Kafka, CLI, etc.)
- Routes them through a governed, observable agent loop
- Routes output through tools configured for the session
- Enforces per-session auth, governance, and audit trails
- Runs fully on-premises with zero external dependencies
- Is extensible at every seam without modifying core code

## 2. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        Signal Sources                            │
│           (Consumer Implemented: HTTP, Slack, CLI, etc.)         │
└─────────────────────────┬────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Auth Layer (AuthProvider)                      │
│  Authenticate & resolve identity from signal metadata             │
└─────────────────────────┬────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                      Signal Queue                                 │
│  Buffers incoming signals; decouples ingestion from processing   │
└─────────────────────────┬────────────────────────────────────────┘
                          │
                          ▼  (dequeue by worker pool)
┌──────────────────────────────────────────────────────────────────┐
│                       Session Manager                            │
│  Create/load session (identity + signal source + policy context) │
└─────────────────────────┬────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                   Governance Layer (PolicyEngine)                 │
│  Resolve permitted tools & targets for this session              │
└─────────────────────────┬────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Agent Loop (Microsoft Agent Framework)         │
│  Iterates: think → tool_call → observe → decide                 │
│  Runs with tools pre-filtered by policy; observability per iteration │
└──────────────────────────────────────────────────────────────────┘
   │                    │                      │
   ▼                    ▼                      ▼
┌────────┐     ┌──────────────┐     ┌──────────────────┐
│ Tools  │     │ Knowledge    │     │ Observability    │
│ (ext)  │     │ Stores       │     │ Providers        │
└───┬────┘     │ (per-session │     │ (OpenTelemetry,  │
    │          │  + shared    │     │  stdout, DD, CW) │
    │          │  DB files)   │     └──────────────────┘
    │          └──────────────┘
    ▼
(output written by tool handler)
```

## 3. Layer Definitions

### 3.1 Domain Layer (`src/HaaS.Domain/`)

The heart of the system. Contains no infrastructure concerns.

**Aggregates & Entities:**

| Aggregate | Responsibility |
|-----------|---------------|
| `SessionRecord` | Carries session metadata, configuration (provider, model, prompt), and terminal output |
| `QueuedSignal` | A signal waiting in or being processed from the queue, including its identity and retry state |

**Value Objects:**

| Value Object | Description |
|-------------|-------------|
| `Identity` | Authenticated user/system identity with claims |
| `Signal` | Payload + source information about an incoming request |
| `AgentSessionConfig` | Configuration for an agent session (provider, model, prompt, tools) |
| `SignalSourceConfig` | Configuration template for a specific signal source type |
| `DomainMessage` | A single message in a session's chat history |

**Repository Ports (interfaces):**

- `ISessionRepository` — CRUD for session metadata
- `IMessageStore` — Storage for session chat history (messages)
- `ISignalQueue` — Enqueue, dequeue, ack, nack for signals
- `ISignalSourceConfigRepository` — Stores and retrieves configuration for signal sources
- `IProviderConfigRepository` — Stores configuration for AI providers
- `IDeferredSessionResultStore` — Holds results for asynchronous signal processing

**Domain Services:**

- `IAgentStrategy` — Orchestration pattern for the agent loop
- `IToolProvider` — Resolves tool definitions and instances

### 3.2 Application Layer (`src/HaaS.Application/`)

Orchestrates domain objects to fulfill use cases.

**Engine & Orchestration:**

| Component | Responsibility |
|-----------|---------------|
| `ISignalSourceRegistry` | Registry for all active signal sources and their presenters. |
| `SignalSourceRegistry` | Implementation of `ISignalSourceRegistry`. |
| `SignalWorker` | Background worker that dequeues signals and executes the agent loop. |
| `SignalSourceRegistration` | Links a `ISignalSource` with its corresponding `ISignalPresenter` and metadata. |

**Use Cases:**

| Use Case | Description |
|----------|-------------|
| `RunSessionUseCase` | Executes the agent loop for a given signal. Resolves config, manages history, and presents results. |
| `EnqueueSignalUseCase` | Accepts an incoming signal, resolves its identity (currently defaults to Anonymous), and enqueues it. |

**Pattern:** Each use case is a class accepting domain ports via constructor injection, with async methods that throw on error.

### 3.3 Adapter Layer (`src/HaaS.Adapters/`)

Implements the ports defined in `domain/`. Swappable by configuration.

**Signal Sources (`SignalSource`):**

Signal adapters receive raw input and interact with the system via the `IHaasEngine`. Each source is registered in the `ISignalSourceRegistry`.

- `CliSignalSource` — CLI stdin/stdout adapter
- `TicTacToeSignalSource` — Example game signal source

**Observability Providers:**

Observability is handled via decorators and specialized implementations of ports.

- `ConsoleLogger` — Implements `ILogger`, writes to stdout
- `ObservableRunSessionUseCase` — Decorator that adds logging to session execution
- `ObservableAgentStrategy` — Decorator that adds logging to agent strategy
- `ObservableHaasEngine` — Decorator that adds logging to engine lifecycle

**Agent Strategy Implementations:**

- `MicrosoftAgentFrameworkStrategy` — Implementation using Microsoft Agent Framework.

**Repository Implementations:**

Each port is backed by an adapter that chooses its own storage topology. 

| Port | Adapter | Topology |
|------|---------|----------|
| `ISessionRepository` | `SharedSqliteSessionRepository` | Shared `sessions.db` — contains session metadata |
| `IMessageStore` | `PerSessionSqliteMessageStore` | `sessions/<session_id>.db` — one file per session, contains `messages` table |
| `ISignalQueue` | `SharedSqliteSignalQueueStore` | Shared `signal_queue.db` — atomic signal queuing |
| `ISignalSourceConfigRepository` | `SharedSqliteSignalSourceConfigRepository` | Shared `config.db` — signal source configurations |
| `IProviderConfigRepository` | `SharedSqliteProviderConfigRepository` | Shared `config.db` — AI provider configurations |

InMemory adapters (e.g., `InMemorySignalQueue`, `InMemorySessionRepository`) are also available for testing and development.

### 3.4 Infrastructure Layer (`src/HaaS.Infrastructure/`)

Wires everything together. Configuration, dependency injection, SQLite setup, and host bootstrap.

**Key Components:**

- `HaasBuilder` — Fluent API for configuring HaaS services and signal sources.
- `ServiceCollectionExtensions` — DI container wiring for all HaaS components.
- `BaseHaasEngine` — Base class for engine implementations.
- `DirectHaasEngine` — Executes signals immediately upon receipt.
- `QueuedHaasEngine` — Enqueues signals and processes them via a worker pool.
- `CompositeHaasEngine` — Orchestrates multiple engine instances.
- `HaasSqliteExtensions` — SQLite-specific DI wiring and repository setup.
- `SignalScopeAccessor` — Manages `AsyncLocal` storage of the current service provider scope.

## 4. Extension Points

Every extension point is a **port** (interface) in `HaaS.Domain`. Adapters live in `HaaS.Adapters` and are selected via configuration.

```csharp
// Example port definition
public interface ISignalSource
{
    string Type { get; }
    Task ListenAsync(Func<Signal, Task> handler);
    Task ShutdownAsync();
}
```

To add a new signal source: implement `SignalSource` → register in config. No core code changes.

## 5. Data Flow (End-to-End)

```
— At ingress (signal source adapter) —
1. Signal arrives via a source (e.g., CLI).
2. Signal is enqueued to the SignalQueue (or processed directly by DirectHaasEngine).

— Queue worker (after dequeue) —
3. SignalWorker dequeues the signal.
4. RunSessionUseCase is invoked for the signal.
5. Session configuration is resolved from repositories.
6. Agent loop begins (Microsoft Agent Framework) using MicrosoftAgentFrameworkStrategy.
   a. Agent thinks → calls a tool.
   b. Execute tool via ToolProvider.
   c. Agent state and messages are persisted to per-session SQLite.
   d. Repeat until agent produces final output.
7. Output is presented back to the signal source via ISignalPresenter.
8. Signal is acknowledged in the queue.
```

## 6. Multi-Agent Strategies

The `AgentStrategy` port allows different orchestration patterns:

| Strategy | Description |
|----------|-------------|
| `SingleAgent` | One agent handles the full session |
| `SupervisorWorker` | Supervisor delegates subtasks to worker agents |
| `Swarm` | Multiple agents collaborate on the same session |
| `Router` | Incoming signal is classified and routed to specialized agents |

Each strategy wraps the Microsoft Agent Framework loop with additional coordination logic.

## 7. Session Lifecycle

```
Created ──→ Running ──→ Completed
               │
               ├──→ Failed
               └──→ Cancelled
```

- **Created:** Initial state for a new session record.
- **Running:** Agent loop is active.
- **Completed/Failed/Cancelled:** Terminal states.

## 8. Governance Model

Governance is primarily handled via `SignalSourceConfig`, which defines the tools available to a session.

Policy rules are stored in SQLite and support:

- **Allow/deny lists** — specific tools or sources
- **RBAC** — role-based (admin, operator, viewer)
- **ABAC** — attribute-based (time of day, signal source type, identity claims)
- **LLM-gated** — ask the LLM to evaluate (experimental)

## 9. Observability

Observability is a core pillar. The system leverages the **Microsoft Agent Framework's** built-in telemetry for agent-level metrics and tracing, while HaaS provides custom **logging and telemetry** for session lifecycle, governance decisions, and adapter-specific events.

System logging (app-level lifecycle, errors, warnings) and metrics (aggregated counters, gauges, histograms) are handled via `ILogger` and `ActivitySource`. Every concern is behind a port, allowing for stdout, OpenTelemetry, or other providers without domain changes.

Every significant agent action produces telemetry spans and log entries:

- **Session Lifecycle:** Start, completion, and error states.
- **Governance:** Policy evaluation results (allow/deny).
- **Execution:** Agent iteration steps and tool invocations.

Observability providers receive these events via standard .NET diagnostic listeners.

## 10. Configuration Model

Configuration is accessed through a `ConfigRepository` port. The `SharedSqliteConfigRepository` adapter stores config in a shared SQLite `config` table, seeded from a YAML bootstrap file on first run and mutable at runtime via `ConfigurePolicy` and similar use cases.

Because every store is behind a domain port, no adapter has a fixed topology — the same `ConfigRepository` interface could be backed by YAML, a single SQLite file, or a Postgres table without touching application or domain code.

Signals can carry optional per-signal overrides (inline or by reference) for model, policies, prompt, and skills. `ConfigRepository.resolve()` merges these against global defaults before the session starts — signal values override global, unspecified fields fall through.

```yaml
signal:
  type: slack
  config:
    token: ${SLACK_TOKEN}
    signing_secret: ${SLACK_SIGNING_SECRET}
    allow_session_continuation: true  # inspect signals for session_id to resume existing sessions

auth:
  type: jwt
  config:
    jwks_url: https://example.com/.well-known/jwks.json

observability:
  - type: opentelemetry
    config:
      endpoint: http://otel-collector:4318
  - type: console

policies:
  - effect: deny
    source_types: [slack]
    tools: [db_write, deploy]
  - effect: allow
    roles: [admin]
    tools: ["*"]
```

## 11. Database Schema (SQLite per DB File)

Tables are organized into separate SQLite files matching the adapter topology from Section 3.3. Cross-DB foreign keys are not enforced — referential integrity is maintained by the application layer.

### Shared: `sessions.db`

```
sessions:
  SessionId TEXT PRIMARY KEY,
  SourceType TEXT NOT NULL,
  Status TEXT NOT NULL,
  Provider TEXT NOT NULL,
  ModelId TEXT NOT NULL,
  SystemPrompt TEXT NOT NULL,
  Tools TEXT NOT NULL,
  ThinkingLevel TEXT NOT NULL,
  Output TEXT,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL
```

### Shared: `signal_queue.db`

```
signal_queue:
  id TEXT PRIMARY KEY,
  session_id TEXT,                               -- links to sessions(SessionId), not enforced as FK
  source_type TEXT NOT NULL,
  identity_json TEXT,
  payload_json TEXT,
  status TEXT NOT NULL DEFAULT 'pending',         -- pending, processing, completed, failed
  created_at TEXT NOT NULL,
  picked_at TEXT,                                 -- when a worker picked it up
  completed_at TEXT,
  retry_count INTEGER NOT NULL DEFAULT 0,
  max_retries INTEGER NOT NULL DEFAULT 3
```

### Shared: `config.db`

Shared SQLite database for configurations.

```
signal_source_configs:
  SourceType TEXT PRIMARY KEY,
  Provider TEXT NOT NULL,
  ModelId TEXT NOT NULL,
  SystemPrompt TEXT NOT NULL,
  ToolBelt TEXT NOT NULL,
  ThinkingLevel TEXT NOT NULL

provider_configs:
  Provider TEXT PRIMARY KEY,
  Endpoint TEXT NOT NULL,
  ApiKey TEXT
```

### Per-session: `sessions/<session_id>.db`

```
messages:
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Role TEXT NOT NULL,
  Content TEXT NOT NULL,
  Timestamp TEXT NOT NULL,
  Payload TEXT
```

`session_id` is implicit — derived from the file name. The `messages` table stores the conversation history for the agent.

## 12. Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| **Ports in domain, adapters in adapter** | Pure DDD — domain has zero infrastructure dependencies |
| **Exceptions for control flow** | Throw on error, let the caller catch. Simple, familiar, no wrapper types. |
| **SQLite with WAL mode** | Zero-ops, ACID, good enough for single-tenant on-prem. WAL allows concurrent reads during writes. |
| **No barrel files** | Prevents circular dependencies, keeps import graph explicit |
| **Microsoft Agent Framework wraps the loop** | We don't reinvent agent orchestration. Governance resolves permitted tools before the loop; observability wraps the process. |
| **Auth flows through** | Identity is resolved once at signal ingress and carried in the Session object — never re-authenticated unless policy demands it. Session auth reaches custom tools via the `tool_call` extension hook (see `IMPLEMENTATION-CONSIDERATIONS.md`).
| **Per-session governance** | Policy resolves the tool set per session based on identity + source metadata. The agent only sees tools it's allowed to use — no per-call gate needed. |
| **Per-signal config resolution** | Each signal carries optional overrides (inline or by reference) for model, policies, prompt, skills. `ConfigRepository.resolve()` merges these against global defaults before the session starts. Keeps global config simple while enabling fine-grained per-signal tuning without ad-hoc env vars. |
| **Session continuation opt-in** | Only signal sources that declare `allowSessionContinuation` inspect incoming signals for a `session_id`. This avoids accidental hijacking — a Kafka consumer or public webhook will never load an arbitrary session. Continuation appends new input to the agent loop context, enabling multi-turn interactions across signals. |
| **Config via repository port** | `ConfigRepository` in domain decouples config source from consumers. Starts with a YAML file adapter; can swap to SQLite or any store later without touching application code. |
| **Native C# Schema Validation** | Used for tool parameter schemas, domain record validation, and configuration shape. Leverages native language features and attributes for consistency. |
| **Builders and fakes over mocks** | State-based tests are more resilient to refactoring than interaction-based mocks |
| **Federated storage via ports** | Each store port in domain resolves to a different adapter topology: shared DBs for global state (queue, registry, config), and a single per-session DB per session for hot-path writes (tasks, memory, agent iterations). The domain layer knows nothing about this — the adapter layer decides. This prevents any single SQLite file from becoming a bottleneck. Cross-DB joins are never needed because each port addresses a separate concern. |

## 13. Supplemental: Signal Queuing and Concurrent Processing

Enterprise signal volumes can exceed the throughput of a single sequential agent loop. The agent loop is I/O-bound (LLM API calls, DB reads/writes), so the .NET runtime can handle many concurrent sessions via async tasks. However, without a queue, bursts of signals either overload the system or get dropped.

### Queue architecture

Signals are not processed inline. Instead they land in a `signal_queue` table, and a configurable worker pool dequeues them:

```
Signal arrives ──→ Auth ──→ signal_queue ──→ dequeue ──→ Session + Policy ──→ Agent Loop ──→ Output
                                    │                                            ↑
                                    │                                     Worker pool
                                    │                                 (N concurrent sessions)
                                    └──────────────────────────────────────────┘
```

**Signal sources authenticate at ingress** and enqueue the signal with the resolved identity. Sources return immediately after enqueuing. The response channel for bidirectional sources waits on the session completing or a timeout.

This works because the queue decouples *processing capacity* from *ingestion*, but response delivery still happens inline — the signal source adapter holds an in-memory promise that the worker resolves when the session finishes. `maxConcurrentSessions` limits how many connections can be *in-flight*, not how many signals can be queued.

Per-source response behaviour:
- **HTTP** — Handler enqueues, then `await`s a deferred Task completion. The host holds the connection open (async I/O, not blocking). If the queue is full, the handler rejects with 503 *before* enqueuing — no dangling connection.
- **Slack** — Slack's Events API requires a 200 OK within 3 seconds just to acknowledge receipt. Handler enqueues, returns 200 immediately. Worker posts the reply to Slack's `response_url` later.
- **CLI** — Holds stdin/stdout open awaiting a deferred response (same as HTTP), or prints a session ID for polling.

### Queue table

The `signal_queue` table lives in its own DB (`signal_queue.db`). See Section 11 for the canonical schema.

### Worker pool

- **Configurable concurrency** — `maxConcurrentSessions` in config controls how many sessions run simultaneously.
- **Async I/O concurrency** — Within the .NET process, the engine can manage many sessions because the loop is almost entirely I/O (LLM HTTP calls).
- **Polling** — Workers poll `signal_queue` for `pending` rows, atomically transition to `processing` via `UPDATE ... WHERE status = 'pending' LIMIT 1` (SQLite serializes writes, so no race).
- **Pick timeout** — If a worker crashes, sessions stuck in `processing` with a stale `picked_at` are retried after a grace period.

### Multi-process scaling (future)

When the host process isn't enough:

1. **Multiple instances, shared SQLite** — WAL mode allows concurrent readers. Workers in separate processes poll the same queue. SQLite write contention becomes the bottleneck under high load.
2. **Dedicated queue** — Swap the SQLite-backed queue for a distributed queue like RabbitMQ or Azure Service Bus. The `SignalQueue` port abstracts this.
3. **Stateless workers** — Workers hold no in-memory state. They can be scaled horizontally behind a proper queue without changes to domain or application layers.

### Backpressure

- Signal source adapters check queue depth before enqueuing.
- Configurable `maxQueueDepth` — if exceeded, sources either reject (HTTP 503) or signal intent to drop.
- Persistent high depth triggers an alert through the observability provider.

### What changes

- **`HaaS.Domain`** — New `ISignalQueue` port: `EnqueueAsync`, `DequeueAsync`, `AckAsync`, `NackAsync`.
- **`HaaS.Adapters`** — SQLite-backed adapter using the table above.
- **`HaaS.Application`** — New use case: polls the queue, orchestrates the full ReceiveSignal → ExecuteAgentIteration flow per item.
- **`HaaS.Infrastructure`** — Worker pool startup in the DI wiring. Config keys for `maxConcurrentSessions`, `maxQueueDepth`, `pickTimeoutMs`.
- **Signal sources** — No longer create sessions directly. They enqueue to `SignalQueue` and return immediately (or hold the response channel for bidirectional flows).

## 15. Signal Execution Lifecycle

To ensure proper isolation and resource management, every signal processing run occurs within its own dependency injection (DI) scope. This is analogous to how web frameworks handle HTTP requests.

### 15.1 Scope Management

- **Scope Creation**: The engines (`DirectHaasEngine` and `QueuedHaasEngine`) are responsible for creating a new `IServiceScope` for each signal.
- **Scope Access**: Singleton services, such as `ToolProvider`, can access the current scoped service provider via the `ISignalScopeAccessor`. This accessor uses `AsyncLocal<IServiceProvider>` to maintain the reference to the provider for the current execution context.
- **Automatic Resolution**: Tools registered via the generic `Register<T>` method are resolved from the current scope's service provider at the moment of execution.

### 15.2 Service Lifetimes

Components are registered with specific lifetimes to support this lifecycle:

| Component | Lifetime | Rationale |
|-----------|----------|-----------|
| `IHaasEngine` | Singleton | Manages global execution and background workers. |
| `IToolProvider` | Singleton | Central registry for tools across all sessions. |
| `ISignalScopeAccessor` | Singleton | Provides access to the current `AsyncLocal` scope. |
| `IRunSessionUseCase` | Scoped | Ensures a fresh execution context (identity, config) per signal. |
| `IAgentStrategy` | Scoped | Maintains agent loop state within a single signal run. |
| `SignalWorker` | Transient | Created per dequeue operation within a new scope. |

### 15.3 Execution Flow

1. **Engine** receives or dequeues a signal.
2. **Engine** creates a new `IServiceScope`.
3. **Engine** sets `ISignalScopeAccessor.ServiceProvider` to the scope's provider.
4. **Engine** resolves `IRunSessionUseCase` (or `SignalWorker`) from the scope.
5. **Use Case** executes the agent loop.
6. **ToolProvider** (when a tool is called) resolves the tool instance from `ISignalScopeAccessor.ServiceProvider`.
7. **Engine** disposes the scope and clears `ISignalScopeAccessor.ServiceProvider`.
