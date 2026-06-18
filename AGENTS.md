# AGENTS.md — HaaS (Enterprise AI Harness)

## Project state

Greenfield. `src/` is empty — no TypeScript config, no test framework, no code yet. `SYSTEM-DESIGN.md` is the authoritative architecture plan; follow it when writing code.

## Stack

- **Runtime:** Node.js ESM (`"type": "module"` in package.json)
- **Language:** TypeScript (strict mode — add `tsconfig.json` before writing code)
- **Package manager:** pnpm v11.7+ (match `devEngines` in package.json)
- **Agent orchestration:** `@earendil-works/pi-coding-agent` ^0.79.6 (SDK — use `createAgentSession` etc.)
- **Schema validation:** pi depends on `typebox` for tool param schemas; zod is suggested in SYSTEM-DESIGN.md for domain validation
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
adapter/ → application/ → domain/
infra/    wires everything together
```

- No barrel files — explicit relative imports per file (prevents circular deps)
- pi-coding-agent SDK wraps the agent loop; harness adds governance, auth, and observability around each iteration

## Coding conventions (design intent)

- Functions over classes (unless aggregate/entity requires state)
- `Result<T, E>` for fallible operations; no try/catch in domain/application layers
- Domain errors as tagged unions, not `Error` subclasses
- `kebab-case.ts` file names, PascalCase types, camelCase functions/values
- Builders and fakes over mocks; state-based verification over interaction-based

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

SignalSource, ExecutionTarget, ObservabilityProvider, AgentStrategy,
TaskStore/MemoryStore/RegistryStore, AuthProvider, PolicyEngine.

## Investigation order

1. `SYSTEM-DESIGN.md` — canonical architecture
2. `package.json`, `pnpm-workspace.yaml`, `.gitignore`
3. Before editing a module, read its sister test file first
