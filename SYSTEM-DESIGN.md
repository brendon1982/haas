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
| `SignalSource` | Type + metadata about where the signal came from |
| `ToolCall` | A tool invocation within an agent iteration |
| `ExecutionTargetId` | Identifier for an output destination |
| `PolicyDecision` | Result of a policy check (allow/deny with reason) |

**Repository Ports (interfaces):**

- `SessionRepository`
- `TaskRepository`
- `MemoryStore`
- `RegistryStore`
- `PolicyRuleRepository`

**Domain Services:**

- `PolicyEngine` — evaluates whether a session+action is permitted
- `AuthService` — resolves identity from raw signal metadata

### 3.2 Application Layer (`src/application/`)

Orchestrates domain objects to fulfill use cases.

**Use Cases:**

| Use Case | Description |
|----------|-------------|
| `ReceiveSignal` | Accepts raw signal, authenticates, creates session, routes to agent loop |
| `ExecuteAgentIteration` | Single agent loop tick: think → call tool → observe |
| `ProduceOutput` | Takes agent output, checks governance, dispatches to execution target |
| `ListSessions` | Queries active/completed sessions |
| `GetSessionLog` | Returns full observability trace for a session |
| `ConfigurePolicy` | CRUD for policy rules |

**Pattern:** Each use case is a function accepting domain ports + DTO, returning a `Result<T, E>`.

### 3.3 Adapter Layer (`src/adapter/`)

Implements the ports defined in `domain/`. Swappable by configuration.

**Signal Sources (`SignalSource`):**

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

- `SqliteSessionRepository`
- `SqliteTaskRepository`
- `SqliteMemoryStore`
- `SqliteRegistryStore`
- `SqlitePolicyRuleRepository`

### 3.4 Infrastructure Layer (`src/infra/`)

Wires everything together. Configuration, dependency injection, SQLite setup, HTTP server bootstrap.

**Key Components:**

- `di-container.ts` — manual DI or lightweight container wiring
- `config.ts` — reads env/config file, resolves adapter implementations
- `sqlite.ts` — connection pool (WAL mode, single writer), migrations
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
1. Signal arrives (e.g., Slack message)
2. AuthProvider resolves identity from signal metadata
3. SessionManager creates/loads Session (SessionId + Identity + SignalSource)
4. PolicyEngine checks: is this source+identity allowed to start a session?
5. PolicyEngine resolves the set of tools permitted for this session.
6. Agent loop begins (pi-coding-agent) with tools pre-filtered by policy:
   a. Agent thinks → calls a tool
   b. Execute tool (reads/writes Knowledge stores)
   c. ObservabilityProvider records the iteration
   d. Repeat until agent produces final output
7. Final output → PolicyEngine checks execution target permissions
8. ExecutionTarget delivers the output
9. Session is closed; full trace is persisted
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
                                                 │
                                                 ├──→ Failed
                                                 └──→ Cancelled
```

- **Created:** Raw signal received but not yet authenticated
- **Authenticated:** Identity resolved
- **Authorized:** Policy engine approved session start
- **Running:** Agent loop is active
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

Configuration is a single JSON/YAML file that declares which adapters to wire:

```yaml
signal:
  type: slack
  config:
    token: ${SLACK_TOKEN}
    signing_secret: ${SLACK_SIGNING_SECRET}

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
| **`typebox` for JSON schema** | Used for tool parameter schemas (via pi-coding-agent SDK), domain DTO validation, and config file shape. Same library everywhere — no reason to introduce a second schema lib. |
| **Builders and fakes over mocks** | State-based tests are more resilient to refactoring than interaction-based mocks |
