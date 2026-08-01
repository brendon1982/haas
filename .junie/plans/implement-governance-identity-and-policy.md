# Governance: Identity Propagation and Deterministic Policy Enforcement

## Problem and outcome

Implement the governance slice end to end so each signal source explicitly supplies authenticated identity information, opaque credential references, and policy attributes; that information remains outside all agent/session chat content; each session is bound to a stable identity; deterministic policies authorize every new or resumed signal and filter the tools exposed to the agent; and DI-resolved tools can read the current signal context without adding auth parameters to LLM-visible tool schemas.

The implementation must preserve both direct and queued processing, support in-memory and SQLite adapters, expose runtime policy CRUD plus fluent startup seeding, and use structured redacted logs for governance decisions.

## Current code state

- `Identity` exists, and `ISignalQueue`/`QueuedSignal` can carry it, but `EnqueueSignalUseCase` always substitutes `Identity.Anonymous`.
- `SignalWorker` discards the identity read from the queue. `DirectHaasEngine` has no identity path.
- `IncomingSignal` cannot carry authentication or source policy attributes.
- `SessionRecord` has no bound identity, so continuation currently trusts only the supplied session ID.
- `MicrosoftAgentFrameworkStrategy` sends only `Signal.Payload` into the user chat message, which is the correct model-content boundary to preserve.
- Generic tool handlers are already resolved from a per-signal DI scope by `ToolProvider` and `ISignalScopeAccessor`. This permits a scoped context accessor without modifying LLM-visible tool parameters.
- The web example has an ad hoc scoped session-ID context/decorator that the core context should replace.
- No policy model, engine, repository, CRUD use cases, persistence, or enforcement exists.
- SQLite repositories use hand-written `Microsoft.Data.Sqlite`; new persistence should follow that implemented convention rather than the stale EF Core design note.
- The actual projects target .NET 10 and Microsoft Agent Framework 1.15.0. Code takes precedence over older version text in documentation.

## Confirmed decisions

- Include identity/auth propagation, the session-start gate, and the tool-resolution gate.
- Policies are deterministic only. Do not implement any LLM-based policy evaluation.
- Support allow/deny, RBAC, and typed ABAC conditions for source, subject, role/claim, signal attribute, tool name, and recurring UTC time windows.
- Policy fallback is configurable separately for session and tool gates and defaults to allow for backward compatibility.
- A larger priority number wins. At the highest matching priority, deny wins ties.
- Every signal source must explicitly supply an authentication context, even when that context is explicitly anonymous.
- Source adapters perform their own protocol/framework-specific authentication and map the result into domain contracts. Do not add a central raw-metadata authenticator.
- Carry identity/claims and opaque credential references only. Never carry or persist raw access tokens, API keys, passwords, or resolved secrets.
- Tool-specific secret resolution remains the responsibility of injected source/vault services. Do not invent a common credential-vault port in this slice.
- A session is bound to its original source and stable identity key. A resumed signal with a different source, issuer, or subject is rejected.
- Claims, roles, authentication method, attributes, and credential references are not part of identity equality. Every signal supplies current values, and those latest values govern that execution.
- Re-evaluate both gates on every signal, including resumed sessions.
- Policy denial and identity mismatch are terminal governance errors: present them through `PresentErrorAsync`, acknowledge queued work without retry, complete queued waiters with the error, and do not mark the session failed.
- Audit through the existing `ILogger` port only. Do not add a durable decision-audit store.
- Runtime policy management includes repository CRUD, application CRUD use cases, and fluent startup seeding; no HTTP or UI administration adapter.

## Non-negotiable security invariants

1. `Signal` remains auth-free. Only `Signal.Payload` becomes the agent user message.
2. `IAgentStrategy` never receives `AuthenticationContext`, claims, attributes, credential references, or a general-purpose envelope.
3. Policy requests deliberately omit signal payload/history and credential references.
4. Tool auth is accessed through a scoped, read-only context injected into the tool handler. Auth is never represented as a tool method parameter, so it cannot appear in the function schema shown to the LLM.
5. Session persistence stores only the stable bound identity key, never current claims or credential references.
6. Queue persistence stores only identity/claims, safe source attributes, and opaque credential references; it has no field or type for resolved secret values.
7. Governance logs may contain gate, outcome, source, tool name, stable issuer/subject, matched rule ID, reason code, and condition category names. They must not contain claim values, attribute values, credential references, payloads, prompts, or message history.
8. The persisted session tool belt remains the configured tool universe. Never overwrite it with a per-signal permitted subset.

## Target contracts

Use these names and responsibilities consistently; do not collapse auth back into `Signal`.

### Identity and signal context

- Replace the current `Identity(Name, string[] Claims)` with a structured record containing:
  - `Issuer`
  - `Subject`
  - immutable/multi-valued claims (`claim type -> values`)
  - a stable key based only on `Issuer + Subject`
  - canonical `Identity.Anonymous` with fixed issuer/subject values
  - helpers for case-sensitive claim types and ordinal claim values
- Add `CredentialReference(Name, Provider, Reference)` as an opaque lookup reference. It must not have a secret/value/token property.
- Add `AuthenticationContext` containing:
  - current `Identity`
  - current authentication method/type
  - immutable credential references keyed by logical name
  - canonical `AuthenticationContext.Anonymous`
- Add `SignalContext` containing:
  - `AuthenticationContext`
  - immutable source-provided policy attributes (`string -> string`)
- Change `IncomingSignal` so `SignalContext` is a required constructor argument. Payload-only construction must stop compiling; update all sources and tests to pass an explicit context.
- Add `SignalEnvelope(Signal Signal, SignalContext Context)` for application/engine/queue propagation.
- Add `SignalExecutionContext(SessionId, Source, Authentication, Attributes)` for tools. It must not contain signal payload or chat history.

### Scoped tool access

- Add a read-only domain port `ISignalContextAccessor` exposing the current `SignalExecutionContext` and throwing a clear `InvalidOperationException` when no signal execution is active.
- Add a separate application-facing scope/initializer port, such as `ISignalContextScope`, whose `Push(SignalExecutionContext)` returns `IDisposable`.
- Implement both with one scoped infrastructure service. Register both interfaces to the same scoped instance.
- `Push` must reject an already-active context; disposal clears/restores it. This prevents accidental reuse in tests or future nested execution.
- Keep the existing singleton/`AsyncLocal` `ISignalScopeAccessor` unchanged because `ToolProvider` still needs it to resolve tool instances from the active DI scope.
- `RunSessionUseCase` owns the lifetime of the new execution context with `using`/`try-finally`. Tool authors inject only the read-only accessor.

### Agent execution boundary

- Add `AgentExecutionRequest` containing only:
  - auth-free `Signal`
  - resolved session ID
  - permitted tool names for this execution
- Change `IAgentStrategy.ExecuteAsync` and all implementations/decorators/fakes to accept this request plus the presenter.
- `MicrosoftAgentFrameworkStrategy` may still load stored session/model configuration, but it must stop using `record.ToolBelt` to populate `ChatOptions.Tools`.
- Build `ChatOptions.Tools` exclusively from `AgentExecutionRequest.PermittedToolNames`, then resolve those names through `IToolProvider`.

### Policy model

- Add `PolicyGate`: `SessionStart`, `ToolResolution`.
- Add `PolicyEffect`: `Allow`, `Deny`.
- Add typed condition records/enums for:
  - source types
  - stable subjects (`issuer + subject`)
  - roles
  - claims (`Exists`, `Absent`, `AnyOf`, `AllOf`)
  - signal attributes (`Exists`, `Absent`, `Equals`, `NotEquals`, `AnyOf`)
  - tool names
  - recurring UTC day/time windows
- Add `PolicyRule` with ID, gate, priority, effect, typed conditions, created timestamp, and updated timestamp.
- Add a validation path used by both fluent configuration and runtime saves:
  - non-empty stable ID
  - supported gate/effect/operators
  - non-empty condition keys/values where required
  - tool conditions are invalid for `SessionStart`
  - normalized, immutable collections
  - valid UTC time boundaries
- Add `PolicyRequest` containing only session ID, gate, source, current identity/claims, current source attributes, and optional candidate tool name. It must not accept `Signal`, payload, auth method, or credential references.
- Add `PolicyDecision` containing allowed/denied, applied effect, matched rule ID if any, fallback/rule reason code, priority if any, and matched condition category names. Do not copy claim/attribute values into it.
- Add `GovernanceDeniedException` carrying session ID, gate, safe reason code, and matched rule ID. Use the same terminal exception family for identity/source continuation mismatch so queue code can classify it without message parsing.
- Add `IPolicyRuleRepository` CRUD:
  - get by ID
  - list all
  - list by gate
  - save/upsert
  - delete
- Add `IPolicyEngine.EvaluateAsync(PolicyRequest, CancellationToken)`.

## Deterministic policy semantics

Implement and test the algorithm exactly:

1. Load only rules for the requested gate.
2. A rule with no conditions matches every request at that gate.
3. Every populated condition category must match (AND across categories).
4. Multiple source, subject, role, tool, or time-window entries within one category are alternatives (OR within that category).
5. Each listed claim/attribute condition must match; the operator controls matching against its own values.
6. Roles are read from a configurable role claim type, defaulting to `role`. Multiple roles listed by a rule are alternatives. Requiring several roles is represented with an `AllOf` claim condition.
7. A `ToolResolution` rule with no tool condition applies to every candidate tool. `SessionStart` rules cannot contain tool conditions.
8. UTC time windows are recurring by configured UTC days and `[start, end)` times. Support windows that cross midnight; use injected `TimeProvider`, never `DateTimeOffset.UtcNow`.
9. Higher integer priority wins.
10. From matching rules at the highest priority, any deny wins; otherwise allow wins.
11. If no rule matches, apply the configured fallback for that gate.
12. The engine returns a decision; application orchestration decides whether to throw a terminal governance exception.

## Execution flow

### Ingress and direct processing

1. A source authenticates with its native mechanism and creates an explicit `SignalContext`.
2. `BaseHaasEngine` maps only payload/source/session metadata to `Signal` and creates a separate `SignalEnvelope`.
3. `DirectHaasEngine` creates the existing DI scope, sets `ISignalScopeAccessor.ServiceProvider`, resolves `IRunSessionUseCase`, and passes the envelope.
4. `RunSessionUseCase` resolves the session ID, creates/pushes `SignalExecutionContext`, enforces identity/policy, and invokes the strategy with an auth-free `AgentExecutionRequest`.
5. The scoped context is cleared before the DI scope is disposed.

### Queued processing

1. `EnqueueSignalUseCase` receives the envelope, assigns missing session/arrival metadata to the inner `Signal`, preserves the supplied context unchanged, and enqueues the envelope. It must never substitute anonymous identity.
2. Both queue adapters store/return the full envelope.
3. `SignalWorker` resolves the registration, presents processing, and passes the dequeued envelope to `IRunSessionUseCase`.
4. On `GovernanceDeniedException`:
   - call that registration's `PresentErrorAsync`
   - complete `IDeferredSessionResultStore` with the error
   - `AckAsync` the queue item
   - log a redacted governance denial
   - return without rethrowing or retrying
5. Only unexpected/runtime failures use the existing `NackAsync` retry path.

### Session and policy orchestration

`RunSessionUseCase` must perform work in this order:

1. Validate envelope and presenter.
2. Resolve source configuration and session ID.
3. Load an existing session, if any.
4. If existing:
   - require exact ordinal source match
   - require stable identity-key match (`Issuer + Subject`)
   - reject mismatch before policy/agent execution and without changing session state
   - use the stored session/model/tool configuration
5. Push the current `SignalExecutionContext`.
6. Evaluate `SessionStart` for every signal, including continuation. On deny, throw `GovernanceDeniedException` before creating/updating a session.
7. Start with only the configured tool names from the selected session config.
8. Evaluate `ToolResolution` independently for each configured tool using current claims/roles/attributes; retain only allowed tools. Policy can remove configured tools but can never add an unconfigured tool.
9. For a new allowed session, persist a record bound to source and stable identity key. Store the original configured tool belt, not the filtered subset.
10. Invoke `IAgentStrategy` with the auth-free signal, session ID, and filtered tool names.
11. Preserve existing completion/failure behavior for genuine strategy failures.
12. Policy denial or identity/source mismatch must not set `SessionRecord.Status` to `Failed`.

## Persistence and migration details

### Signal queue

- Change `ISignalQueue.EnqueueAsync` to accept `SignalEnvelope`.
- Change `QueuedSignal` to contain the envelope rather than separate `Signal` and legacy `Identity`.
- In `signal_queue`, add append-only nullable migration columns:
  - `context_json`
  - `arrived_at`
  - `message_id`
- Continue storing payload/source/session in their existing columns. Serialize `SignalContext` into `context_json`.
- For legacy rows with no `context_json`, construct explicit anonymous context. This is safe because production enqueue currently always writes anonymous identity.
- Rehydrate complete `Signal` metadata on dequeue; this also fixes the currently dropped `ArrivedAt` and `MessageId`.
- Use explicit selected columns and strict JSON deserialization. Invalid non-null JSON is an error, not an anonymous fallback.

### Sessions

- Append `IdentityIssuer` and `IdentitySubject` to `SessionRecord` and the SQLite table.
- Add `ALTER TABLE` migration statements for existing databases.
- Existing rows with missing/null identity fields map to canonical anonymous identity. Therefore legacy sessions may only resume under explicit anonymous context.
- Replace `SELECT *`/ordinal assumptions with an explicit column list.
- Update insert/upsert columns and every `SessionRecord` builder/construction site.
- Store no claims, attributes, authentication method, or credential references in `sessions.db`.

### Policies

- Add in-memory and `SharedSqlitePolicyRuleRepository`.
- Use a dedicated `policies.db`.
- Use an explicit schema:
  - `id TEXT PRIMARY KEY`
  - `gate TEXT NOT NULL`
  - `priority INTEGER NOT NULL`
  - `effect TEXT NOT NULL`
  - `conditions_json TEXT NOT NULL`
  - `created_at TEXT NOT NULL`
  - `updated_at TEXT NOT NULL`
- The explicit `gate` column is an intentional refinement of `SYSTEM-DESIGN.md`; it prevents cross-gate ambiguity and permits gate-filtered reads.
- Serialize only typed conditions in `conditions_json`; reject malformed or unknown enum/operator data.
- Parameterize all SQL and use explicit column lists.
- Startup seeds are inserted only when their ID is absent so runtime CRUD changes are not overwritten on every restart.

## Configuration and DI

- Add governance configuration with:
  - session fallback effect, default `Allow`
  - tool fallback effect, default `Allow`
  - role claim type, default `role`
  - startup rule seeds
- Add `WithGovernance(Action<HaasGovernanceBuilder>)`.
- Fluent rule shape should support a readable configuration such as:
  - stable rule ID
  - gate
  - priority
  - allow/deny
  - source/subject/role/claim/attribute/tool/time conditions
- Register default configuration, scoped signal context service, deterministic policy engine, in-memory policy repository, and policy CRUD use cases from `AddHaas`.
- Register policy repositories through DI factory lambdas, not eagerly-created instances, so they consume the final governance options and startup seeds regardless of builder call order.
- In-memory and SQLite repository factories must use the same final seed set and injected `TimeProvider`.
- `WithSqlitePersistence` always wires `policies.db`; this is independent of `includeConfig`.
- Ensure policy engine and CRUD use cases resolve the same singleton repository instance.

## TDD implementation phases and todos

### 1. Establish auth-safe domain contracts

Start with failing domain/compile-time tests, then:

- Redesign `Identity` and add builders for identity, authentication, signal context, envelope, and execution context.
- Add credential-reference and signal-context records.
- Make `IncomingSignal` require explicit context.
- Add the envelope and scoped context ports.
- Add `AgentExecutionRequest` and change `IAgentStrategy`.
- Update existing builders/fakes only far enough to restore a green baseline.

Acceptance:

- Stable identity equality uses only issuer/subject.
- Anonymous values are canonical.
- No auth type references exist on `Signal` or `AgentExecutionRequest`.
- Every source/test supplies explicit anonymous or authenticated context.

### 2. Propagate auth through direct and queued paths

Write failing ingress/queue tests first, then update:

- `BaseHaasEngine`, `DirectHaasEngine`, `QueuedHaasEngine`
- `IEnqueueSignalUseCase`/`EnqueueSignalUseCase`
- `ISignalQueue`, `QueuedSignal`, in-memory queue, SQLite queue
- `SignalWorker` and its fakes/tests

Add SQLite migration tests starting from the pre-change queue schema.

Acceptance:

- Direct execution receives the exact source context.
- Queue round-trip preserves identity, claims, auth method, attributes, credential references, arrival time, and message ID.
- No code substitutes anonymous context.
- Legacy queued rows become explicit anonymous context.

### 3. Bind sessions and expose current context to tools

Write failing continuation/context tests first, then:

- Add stable identity fields to `SessionRecord`, builders, in-memory behavior, and SQLite repository/migrations.
- Implement/register the scoped read-only accessor plus scope initializer.
- Update `RunSessionUseCase` and decorators/fakes to receive envelopes and push context.
- Reject source/identity mismatch without mutating the session.
- Add a generic DI-resolved tool test whose constructor injects the accessor and observes session/source/current auth.
- Add concurrent-scope isolation coverage so two signal scopes cannot observe each other's context.

Acceptance:

- Same issuer/subject with changed claims/roles/references resumes successfully and exposes the newest values.
- Changed issuer or subject is rejected.
- Legacy anonymous sessions resume only as anonymous.
- Tool method schemas contain only LLM-authored arguments.

### 4. Implement deterministic policy domain behavior

Write one failing test per matching partition, then add:

- Policy gates/effects/rules/conditions/requests/decisions/options
- Rule validation
- Repository and policy-engine ports
- Deterministic engine using `TimeProvider`
- Redacted structured decision logs

Required tests:

- no-match fallback for each gate
- matching empty rule
- priority direction
- deny tie
- AND across categories and OR within categories
- source and subject
- role claim configuration
- each claim operator
- each attribute operator
- tool-specific and all-tool rules
- ordinary and overnight UTC windows, day boundaries, and end-exclusive behavior
- gate isolation
- log capture proving no claim/attribute/reference/payload values are emitted

### 5. Add policy persistence, CRUD, and fluent configuration

Write adapter/application/infrastructure tests first, then add:

- In-memory policy repository
- `SharedSqlitePolicyRuleRepository` and `policies.db`
- list/get/save/delete application use cases, with `TimeProvider` timestamp handling
- `HaasGovernanceBuilder` and `WithGovernance`
- insert-if-absent startup seeding
- default and SQLite DI wiring

Acceptance:

- In-memory and SQLite repositories have equivalent CRUD behavior.
- Rules round-trip every typed condition.
- Invalid JSON/operator data fails explicitly.
- Seeds are idempotent and do not overwrite runtime edits.
- Builder call order does not change defaults, seeds, or selected repository.
- `WithSqlitePersistence(..., includeConfig: false)` still creates and registers `policies.db`.

### 6. Enforce session and tool gates

Write failing orchestration tests first, then wire:

- continuation source/identity check
- session decision on every signal
- per-configured-tool evaluation
- filtered `AgentExecutionRequest`
- safe governance exceptions
- terminal queued denial handling

Update `MicrosoftAgentFrameworkStrategy` so only request-permitted tools enter `ChatOptions.Tools`.

Required tests:

- session allow and deny
- fallback allow/deny
- resumed session is re-evaluated with newest claims
- denied new session is not created
- denied existing session is not changed to failed
- configured-and-allowed tool is exposed
- configured-but-denied tool is absent
- policy-allowed but unconfigured tool cannot be added
- all tools denied produces an empty tool list
- queued denial is presented as error, waiter receives error, item is acknowledged, and no retry occurs
- direct denial is presented once through the existing engine error path

### 7. Prove the LLM isolation boundary

Add focused adapter/integration tests that capture:

- every message passed to `IChatClient`
- persisted `DomainMessage` content/payload
- generated function/tool schemas
- a DI-resolved tool's observed execution context

Use distinctive subject, claim, attribute, and credential-reference marker strings. Assert those markers never occur in model messages, persisted chat messages, or tool schemas, while the tool handler receives them through the scoped accessor.

Cover both direct and queued execution.

### 8. Update examples and authoritative documentation

- Update every CLI/web/test source to pass explicit authentication context.
- In web ingress adapters, map an authenticated `ClaimsPrincipal` to structured identity when present; otherwise pass explicit anonymous context. Do not treat connection ID as authenticated identity.
- Remove `ScopedSessionContext` and `SessionContextRunSessionUseCaseDecorator`.
- Update `WebTicTacToeToolHandlers` to use the core `ISignalContextAccessor` for session ID/current auth.
- Update `SYSTEM-DESIGN.md` to describe implemented contracts, deterministic policy gates, identity binding, policy schema, current direct/queued flow, and scoped tool context. Remove LLM-gated policy claims.
- Rewrite `IMPLEMENTATION-CONSIDERATIONS.md` away from the obsolete pi-coding-agent mutation hook and toward constructor-injected scoped context plus opaque credential resolution.

### 9. Validate the completed slice

- Run `dotnet build library/haas.sln`.
- Run `dotnet test library/haas.sln`.
- Run the relevant example/server compile paths if they are not included in `library/haas.sln`.
- Inspect the final dependency graph: domain contracts contain no adapter/DI implementation types; application owns orchestration; adapters own SQLite/MAF behavior; infrastructure owns registration/builders.
- Search for payload/auth coupling and confirm no strategy/chat-message/tool-schema path receives authentication objects.

## Todo dependency order

1. Auth-safe contracts
2. Signal propagation
3. Session binding and scoped tool context
4. Policy domain and deterministic engine
5. Policy persistence/CRUD/configuration
6. Gate enforcement and permitted-tool boundary
7. LLM isolation proof
8. Examples and documentation
9. Full validation

Todos 3 and 4 may proceed after todo 2, but the smaller implementation model should prefer the sequential order above. Todo 6 depends on todos 3, 4, and 5. Todo 7 depends on todo 6. Todo 8 follows the final public contracts. Todo 9 is last.

## Explicitly out of scope

- Raw credential storage or transport
- A common vault/credential resolver implementation
- HTTP/UI policy administration
- Durable governance audit storage
- LLM-based policy decisions
- Arbitrary policy scripting/expression languages
- Policy-result caching
- Cross-identity delegation or session ownership transfer
- Per-tool-call reauthorization after the pre-loop permitted tool set is resolved
