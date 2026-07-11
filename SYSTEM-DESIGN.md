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
| `Session` | Carries identity, signal source metadata, policy context, execution state |
| `Task` | A single unit of work tracked through the agent loop |
| `PolicyRule` | Governance rule (allow/deny, scope, conditions) |

**Value Objects:**

| Value Object | Description |
|-------------|-------------|
| `SessionId` | Wraps a session identifier |
| `Identity` | Authenticated user/system identity with claims |
| `SignalSource` | Type + metadata about where the signal came from, including optional inline or referenced config overrides |
| `SessionConfig` | Resolved effective config for a session — merged from global defaults and per-signal overrides (model, policy refs, prompt, skills) |
| `ToolCall` | A tool invocation within an agent iteration |
| `PolicyDecision` | Result of a policy check (allow/deny with reason) |

**Repository Ports (interfaces):**

- `SessionRepository`
- `TaskRepository`
- `MemoryStore`
- `RegistryStore`
- `PolicyRuleRepository`
- `ConfigRepository` — stores global config, resolves per-signal overrides against defaults via `resolve(signalOverrides)`
- `SignalQueue` — enqueue, dequeue, ack, nack

**Domain Services:**

- `PolicyEngine` — evaluates whether a session+action is permitted
- `AuthService` — resolves identity from raw signal metadata
- `AgentStrategy` — orchestration pattern for the agent loop (single-agent, supervisor+worker, swarm, router)

### 3.2 Application Layer (`src/HaaS.Application/`)

Orchestrates domain objects to fulfill use cases.

**Engine & Orchestration:**

| Component | Responsibility |
|-----------|---------------|
| `IHaasEngine` | Entry point for the application. Starts all registered signal sources and coordinates the execution of signals. |
| `HaasEngine` | Implementation of `IHaasEngine`. Wires up `SignalSourceRegistration`s, ensures configurations are persisted, and manages the execution lifecycle of multiple concurrent signal sources. |
| `SignalSourceRegistration` | Links a `ISignalSource` with its corresponding `ISignalPresenter` and `SignalSourceConfig`. Maintains session continuity state (last known session ID). |

**Use Cases:**

| Use Case | Description |
|----------|-------------|
| `RunSessionUseCase` | Accepts an incoming signal and a presenter. Resolves session configuration (loads existing session or creates new one). Executes the agent strategy and presents the result. |
| `ResolveConfig` | Takes global config + signal overrides, returns resolved `SessionConfig` with merge logic |
| `ExecuteAgentIteration` | Single agent loop tick: think → call tool → observe |
| `ListSessions` | Queries active/completed sessions |
| `GetSessionLog` | Returns full observability trace for a session |
| `ConfigurePolicy` | CRUD for policy rules |

**Pattern:** Each use case is a class accepting domain ports via constructor injection, with async methods that throw on error.

### 3.3 Adapter Layer (`src/HaaS.Adapters/`)

Implements the ports defined in `domain/`. Swappable by configuration.

**Signal Sources (`SignalSource`):**

Signal adapters receive raw input and may include optional per-signal config overrides (inline or by reference) alongside the payload. Each source can optionally declare `allowSessionContinuation` in its config — when true, the adapter inspects the incoming signal for a `session_id` field and, if valid, loads the existing session rather than creating a new one.

- `HttpWebhookSignalSource` — Potential HTTP webhook adapter
- `SlackSignalSource` — Potential Slack Events API adapter
- `KafkaSignalSource` — Potential Kafka consumer
- `CliStdinSignalSource` — Potential CLI stdin adapter
- `ScheduledPollerSignalSource` — Potential cron-based poller adapter

**Observability Providers (`ObservabilityProvider`):**

- `ConsoleObservabilityProvider` — structured JSON logs to stdout
- `OpenTelemetryObservabilityProvider` — OTLP export
- `DataDogObservabilityProvider` — DogStatsD / API
- `CloudWatchObservabilityProvider` — CloudWatch Logs

**Auth Providers (`AuthProvider`):**

- `JwtAuthProvider` — validates JWT tokens
- `OAuth2AuthProvider` — OAuth2 token exchange
- `ApiKeyAuthProvider` — API key lookup
- `MtlsAuthProvider` — mTLS certificate validation
- `PassthroughAuthProvider` — dev/no-auth mode

**Repository Implementations:**

Each port is backed by an adapter that chooses its own storage topology. The port abstraction in the domain layer makes the topology invisible to use cases — both per-session and shared adapters implement the same port interface.

| Port | Adapter | Topology |
|------|---------|----------|
| `SessionRepository` | `SharedSqliteSessionRepository` | Shared `sessions.db` — sessions are metadata, few enough for a single table |
| `TaskRepository` | `PerSessionSqliteTaskRepository` | `sessions/<session_id>.db` — one file per session, contains `tasks`, `memory`, and `agent_iterations` tables |
| `MemoryStore` | `PerSessionSqliteMemoryStore` | `sessions/<session_id>.db` — shares the same per-session file as tasks and observability |
| `RegistryStore` | `SharedSqliteRegistryStore` | Shared `registry.db` — global KV store (skill definitions, tool manifests), read-mostly |
| `SignalQueueStore` | `SharedSqliteSignalQueueStore` | Shared `signal_queue.db` — atomic dequeue ordering across all sessions |
| `PolicyRuleRepository` | `SharedSqlitePolicyRuleRepository` | Shared `policies.db` — global policy rules, read-mostly |
| `ConfigRepository` | `SharedSqliteConfigRepository` | Shared `config.db` — key-value config, seeded from YAML at bootstrap, settled in SQLite for runtime reads |

No cross-DB joins are needed because each port serves a distinct purpose — write paths are naturally separated. The only coordination is at the worker level: dequeue a signal (signal queue) → load session (sessions) → execute iteration (tasks + memory per session) → record trace (observability). Each step touches different files.

### 3.4 Infrastructure Layer (`src/HaaS.Infrastructure/`)

Wires everything together. Configuration, dependency injection, SQLite setup, and host bootstrap.

**Key Components:**

- `Program.cs` — Application entry point and DI container wiring
- `appsettings.json` — Configuration file, resolved via `Microsoft.Extensions.Configuration`
- `DatabaseContext.cs` — Manages multiple SQLite DB connections per the adapter topology (shared DBs + per-session DBs)
- `LoggingConfiguration.cs` — structured logging and telemetry setup

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
1. Signal arrives, optionally with per-signal config overrides and/or a session_id
2. AuthProvider resolves identity from signal metadata
3. Source enqueues signal (payload + identity + metadata) to SignalQueue

— Queue worker (after dequeue) —
4. ResolveConfig merges global defaults with signal-level overrides → resolved SessionConfig
5. If signal carries a session_id and the source permits continuation, SessionManager loads the existing session with new input appended. Otherwise, creates a new session with resolved config.
6. PolicyEngine checks: is this source+identity allowed to start or continue this session?
7. PolicyEngine resolves the set of tools permitted for this session.
8. Agent loop begins (Microsoft Agent Framework) with tools pre-filtered by policy and resolved model/prompts/skills:
   a. Agent thinks → calls a tool
   b. Execute tool (reads/writes Knowledge stores)
   c. ObservabilityProvider records the iteration
   d. Repeat until agent produces final output
   > See `IMPLEMENTATION-CONSIDERATIONS.md` for how session auth is injected into custom tool calls via the `tool_call` extension event.
9. Agent produces output via tools (e.g., a `reply_to_user` tool writes the response)
11. Session is closed; full trace is persisted
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
Created ──→ Authenticated ──→ Authorized ──→ Running ──→ Completed
                  ↑                               │
                  └────── Continued ───────────────┘
                                                  │
                                                  ├──→ Failed
                                                  └──→ Cancelled
```

- **Created:** Raw signal received but not yet authenticated
- **Authenticated:** Identity resolved
- **Authorized:** Policy engine approved session start
- **Running:** Agent loop is active
- **Continued:** New signal arrived with a valid `session_id` for this session. Session re-enters Running with appended input.
- **Completed/Failed/Cancelled:** Terminal states

## 8. Governance Model

Policy rules are evaluated at three gates:

| Gate | When | What is checked |
|------|------|----------------|
| Session Start | After auth, before loop | Is this identity+source allowed to start a session? |
| Tool Resolution | After session start, before loop | What tools is this session permitted to use? Only those tools are handed to the agent. |


> Custom tools that call external services receive session auth via the `tool_call` extension hook. See `IMPLEMENTATION-CONSIDERATIONS.md`.

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
  id TEXT PRIMARY KEY,
  source_type TEXT NOT NULL,
  source_metadata_json TEXT,
  identity_json TEXT NOT NULL,
  status TEXT NOT NULL,
  session_config_json TEXT,         -- resolved per-signal overrides (model, policies, prompt, skills)
  input_payload_json TEXT,          -- original signal payload
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  completed_at TEXT
```

### Shared: `signal_queue.db`

```
signal_queue:
  id TEXT PRIMARY KEY,
  session_id TEXT,                               -- set when dequeued: links to sessions(id), not enforced as FK
  source_type TEXT NOT NULL,
  source_metadata_json TEXT,
  identity_json TEXT,
  payload_json TEXT,
  status TEXT NOT NULL DEFAULT 'pending',         -- pending, processing, completed, failed
  created_at TEXT NOT NULL,
  picked_at TEXT,                                 -- when a worker picked it up
  completed_at TEXT,
  retry_count INTEGER NOT NULL DEFAULT 0,
  max_retries INTEGER NOT NULL DEFAULT 3
```

### Shared: `registry.db`

```
registries:
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL,
  ttl_seconds INTEGER
```

### Shared: `config.db`

```
config:
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL
```

### Shared: `policies.db`

```
policy_rules:
  id TEXT PRIMARY KEY,
  priority INTEGER NOT NULL,
  effect TEXT NOT NULL,
  conditions_json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
```

### Per-session: `sessions/<session_id>.db`

```
tasks:
  id TEXT PRIMARY KEY,
  status TEXT NOT NULL,
  input_json TEXT,
  output_json TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL

memory:
  key TEXT NOT NULL,
  value_json TEXT NOT NULL,
  PRIMARY KEY (key)

agent_iterations:
  id TEXT PRIMARY KEY,
  iteration_number INTEGER NOT NULL,
  phase TEXT NOT NULL,
  input_json TEXT,
  output_json TEXT,
  timestamp TEXT NOT NULL
```

`session_id` is implicit — derived from the file name. No FK column needed. The three tables live in the same file to keep per-session state self-contained — SQLite with WAL mode handles concurrent table writes without contention. The `ObservabilityProvider` adapter decides the storage format for traces (SQLite for queryability, JSONL for append-only streaming); the domain port is the same.

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

## 13. Supplemental: Bidirectional (Request-Response) Flow

Some signal sources expect a response — HTTP returns 200 with a body, Slack replies in a thread, CLI prints to stdout. Others are fire-and-forget (Kafka, scheduled poller).

Each signal source can define its own mechanism for delivering these responses, typically by implementing a corresponding tool that the agent calls to produce output.

### How it works

The `ReplyTool` property on `SignalSourceConfig` names the tool the agent must call to respond. The `SignalSourceConfigRepository` stores this per source type. When the agent strategy loads a session, it checks `config.ReplyTool`:

- If set → `chatOptions.ToolMode = ChatToolMode.RequireSpecific(config.ReplyTool)` — the LLM is required to call that tool
- If null → `chatOptions.ToolMode = ChatToolMode.Auto` — the LLM can use tools freely or respond with plain text

```
Signal arrives ──→ Auth/Session/Policy ──→ Agent Loop ──→ reply tool writes output

     │                                                    │
     │  (bidirectional sources:                           │
     │   HTTP, Slack, CLI)                                │
     │                                                    ▼
     └─────────────────── session_id ────────────→ CLI/HTTP/Slack waits
```

### What changes

- **`adapter/` only** — Reply tools are registered in `IToolRegistry` alongside domain tools. Their handlers write to the output destination (console, HTTP response, Slack API). The agent strategy creates the `ChatOptions` with the appropriate `ToolMode`.
- **`domain/`** — `SignalSourceConfig`, `AgentSessionConfig`, and `SessionRecord` gain an optional `ReplyTool` string field.
- **`application/`** — `RunSessionUseCase` passes `ReplyTool` from config through to the session record. No separate output dispatch is needed.
- **`infra/`** — Optional: response timeout wiring for HTTP to avoid dangling connections if the agent loop stalls.

## 14. Supplemental: Signal Queuing and Concurrent Processing

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
