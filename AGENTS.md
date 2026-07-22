# AGENTS.md — HaaS (Enterprise AI Harness)

## Project Overview

On-prem, customer-configurable enterprise AI harness. Routes inputs from multiple sources through a governed agent loop to produce outputs with full observability.

## Tech Stack Matrix

| Path | Stack | Core Patterns | Local Guide |
|------|-------|---------------|-------------|
| `library/` | .NET 9, EF Core | DDD, Ports/Adapters | `library/AGENTS.md` |
| `examples/web/client/` | Angular 21, Tailwind | Signals, Standalone, Functional | `examples/web/client/AGENTS.md` |

## Architecture (High-Level)

DDD 4-layer, dependencies point inward:

```
library/
  HaaS.Domain/         # Entities, value objects, aggregates, domain services, repository ports
  HaaS.Application/    # Use cases / application services, DTOs, orchestrators
  HaaS.Adapters/       # Controllers, presenters, repo implementations, signal/execution/observability adapters
  HaaS.Infrastructure/ # SQLite, logging, config, DI wiring, HTTP servers, etc.
```

Dependencies point **inward**: `adapter/` → `application/` → `domain/`. `infra/` wires everything together.

## Development Philosophy

- **TDD (Red-Green-Refactor)**: No production code without a failing test first.
- **Thin Vertical Slices**: Every feature cuts through all four layers (domain → application → adapter → infra) in one small, end-to-end increment.
- **DDD**: Model the domain explicitly. Keep persistence and frameworks in the infra layer.

## Modernity First Protocol

**CRITICAL: MODERN NORMS ONLY.** This project uses cutting-edge versions (Angular 21, .NET 9). If your training data suggests a pattern (e.g., `constructor` injection in Angular, `standalone: true` decorator, `NgModules`), but the project guidelines or existing code show a newer functional pattern (`inject()`, default standalone), **the project wins**. Never "revert" code to legacy patterns.

## Investigation Order

1. `SYSTEM-DESIGN.md` — Canonical architecture.
2. `AGENTS.md` — Global Tech Stack Matrix.
3. **Local `AGENTS.md`** of the target module (e.g., `library/AGENTS.md` or `examples/web/client/AGENTS.md`).
4. `package.json` or `library/haas.sln` — Build/Test configuration.
5. Sister test files — TDD context.

## Module-Specific Guidelines

Some modules have their own `AGENTS.md` with specialized rules. Always check for and follow these local guidelines when working in these directories:

- `library/AGENTS.md` — Backend (.NET 9, DDD, NUnit).
- `examples/web/client/AGENTS.md` — Frontend (Angular 21, Signals, Vitest).
