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

- `dotnet test` — runs all tests
- `dotnet build` — compiles solution
- `dotnet run --project src_csharp/haas/HaaS.Host.CLI` — runs the CLI host
- Install packages with `dotnet add package ...`

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

## Dev approach

- **TDD** — Red-green-refactor. Tests drive every module. No production code without a failing test first.
- **Thin vertical slices** — Every feature cuts through all four layers (domain → application → adapter → infra) in one small, end-to-end increment. No deep layer-by-layer builds.
- **DDD** — Model the domain explicitly. Keep persistence and frameworks in the infra layer.
- **Test structure** — Every test body starts with `// Arrange`, `// Act`, `// Assert` comments separating the three phases. Before writing tests, analyze all boundaries and equivalence partitions, then write one test per case.
- **Arrange only what matters** — Arrange sections should contain only setup relevant to the test scenario. If a value isn't varied or asserted, rely on the builder's default. Builders provide sensible defaults so tests don't have to repeat boilerplate.
- **No magic strings in assertions** — Assertions should reference values from arrange instances (e.g., `signal.Payload`, `expected.SessionId`) rather than repeating string literals. Where a value isn't accessible from an instance, create an explicit `expected*` variable in arrange and use it both when configuring the SUT/builder and in the assertion. This keeps the link between cause and effect visible in a single named variable.

## Coding conventions

- **General:** Functions over classes (unless aggregate/entity requires state). Prefer `interface` over `type` for object shapes. Async via `Task` always, no callbacks.
- **Naming:** PascalCase file names, PascalCase types, PascalCase methods. Test files `*Tests.cs` in a matching test project.
- **Imports:** No barrel files — explicit `using` statements per file (prevents circular deps).
- **Tests:** Every test body starts with `// Arrange`, `// Act`, `// Assert` comments separating the three phases. Before writing tests, analyze all boundaries and equivalence partitions, then write one test per case.
- **Tests must not share state:** No `static` or `static readonly` fields shared across tests. Each test gets a fresh SUT.
- **SUT builder per test class:** Every `[TestFixture]` must have a `SutBuilder` (private `file sealed` class at the bottom of the file) that creates the SUT with all necessary default dependencies. The builder provides `With*` methods for tests to supply specific dependencies when relevant. The builder's `Build()` method returns only the SUT — default dependencies are encapsulated and not exposed to tests. Tests that need a non-default dependency create it themselves and pass it via a `With*` method.
- **Builders:** Use builder classes suffixed with `TestBuilder` for domain object creation. Private constructor, static `Create()` method, stacked `With*` methods for configuration, sensible defaults. Assign builder results to variables before use (no inline chaining). Shared builders live in the test project corresponding to the layer, one file per builder. Builder used only in one test file stays at the bottom of that file.

## Extension points (ports in `domain/`)

| Layer | Port | Adapters (examples) |
|-------|------|---------------------|
| **Signal/Input** | `SignalSource` | HTTP webhook, Slack, Kafka, CLI stdin, scheduled poller |
| **Execution/Output** | `ExecutionTarget` | stdout, Slack message, Jira ticket, email, PagerDuty |
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
