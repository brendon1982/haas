# System Design — Enterprise AI Harness (HaaS)

## 1. Problem Statement

Enterprise customers need an on-premise AI orchestration platform that:

- Accepts signals from diverse input sources (webhooks, Slack, Kafka, CLI, etc.)
- Routes them through a governed, observable agent loop
- Produces outputs to configurable execution targets (Slack, Jira, email, PagerDuty, etc.)
- Enforces per-session auth, governance, and audit trails
- Runs fully on-premises with zero external dependencies
- Is extensible at every seam without modifying core code

## 2. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        Signal Sources                            │
│  (HTTP Webhook, Slack, Kafka, CLI, Poller, ...)                  │
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
│                    Agent Loop (pi-coding-agent)                   │
│  Iterates: think → tool_call → observe → decide                 │
│  Runs with tools pre-filtered by policy; observability per iteration │
└──┬────────────────────┬──────────────────────┬───────────────────┘
   │                    │                      │
   ▼                    ▼                      ▼
┌────────┐     ┌──────────────┐     ┌──────────────────┐
│ Tools  │     │ Knowledge    │     │ Observability    │
│ (ext)  │     │ Stores       │     │ Providers        │
└───┬────┘     │ (SQLite)     │     │ (OpenTelemetry,  │
    │          │ Tasks/Mem/   │     │  stdout, DD, CW) │
    │          │ Registries   │     └──────────────────┘
    │          └──────────────┘
    ▼
┌──────────────────────────┐
│    Execution Targets     │
│ (Slack, Jira, Email, ...)│
└──────────────────────────┘
```

## 3. Layer Definitions

### 3.1 Domain Layer (`src/domain/`)

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
| `ExecutionTargetId` | Identifier for an output destination |
| `PolicyDecision` | Result of a policy check (allow/deny with reason) |

**Repository Ports (interfaces):**

- `SessionRepository`
- `TaskRepository`
- `MemoryStore`
- `RegistryStore`
- `PolicyRuleRepository`
- `ConfigRepository` — stores global config, resolves per-signal overrides against defaults via `resolve(signalOverrides)`

**Domain Services:**

- `PolicyEngine` — evaluates whether a session+action is permitted
- `AuthService` — resolves identity from raw signal metadata

### 3.2 Application Layer (`src/application/`)

Orchestrates domain objects to fulfill use cases.

**Use Cases:**

| Use Case | Description |
|----------|-------------|
| `ReceiveSignal` | Accepts raw signal, authenticates, resolves config (merges global defaults with per-signal overrides). If signal carries a valid `session_id` and the source permits continuation, loads the existing session with new input appended; otherwise creates a new session. Routes to agent loop. |
| `ResolveConfig` | Takes global config + signal overrides, returns resolved `SessionConfig` with merge logic |
| `ExecuteAgentIteration` | Single agent loop tick: think → call tool → observe |
| `ProduceOutput` | Takes agent output, checks governance, dispatches to execution target |
| `ListSessions` | Queries active/completed sessions |
| `GetSessionLog` | Returns full observability trace for a session |
| `ConfigurePolicy` | CRUD for policy rules |

**Pattern:** Each use case is a function accepting domain ports + DTO, returning a `Result<T, E>`.

### 3.3 Adapter Layer (`src/adapter/`)

Implements the ports defined in `domain/`. Swappable by configuration.

**Signal Sources (`SignalSource`):**

Signal adapters receive raw input and may include optional per-signal config overrides (inline or by reference) alongside the payload. Each source can optionally declare `allowSessionContinuation` in its config — when true, the adapter inspects the incoming signal for a `session_id` field and, if valid, loads the existing session rather than creating a new one.

- `HttpWebhookSignalSource` — Express/Koa route handler
- `SlackSignalSource` — Slack Events API adapter
- `KafkaSignalSource` — Kafka consumer
- `CliStdinSignalSource` — reads from stdin/pipe
- `ScheduledPollerSignalSource` — cron-based polling

**Execution Targets (`ExecutionTarget`):**

- `StdoutExecutionTarget` — writes to stdout (dev/default)
- `SlackExecutionTarget` — posts to Slack channel
- `JiraExecutionTarget` — creates Jira tickets
- `EmailExecutionTarget` — sends via SMTP
- `PagerDutyExecutionTarget` — triggers PagerDuty alerts

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

Each port is backed by an adapter that chooses its own storage topology. The port abstraction in the domain layer makes the topology invisible to use cases — a `PerSessionSqliteTaskStore` and a `SharedSqliteRegistryStore` share the same `TaskStore`/`RegistryStore` interface.

| Port | Adapter | Topology |
|------|---------|----------|
| `SessionRepository` | `SharedSqliteSessionRepository` | Shared `sessions.db` — sessions are metadata, few enough for a single table |
| `TaskRepository` | `PerSessionSqliteTaskRepository` | `tasks/<session_id>.db` — one file per session, tasks are write-heavy per agent iteration and isolated by session |
| `MemoryStore` | `PerSessionSqliteMemoryStore` | `memory/<session_id>.db` — per-session KV store, avoids lock contention between sessions |
| `RegistryStore` | `SharedSqliteRegistryStore` | Shared `registry.db` — global KV store (skill definitions, tool manifests), read-mostly |
| `SignalQueueStore` | `SharedSqliteSignalQueueStore` | Shared `signal_queue.db` — atomic dequeue ordering across all sessions |
| `PolicyRuleRepository` | `SharedSqlitePolicyRuleRepository` | Shared `policies.db` — global policy rules, read-mostly |
| `ConfigRepository` | `SharedSqliteConfigRepository` | Shared `config.db` — key-value config, seeded from YAML at bootstrap, settled in SQLite for runtime reads |

No cross-DB joins are needed because each port serves a distinct purpose — write paths are naturally separated. The only coordination is at the worker level: dequeue a signal (signal queue) → load session (sessions) → execute iteration (tasks + memory per session) → record trace (observability). Each step touches different files.

### 3.4 Infrastructure Layer (`src/infra/`)

Wires everything together. Configuration, dependency injection, SQLite setup, HTTP server bootstrap.

**Key Components:**

- `di-container.ts` — manual DI or lightweight container wiring
- `config.ts` — reads config via `SharedSqliteConfigRepository` (seeded from a YAML bootstrap file on first run, stored in SQLite for runtime), resolves adapter implementations
- `sqlite.ts` — manages multiple DB connections per the adapter topology (shared DBs + per-session DBs), each in WAL mode
- `http-server.ts` — Express/Fastify server mounting signal source routes
- `logging.ts` — logger setup

## 4. Extension Points

Every extension point is a **port** (interface) in `domain/`. Adapters live in `adapter/` and are selected via configuration.

```typescript
// Example port definition
interface SignalSource {
  readonly type: string;
  listen(handler: SignalHandler): Promise<void>;
  shutdown(): Promise<void>;
}
```

To add a new signal source: implement `SignalSource` → register in config. No core code changes.

## 5. Data Flow (End-to-End)

```
1. Signal arrives, optionally with per-signal config overrides and/or a session_id
2. AuthProvider resolves identity from signal metadata
3. ResolveConfig merges global defaults with signal-level overrides → resolved SessionConfig
4. If signal carries a session_id and the source permits continuation, SessionManager loads the existing session with new input appended. Otherwise, creates a new session with resolved config.
5. PolicyEngine checks: is this source+identity allowed to start or continue this session?
6. PolicyEngine resolves the set of tools permitted for this session.
7. Agent loop begins (pi-coding-agent) with tools pre-filtered by policy and resolved model/prompts/skills:
   a. Agent thinks → calls a tool
   b. Execute tool (reads/writes Knowledge stores)
   c. ObservabilityProvider records the iteration
   d. Repeat until agent produces final output
8. Final output → PolicyEngine checks execution target permissions
9. ExecutionTarget delivers the output
10. Session is closed; full trace is persisted
```

## 6. Multi-Agent Strategies

The `AgentStrategy` port allows different orchestration patterns:

| Strategy | Description |
|----------|-------------|
| `SingleAgent` | One agent handles the full session |
| `SupervisorWorker` | Supervisor delegates subtasks to worker agents |
| `Swarm` | Multiple agents collaborate on the same session |
| `Router` | Incoming signal is classified and routed to specialized agents |

Each strategy wraps `pi-coding-agent`'s loop with additional coordination logic.

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
| Output | Before dispatch | Is this execution target permitted for this session? |

Policy rules are stored in SQLite and support:

- **Allow/deny lists** — specific tools, targets, or sources
- **RBAC** — role-based (admin, operator, viewer)
- **ABAC** — attribute-based (time of day, signal source type, identity claims)
- **LLM-gated** — ask the LLM to evaluate (experimental)

## 9. Observability

Every agent loop iteration produces a structured event:

```typescript
interface AgentIterationEvent {
  sessionId: SessionId;
  iteration: number;
  phase: 'think' | 'tool_call' | 'observe' | 'decide';
  input?: unknown;
  output?: unknown;
  timestamp: Date;
}
```

Observability providers receive these events. The console provider writes JSON lines; OpenTelemetry provider converts to spans.

## 10. Configuration Model

Configuration is accessed through a `ConfigRepository` port. The `SharedSqliteConfigRepository` adapter stores config in a shared SQLite `config` table, seeded from a YAML bootstrap file on first run and mutable at runtime via `ConfigurePolicy` and similar use cases.

Because every store is behind a domain port, no adapter has a fixed topology — the same `ConfigRepository` interface could be backed by YAML, a single SQLite file, or a Postgres table without touching application or domain code.

Signals can carry optional per-signal overrides (inline or by reference) for model, policies, prompt, skills, and execution targets. `ConfigRepository.resolve()` merges these against global defaults before the session starts — signal values override global, unspecified fields fall through.

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

execution:
  targets:
    - type: jira
      config:
        base_url: https://company.atlassian.net
        api_token: ${JIRA_TOKEN}

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

## 11. Database Schema (SQLite)

```
sessions:
  id TEXT PRIMARY KEY,
  identity_json TEXT NOT NULL,
  source_type TEXT NOT NULL,
  source_metadata_json TEXT,
  status TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  completed_at TEXT

tasks:
  id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL REFERENCES sessions(id),
  status TEXT NOT NULL,
  input_json TEXT,
  output_json TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL

agent_iterations:
  id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL REFERENCES sessions(id),
  iteration_number INTEGER NOT NULL,
  phase TEXT NOT NULL,
  input_json TEXT,
  output_json TEXT,
  timestamp TEXT NOT NULL

memory:
  session_id TEXT NOT NULL REFERENCES sessions(id),
  key TEXT NOT NULL,
  value_json TEXT NOT NULL,
  PRIMARY KEY (session_id, key)

registries:
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL,
  ttl_seconds INTEGER

config:
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL
policy_rules:
  id TEXT PRIMARY KEY,
  priority INTEGER NOT NULL,
  effect TEXT NOT NULL,
  conditions_json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
```

## 12. Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| **Ports in domain, adapters in adapter** | Pure DDD — domain has zero infrastructure dependencies |
| **`Result<T, E>` instead of exceptions** | Makes fallibility explicit in type signatures; no hidden control flow |
| **SQLite with WAL mode** | Zero-ops, ACID, good enough for single-tenant on-prem. WAL allows concurrent reads during writes. |
| **No barrel files** | Prevents circular dependencies, keeps import graph explicit |
| **`pi-coding-agent` wraps the loop** | We don't reinvent agent orchestration. Governance resolves permitted tools before the loop; observability wraps each iteration. |
| **Auth flows through** | Identity is resolved once at signal ingress and carried in the Session object — never re-authenticated unless policy demands it |
| **Per-session governance** | Policy resolves the tool set per session based on identity + source metadata. The agent only sees tools it's allowed to use — no per-call gate needed. |
| **Per-signal config resolution** | Each signal carries optional overrides (inline or by reference) for model, policies, prompt, skills. `ConfigRepository.resolve()` merges these against global defaults before the session starts. Keeps global config simple while enabling fine-grained per-signal tuning without ad-hoc env vars. |
| **Session continuation opt-in** | Only signal sources that declare `allowSessionContinuation` inspect incoming signals for a `session_id`. This avoids accidental hijacking — a Kafka consumer or public webhook will never load an arbitrary session. Continuation appends new input to the agent loop context, enabling multi-turn interactions across signals. |
| **Config via repository port** | `ConfigRepository` in domain decouples config source from consumers. Starts with a YAML file adapter; can swap to SQLite or any store later without touching application code. |
| **`typebox` for JSON schema** | Used for tool parameter schemas (via pi-coding-agent SDK), domain DTO validation, and config shape. Same library everywhere — no reason to introduce a second schema lib. |
| **Builders and fakes over mocks** | State-based tests are more resilient to refactoring than interaction-based mocks |
| **Federated storage via ports** | Each store port in domain resolves to a different adapter topology: shared DBs for global state (queue, registry, config), per-session DBs for hot-path writes (tasks, memory), and append-only files for observability. The domain layer knows nothing about this — the adapter layer decides. This prevents any single SQLite file from becoming a bottleneck. Cross-DB joins are never needed because each port addresses a separate concern. |

## 13. Supplemental: Bidirectional (Request-Response) Flow

Some signal sources expect a response — HTTP returns 200 with a body, Slack replies in a thread, CLI prints to stdout. Others are fire-and-forget (Kafka, scheduled poller). The design accommodates both without domain changes.

### Approach

The signal source adapter attaches a response channel to the session context at ingress. The existing `ExecutionTarget` port routes output back through that channel rather than to an external destination.

| Signal Source | Response Execution Target | Mechanism |
|---------------|--------------------------|-----------|
| `HttpWebhookSignalSource` | `HttpResponseExecutionTarget` | Holds the `Response` object, writes body + status code |
| `SlackSignalSource` | `SlackThreadExecutionTarget` | Posts to the same thread using the thread timestamp from the event |
| `CliStdinSignalSource` | `StdoutExecutionTarget` | Already works — stdout is the natural reply channel |
| `KafkaSignalSource` | *(none — fire-and-forget)* | Output goes to the configured external target |
| `ScheduledPollerSignalSource` | *(none — fire-and-forget)* | Output goes to the configured external target |

### How it works

The `ProduceOutput` use case checks the session's `SignalSource` metadata. If the source registered a response channel, output is dispatched to the corresponding response `ExecutionTarget`. If no response channel exists (fire-and-forget sources), output goes to the default execution target configured in policy.

```
Signal arrives ──→ Auth/Session/Policy ──→ Agent Loop ──→ Output

     │                                                    │
     │  (bidirectional sources:                           │
     │   HTTP, Slack, CLI)                                │
     │                                                    ▼
     └────────────────── response ──────────→ HttpResponseExecutionTarget
                                              SlackThreadExecutionTarget
                                              StdoutExecutionTarget
```

### What changes

- **`adapter/` only** — New `HttpResponseExecutionTarget`, `SlackThreadExecutionTarget`. Signal source adapters gain a small amount of wiring to pass a reply handle into the session context.
- **`domain/`** — No changes. `Session.SignalSource` already carries source type and metadata. `ExecutionTarget` is already a port.
- **`application/`** — `ProduceOutput` logic checks whether a response execution target should be selected based on signal source metadata. Single responsibility preserved.
- **`infra/`** — Optional: response timeout wiring for HTTP to avoid dangling connections if the agent loop stalls.

## 14. Supplemental: Signal Queuing and Concurrent Processing

Enterprise signal volumes can exceed the throughput of a single sequential agent loop. The agent loop is I/O-bound (LLM API calls, DB reads/writes), so a single Node.js process can handle many concurrent sessions via async concurrency. However, without a queue, bursts of signals either overload the process or get dropped.

### Queue architecture

Signals are not processed inline. Instead they land in a `signal_queue` table, and a configurable worker pool dequeues them:

```
Signal arrives ──→ Auth ──→ signal_queue ──→ dequeue ──→ Session + Policy ──→ Agent Loop ──→ Output
                                    │                                            ↑
                                    │                                     Worker pool
                                    │                                 (N concurrent sessions)
                                    └──────────────────────────────────────────┘
```

**Signal sources return immediately** after enqueuing (or after auth, if auth is fast). The response channel for bidirectional sources waits on the session completing or a timeout.

This works because the queue decouples *processing capacity* from *ingestion*, but response delivery still happens inline — the signal source adapter holds an in-memory promise that the worker resolves when the session finishes. `maxConcurrentSessions` limits how many connections can be *in-flight*, not how many signals can be queued.

Per-source response behaviour:
- **HTTP** — Handler enqueues, then `await`s a deferred promise. Node.js holds the connection open (async I/O, not blocking). If the queue is full, the handler rejects with 503 *before* enqueuing — no dangling connection.
- **Slack** — Slack's Events API requires a 200 OK within 3 seconds just to acknowledge receipt. Handler enqueues, returns 200 immediately. Worker posts the reply to Slack's `response_url` later.
- **CLI** — Holds stdin/stdout open awaiting a deferred response (same as HTTP), or prints a session ID for polling.

### Queue table (in addition to existing schema)

```
signal_queue:
  id TEXT PRIMARY KEY,
  source_type TEXT NOT NULL,
  source_metadata_json TEXT,
  identity_json TEXT,
  payload_json TEXT,
  status TEXT NOT NULL DEFAULT 'pending',     -- pending, processing, completed, failed
  created_at TEXT NOT NULL,
  picked_at TEXT,                              -- when a worker picked it up
  completed_at TEXT,
  retry_count INTEGER NOT NULL DEFAULT 0,
  max_retries INTEGER NOT NULL DEFAULT 3
```

### Worker pool

- **Configurable concurrency** — `maxConcurrentSessions` in config controls how many sessions run simultaneously.
- **Async I/O concurrency** — Within a single Node.js process, one worker can manage many sessions because the loop is almost entirely I/O (LLM HTTP calls). CPU-bound post-processing would need `worker_threads`.
- **Polling** — Workers poll `signal_queue` for `pending` rows, atomically transition to `processing` via `UPDATE ... WHERE status = 'pending' LIMIT 1` (SQLite serializes writes, so no race).
- **Pick timeout** — If a worker crashes, sessions stuck in `processing` with a stale `picked_at` are retried after a grace period.

### Multi-process scaling (future)

When one Node.js process isn't enough:

1. **Multiple processes, shared SQLite** — WAL mode allows concurrent readers. Workers in separate processes poll the same queue. SQLite write contention becomes the bottleneck under high load.
2. **Dedicated queue** — Swap the SQLite-backed queue for Redis or Bull. The `SignalQueue` port abstracts this — same pattern as `ConfigRepository`.
3. **Stateless workers** — Workers hold no in-memory state. They can be scaled horizontally behind a proper queue without changes to domain or application layers.

### Backpressure

- Signal source adapters check queue depth before enqueuing.
- Configurable `maxQueueDepth` — if exceeded, sources either reject (HTTP 503) or signal intent to drop.
- Persistent high depth triggers an alert through the observability provider.

### What changes

- **`domain/signal-queue.ts`** — New `SignalQueue` port: `enqueue`, `dequeue`, `ack`, `nack`.
- **`adapter/sqlite-signal-queue.ts`** — SQLite-backed adapter using the table above.
- **`application/queue-worker.ts`** — New use case: polls the queue, orchestrates the full ReceiveSignal → ExecuteAgentIteration → ProduceOutput flow per item.
- **`infra/`** — Worker pool startup in the DI wiring. Config keys for `maxConcurrentSessions`, `maxQueueDepth`, `pickTimeoutMs`.
- **Signal sources** — No longer create sessions directly. They enqueue to `SignalQueue` and return immediately (or hold the response channel for bidirectional flows).
