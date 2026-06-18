# AGENTS.md — HaaS (Enterprise AI Harness)

## Project state

Greenfield. `src/` is empty — no TypeScript config, no test framework, no code yet. `SYSTEM-DESIGN.md` is the authoritative architecture plan; follow it when writing code.

## Stack

- **Runtime:** Node.js ESM (`"type": "module"` in package.json)
- **Language:** TypeScript (strict mode — add `tsconfig.json` before writing code)
- **Package manager:** pnpm v11.7+ (match `devEngines` in package.json)
- **Agent orchestration:** `@earendil-works/pi-coding-agent` ^0.79.6 (SDK — use `createAgentSession` etc.)
- **Schema validation:** `typebox` for JSON schema (tool params, domain DTOs, config)
- **Testing:** Vitest
- **Persistence:** SQLite with WAL mode (design intent, not implemented)

## Commands

- `pnpm test` — vitest
- `pnpm typecheck` — `tsc --noEmit`
- `pnpm lint` — biome check
- Install deps with `pnpm add ...`

## Architecture (from SYSTEM-DESIGN.md)

DDD 4-layer, dependencies point inward:

```
src/
  domain/       # Entities, value objects, aggregates, domain services, repository ports
  application/  # Use cases / application services, DTOs, orchestrators
  adapter/      # Controllers, presenters, repo implementations, signal/execution/observability adapters
  infra/        # SQLite, logging, config, DI wiring, HTTP servers, etc.
```

Dependencies point **inward**: `adapter/` → `application/` → `domain/`. `infra/` wires everything together.

- No barrel files — explicit relative imports per file (prevents circular deps)
- pi-coding-agent SDK wraps the agent loop; harness adds governance, auth, and observability around each iteration

## Dev approach

- **TDD** — Red-green-refactor. Tests drive every module. No production code without a failing test first.
- **DDD** — Model the domain explicitly. Keep persistence and frameworks in the infra layer.

## Coding conventions (design intent)

- **General:** Functions over classes (unless aggregate/entity requires state). Prefer `interface` over `type` for object shapes. Async via `Promise` always, no callbacks.
- **Errors:** `Result<T, E>` for fallible operations; no try/catch in domain/application layers. Domain errors as tagged unions, not `Error` subclasses.
- **Naming:** `kebab-case.ts` file names, PascalCase types, camelCase functions/values. Test files `*.test.ts` colocated with source.
- **Tests:** Builders and fakes over mocks; state-based verification over interaction-based. Integration tests in `test/integration/` with `.int.test.ts` suffix.

## pi-coding-agent SDK

Key imports (see `docs/sdk.md` in the package for full API):

```typescript
import {
  createAgentSession, defineTool, SessionManager,
  DefaultResourceLoader, AuthStorage, ModelRegistry,
} from "@earendil-works/pi-coding-agent";
```

Before writing integration code, read `node_modules/@earendil-works/pi-coding-agent/docs/sdk.md`.

## Extension points (ports in `domain/`)

| Layer | Port | Adapters (examples) |
|-------|------|---------------------|
| **Signal/Input** | `SignalSource` | HTTP webhook, Slack, Kafka, CLI stdin, scheduled poller |
| **Execution/Output** | `ExecutionTarget` | stdout, Slack message, Jira ticket, email, PagerDuty |
| **Observability** | `ObservabilityProvider` | stdout logging, OpenTelemetry, DataDog, CloudWatch |
| **Multi-Agent** | `AgentStrategy` | Single-agent, supervisor+worker, swarm, router |
| **Knowledge** | `TaskStore`, `MemoryStore`, `RegistryStore` | SQLite, Postgres, in-memory |
| **Auth** | `AuthProvider` | JWT, OAuth2, API key, mTLS, passthrough |
| **Governance** | `PolicyEngine` | RBAC, ABAC, allow-list, deny-list, LLM-gated |

## Investigation order

1. `SYSTEM-DESIGN.md` — canonical architecture
2. `package.json`, `pnpm-workspace.yaml`, `.gitignore`
3. Before editing a module, read its sister test file first
