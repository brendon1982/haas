# AGENTS.md — HaaS (Enterprise AI Harness)

## Project

On-prem, customer-configurable enterprise AI harness. Routes inputs from multiple sources through a governed agent loop to produce outputs with full observability.

## Stack

- **Runtime:** .NET 9.0+
- **Language:** C#
- **Agent orchestration:** `Microsoft.Agents.AI` v1.10.0 (Microsoft Agent Framework)
- **Schema validation:** native C# records + attributes
- **Testing:** NUnit
- **Persistence:** EF Core + SQLite (design intent, not implemented)

## Commands

- `dotnet build library\haas.sln` — compiles the full solution
- `dotnet test library\haas.sln` — runs all tests
- `dotnet run --project examples\CLI\HaaS.Host.CLI` — runs the CLI host
- Install packages with `dotnet add package ...`

> **Verification:** Always build and test against the **solution file** (`library\haas.sln`), not individual projects. Building individual projects can miss cross-project reference breaks (e.g. changed constructor signatures in `HaaS.Adapters` that break callers in `HaaS.Host.CLI`).

## Architecture (from SYSTEM-DESIGN.md)

DDD 4-layer, dependencies point inward:

```
library/
  HaaS.Domain/         # Entities, value objects, aggregates, domain services, repository ports
  HaaS.Application/    # Use cases / application services, DTOs, orchestrators
  HaaS.Adapters/       # Controllers, presenters, repo implementations, signal/execution/observability adapters
  HaaS.Infrastructure/ # SQLite, logging, config, DI wiring, HTTP servers, etc.
```

Dependencies point **inward**: `adapter/` → `application/` → `domain/`. `infra/` wires everything together.

## Dev approach

- **TDD** — Red-green-refactor.
- **Thin vertical slices** — Every feature cuts through all four layers (domain → application → adapter → infra) in one small, end-to-end increment. No deep layer-by-layer builds.
- **DDD** — Model the domain explicitly. Keep persistence and frameworks in the infra layer.

## Testing

### Philosophy
- **TDD** — Red-green-refactor. Tests drive every module. No production code without a failing test first.
- **Thin vertical slices** — Every feature cuts through all four layers.
- **Pass/Fail for the right reason** — Avoid tests that pass regardless of whether the logic under test is actually executed. Verify side effects, not just return values.
  *Example: When testing a decorator that logs, assert that the logger was actually called with the expected parameters, not just that the decorated method returned a value.*
- **Comprehensive coverage via analysis** — Design test suites by analyzing boundaries and equivalence partitions of the problem space to ensure all behaviors are covered. Write one test per identified case.

### Structure
- **Triple-A (Arrange, Act, Assert)** — Every test body starts with `// Arrange`, `// Act`, `// Assert` comments.
- **SutBuilder per test class** — Use a private `file sealed class SutBuilder` at the bottom of the file. It encapsulates default dependencies and provides `With*` methods for customization. `Build()` returns only the SUT.
- **No manual model instantiation** — Tests must never manually `new` up data models, value types, or complex objects. Always use `*TestBuilder` classes or the `SutBuilder`.
- **Builders for Domain Objects** — Use `*TestBuilder` classes for domain entities/records. Shared builders live in the corresponding test project; one-off builders stay at the bottom of the test file.
- **Manual Fakes over Mocks** — Prefer `file sealed` fake implementations of ports over mocking libraries (NSubstitute/Moq). This keeps dependencies explicit and avoids library "magic".
- **No shared state** — Tests must not share state via `static` fields. Each test gets a fresh SUT.
- **Arrange only what matters** — Arrange sections should contain only setup relevant to the scenario. Rely on builder defaults for everything else.

### Assertions (NExpect)
- Use fluent `Expect(actual).To.Equal(expected)` syntax. See [`USING_NEXPECT.md`](USING_NEXPECT.md) for setup, working syntax patterns, and common pitfalls.
- **No magic strings** — Assert against Arrange variables, not hardcoded literals. Create `expected*` variables if needed to keep the link between cause and effect visible.
- **Value objects are records, not SUT** — Don't test simple records unless they contain custom logic.
- **Explicit "No Throw" Assertions** — When a test's purpose is to verify that no exception is thrown, use `Expect(fn).Not.To.Throw()` explicitly instead of relying on a successful execution or comments.

## Coding conventions

- **General:** Functions over classes (unless aggregate/entity requires state). Prefer `interface` over `type` for object shapes. Async via `Task` always, no callbacks.
- **Naming:** PascalCase file names, PascalCase types, PascalCase methods. Test files `*Tests.cs` in a matching test project.
- **Imports:** No barrel files — explicit `using` statements per file (prevents circular deps).

## Extension points (ports in `domain/`)

| Layer | Port | Adapters (examples) |
|-------|------|---------------------|
| **Signal/Input** | `SignalSource` | HTTP webhook, Slack, Kafka, CLI stdin, scheduled poller |
| **Execution/Output** | *(tools handle output via `ToolBelt` + `ChatToolMode`)* | `reply_to_user`, Slack message, Jira ticket, email |
| **Observability** | `ObservabilityProvider` | stdout logging, OpenTelemetry, DataDog, CloudWatch |
| **Multi-Agent** | `AgentStrategy` | Single-agent, supervisor+worker, swarm, router |
| **Configuration** | `ConfigRepository` | YAML file, SQLite |
| **Knowledge** | `TaskStore`, `MemoryStore`, `RegistryStore` | SQLite, Postgres, in-memory |
| **Auth** | `AuthProvider` | JWT, OAuth2, API key, mTLS, passthrough |
| **Governance** | `PolicyEngine` | RBAC, ABAC, allow-list, deny-list, LLM-gated |

## Investigation order

1. `SYSTEM-DESIGN.md` — canonical architecture
2. `package.json`, `pnpm-workspace.yaml`, `.gitignore`
3. Before editing a module, read its sister test file first
