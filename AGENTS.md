# AGENTS.md — Enterprise AI Harness (HaaS)

## Project Overview

On-prem, customer-configurable enterprise AI harness. Routes inputs from multiple sources through a governed agent loop to produce outputs with full observability.

## Tech Stack

- **Runtime:** Node.js ESM
- **Language:** TypeScript (strict mode)
- **Agent orchestration:** `@earendil-works/pi-coding-agent`
- **Persistence:** SQLite (session, memory, registries, task tracking)
- **Package manager:** pnpm (v11.7+)
- **Testing:** Vitest

## Dev Values (in order)

1. **DDD** — Model the domain explicitly. Aggregates, entities, value objects, repositories, domain events. Keep persistence & frameworks in the infra layer.
2. **TDD** — Red-green-refactor. Tests drive every module. No production code without a failing test first.
3. **Test harnessing** — Builders, rich fakes, and test fixtures over mocks. Prefer state-based verification over interaction-based.
4. **Modular, simple, readable, maintainable** — Small files, obvious names, minimal indirection, no premature abstraction.

## Architecture Layering

```
src/
  domain/       # Entities, value objects, aggregates, domain services, repository ports
  application/  # Use cases / application services, DTOs, orchestrators
  adapter/      # Controllers, presenters, repo implementations, signal/execution/observability adapters
  infra/        # SQLite, logging, config, DI wiring, HTTP servers, etc.
```

Dependencies point **inward**: `adapter/` → `application/` → `domain/`. `infra/` wires everything together.

## Coding Conventions

### General

- Strict mode TypeScript. Prefer `interface` over `type` for object shapes. Use `zod` for runtime validation (already present via `pi-coding-agent` deps).
- Explicit exports — no `index.ts` barrel files (prevents circular deps, keeps imports obvious).
- Functions over classes unless the domain demands an aggregate/entity. Pure functions where possible.
- Errors are domain-value objects or tagged unions, not `Error` subclasses.
- Async: `Promise` always, no callbacks. Use `Result<T, E>` pattern for fallible operations instead of try/catch in domain/application layers.

### Naming

- Files: `kebab-case.ts` (e.g. `session-repository.ts`)
- Exports: PascalCase for types/interfaces, camelCase for functions/values
- Test files: `*.test.ts` or `*.spec.ts` colocated with source
- DDD: `CustomerId` (value object), `Customer` (entity), `CustomerRepository` (port), `SqliteCustomerRepository` (adapter)

### Testing

- File-per-module tests: `session-repository.test.ts` next to `session-repository.ts`
- Follow Given-When-Then assertions
- Use builders for complex domain objects (e.g. `aSession().withId(...).build()`)
- Fakes implement repository ports in-memory; use them in application-layer tests
- Integration tests go in `test/integration/` with a `.int.test.ts` suffix

## Extension Points (System Architecture)

The harness is extensible at these seams — every seam is an interface/port in `domain/`:

| Layer | Port | Adapters (examples) |
|-------|------|---------------------|
| **Signal/Input** | `SignalSource` | HTTP webhook, Slack, Kafka, CLI stdin, scheduled poller |
| **Execution/Output** | `ExecutionTarget` | stdout, Slack message, Jira ticket, email, PagerDuty |
| **Observability** | `ObservabilityProvider` | stdout logging, OpenTelemetry, DataDog, CloudWatch |
| **Multi-Agent** | `AgentStrategy` | Single-agent, supervisor+worker, swarm, router |
| **Knowledge** | `TaskStore`, `MemoryStore`, `RegistryStore` | SQLite, Postgres, in-memory |
| **Auth** | `AuthProvider` | JWT, OAuth2, API key, mTLS, passthrough |
| **Governance** | `PolicyEngine` | RBAC, ABAC, allow-list, deny-list, LLM-gated |

## Key Decisions (for agents)

- **SQLite** — bundled, zero-ops, good enough for on-prem single-tenant. Keep connection management simple (WAL mode, one writer).
- **`pi-coding-agent`** — runs agent loops. The harness wraps it with governance, auth, and observability around each loop iteration.
- **Auth flows through** — the identity established at the signal layer is propagated all the way to execution layer policies. Never re-authenticate mid-flow unless the policy engine demands it.
- **Governance is per-session** — a session carries the identity + signal source metadata; the policy engine checks permitted execution targets and tools before each action.

## Running Tests

```
pnpm test        # unit + integration
pnpm test:unit   # unit only
pnpm test:watch  # watch mode
```

## Understanding the Unfamiliar

Before editing a module, read its test file first — tests are the executable spec.
