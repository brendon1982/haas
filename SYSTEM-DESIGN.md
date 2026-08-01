# System Design — Enterprise AI Harness (HaaS)

## Purpose

HaaS is an on-premises harness that accepts signals from source adapters, runs
them through an observable agent loop, and presents the result through the source
adapter. The domain defines ports; application code orchestrates them; adapters
implement storage and the Microsoft Agent Framework boundary; infrastructure
wires scopes, engines, and configuration.

```
source adapter -> IncomingSignal + SignalContext -> direct engine or queue
                                                     |
                                              RunSessionUseCase
                                                     |
                                 deterministic governance + scoped tool context
                                                     |
                                    AgentExecutionRequest -> agent + tools
```

## Core contracts

### Signal, identity, and context

- `IncomingSignal(Payload, Context, SessionId, ArrivedAt, MessageId)` is the
  ingress contract. Every source supplies a `SignalContext`, including the
  canonical explicit anonymous context.
- `Signal` remains auth-free: it carries payload, source, session, arrival, and
  message metadata. `SignalEnvelope` transports a `Signal` with its separate
  `SignalContext`.
- `Identity` has an ordinal `Issuer`, `Subject`, and immutable multi-valued
  claims. Its stable key is issuer plus subject. `Identity.Anonymous` is the
  canonical anonymous identity.
- `AuthenticationContext` carries the current `Identity`, authentication method,
  and opaque `CredentialReference` values. A credential reference is a lookup
  handle only; it never contains a secret.
- `SignalContext` carries `AuthenticationContext` plus immutable source policy
  attributes. Source adapters authenticate using their own protocol/framework and
  map the result to this context. There is no central raw-metadata authentication
  port.

Web ingress maps an authenticated `ClaimsPrincipal` to an `Identity` using its
issuer, subject/name identifier, and grouped claims. Missing or unauthenticated
principals use the explicit anonymous context. A SignalR connection ID is routing
and session metadata, never an authenticated identity.

### Session binding

`RunSessionUseCase` binds a new `SessionRecord` to the source, identity issuer,
and identity subject. A continuation must have the same ordinal source and stable
identity key before policy evaluation or agent execution. Claims, roles,
authentication method, source attributes, and credential references are not part
of the binding: every signal supplies their current values and those values govern
that run.

The persisted session retains the configured tool belt, not the per-run permitted
subset. This lets current policy be re-evaluated on every signal.

### Agent and tool boundary

`IAgentStrategy` receives `AgentExecutionRequest`, which contains only the
auth-free `Signal`, resolved session ID, and permitted tool names. The Microsoft
Agent Framework adapter builds its tool list exclusively from those names.
Authentication, claims, attributes, credential references, and the envelope are
not agent inputs; only `Signal.Payload` becomes a user chat message.

`RunSessionUseCase` pushes a `SignalExecutionContext` for the lifetime of the
execution. It contains session ID, source, current authentication, and current
attributes, but no payload or history. DI-resolved tool handlers inject the
read-only `ISignalContextAccessor` and read `Current`. It throws outside an active
signal execution. `ISignalContextScope` is the separate initializer port used by
the use case.

`ToolProvider` still uses the singleton `ISignalScopeAccessor` (`AsyncLocal`) to
resolve registered tool instances from the active per-signal DI scope. The
context accessor is a scoped service, so tools observe the same execution context
as the use case without exposing it in a callable tool signature.

## Execution flows

### Direct processing

1. A source adapter authenticates its input and constructs `IncomingSignal` with
   an explicit `SignalContext`.
2. `BaseHaasEngine` maps source metadata and payload to `Signal`, creates a
   `SignalEnvelope`, and dispatches it to `DirectHaasEngine`.
3. `DirectHaasEngine` creates a DI scope, sets `ISignalScopeAccessor`, and
   resolves `IRunSessionUseCase`.
4. `RunSessionUseCase` validates continuation binding, evaluates governance,
   pushes `SignalExecutionContext`, and invokes the agent with an
   `AgentExecutionRequest`.
5. The context is disposed before the engine clears the DI scope. Results or
   errors are presented through the registration's `ISignalPresenter`.

### Queued processing

1. The source path constructs the same envelope.
2. `EnqueueSignalUseCase` assigns missing session and arrival metadata to the
   inner `Signal`, preserves `SignalContext` unchanged, and stores the envelope.
3. `QueuedHaasEngine` workers dequeue work in a new DI scope and set
   `ISignalScopeAccessor`.
4. `SignalWorker` resolves the source registration and calls
   `IRunSessionUseCase` with the stored envelope.
5. Successful work is acknowledged and completes its deferred result. A
   `GovernanceDeniedException` is presented as an error, completes the deferred
   result with that error, and is acknowledged without retry. Unexpected failures
   follow the queue's nack/retry path.

Both paths apply exactly the same session binding, policy checks, agent boundary,
and scoped tool context.

## Deterministic governance

`IPolicyEngine` evaluates `PolicyRequest`, which contains only session ID, gate,
source, current identity and claims, current source attributes, and an optional
candidate tool name. It intentionally has no payload, history, authentication
method, or credential references.

There are two gates:

| Gate | Timing | Result |
| --- | --- | --- |
| `SessionStart` | Every new and resumed signal, after continuation binding and before session creation/agent execution | Allows or denies the execution. |
| `ToolResolution` | Before the agent loop, once for each configured tool | Filters the configured tool belt. It can remove tools but cannot add unconfigured tools. |

`PolicyRule` has an ID, gate, priority, allow/deny effect, typed conditions, and
created/updated timestamps. Conditions support source types, stable subjects,
roles, claim operators (`Exists`, `Absent`, `AnyOf`, `AllOf`), attribute operators
(`Exists`, `Absent`, `Equals`, `NotEquals`, `AnyOf`), tool names, and recurring UTC
time windows. Session-start rules cannot contain a tool condition.

All populated condition categories must match. Entries within source, subject,
role, tool, and time-window categories are alternatives. Each listed claim or
attribute condition must match under its operator. The highest matching priority
wins; a deny wins ties at that priority. If no rule matches, independently
configured session-start and tool-resolution fallbacks apply (both default to
allow). Roles are read from the configured role claim type, which defaults to
`role`.

The application turns a denied decision into `GovernanceDeniedException`. A denial
or continuation binding mismatch is terminal governance handling, not a failed
agent session. Governance logs use only safe gate, outcome, source, tool,
issuer/subject, rule ID, reason, and condition-category information; they do not
include payloads, claim values, attribute values, credential references, prompts,
or history.

Runtime policy CRUD is exposed through repository-backed application use cases.
`HaasBuilder.WithGovernance` configures fallbacks, role claim type, and idempotent
startup seed rules. There is no HTTP or UI policy administration adapter.

## Persistence topology

SQLite adapters use separate databases; in-memory adapters implement the same
ports for development and tests.

### `sessions.db`

```
sessions:
  SessionId TEXT PRIMARY KEY
  SourceType TEXT NOT NULL
  Status TEXT NOT NULL
  Provider TEXT NOT NULL
  ModelId TEXT NOT NULL
  SystemPrompt TEXT NOT NULL
  Tools TEXT NOT NULL
  ThinkingLevel TEXT NOT NULL
  Output TEXT
  CreatedAt TEXT NOT NULL
  UpdatedAt TEXT NOT NULL
  IdentityIssuer TEXT
  IdentitySubject TEXT
```

Only the stable bound issuer and subject are persisted. Claims and credential
references are never stored in a session.

### `signal_queue.db`

```
signal_queue:
  id TEXT PRIMARY KEY
  session_id TEXT
  source_type TEXT NOT NULL
  payload_json TEXT
  context_json TEXT
  arrived_at TEXT
  message_id TEXT
  status TEXT NOT NULL
  created_at TEXT NOT NULL
  picked_at TEXT
  completed_at TEXT
  retry_count INTEGER NOT NULL
  max_retries INTEGER NOT NULL
  visible_at TEXT
  last_error TEXT
```

`context_json` serializes identity claims, safe source attributes, authentication
method, and opaque credential references so queued execution receives the same
envelope. It has no representation for resolved secret values.

### `policies.db`

```
policy_rules:
  id TEXT PRIMARY KEY
  gate TEXT NOT NULL
  priority INTEGER NOT NULL
  effect TEXT NOT NULL
  conditions_json TEXT NOT NULL
  created_at TEXT NOT NULL
  updated_at TEXT NOT NULL
```

`conditions_json` stores the typed source, subject, role, claim, attribute, tool,
and UTC time-window condition collections. Invalid persisted enum or condition
data fails explicitly during loading.

### Other stores

- `config.db` stores signal-source and provider configuration.
- `sessions/<session_id>.db` stores the session's domain messages.

## Service lifetimes and extension points

| Component | Lifetime | Responsibility |
| --- | --- | --- |
| `IHaasEngine`, `IToolProvider`, `ISignalScopeAccessor` | Singleton | Global engine/registry and active DI-scope access. |
| `ISignalContextAccessor`, `ISignalContextScope`, `IRunSessionUseCase`, `IAgentStrategy` | Scoped | One execution's context and orchestration. |
| `SignalWorker` | Transient | One queued dequeue operation in a scoped worker loop. |
| `IPolicyRuleRepository`, `IPolicyEngine`, policy CRUD use cases | Singleton | Shared deterministic policy state. |

Domain ports include repositories for sessions, messages, queues, source/provider
configuration, deferred results, and policy rules, plus agent strategy, tool
provider, signal context access, and policy engine ports. Implement new sources
at the ingress boundary: authenticate with the source protocol, create an
explicit context, and submit `IncomingSignal`; do not put authentication in
payloads or tool method parameters.
