---
sessionId: session-260722-184842-15dy
---

# Requirements

### Overview & Goals
The current instruction structure (primarily `AGENTS.md`) suffers from **Context Pollution** and **Discovery Gaps**. C#/.NET-specific rules are mixed into the global guide, while crucial frontend commands and modern framework norms (Angular 21+) are either missing or buried in sub-directories. This leads to agents applying the wrong patterns (e.g., .NET testing patterns to Angular) or using legacy syntax that is now deprecated in the project's cutting-edge stack.

The goal is to move to a **Hierarchical Instruction Model** where:
1. The **Root** defines the "What" and "Why" (Global Architecture, DDD, Philosophy).
2. The **Modules** define the "How" (Tech-specific Norms, Commands, Antipatterns).

### Scope
- **Root `AGENTS.md`**: Transition to a high-level router and architectural guide.
- **`library/AGENTS.md`**: (New) Centralized guide for the .NET backend.
- **`examples/web/client/AGENTS.md`**: (Update) Enhanced with commands and specific "Modernity Warnings".
- **Investigation Protocol**: Updated to mandate local context discovery.

### Functional Requirements
- **Technological Isolation**: Prevent .NET rules from leaking into Angular tasks and vice versa.
- **Visibility of Local Rules**: Ensure local `AGENTS.md` files are discovered and read before any code modification.
- **Modernity Enforcement**: Explicitly instruct agents to ignore legacy training data in favor of project-defined modern norms (e.g., Angular Signals, `inject()`, .NET 9 features).
- **Accuracy**: Remove references to missing files (like `pnpm-workspace.yaml`) and include missing commands.

# Technical Design

### Current Implementation
- The root `AGENTS.md` is heavily biased towards .NET (NExpect, NUnit, C# records).
- Local guidelines are linked at the very bottom and easily missed during initial investigation.
- No central "Source of Truth" for the tech stack and versions across the whole project.
- Instructions reference non-existent files (`pnpm-workspace.yaml`).

### Proposed Changes

#### 1. Hierarchical Reorganization
Move from a "flat" instruction set to a "Cascading" one:
- **Global AGENTS.md**: Architecture (DDD 4-layer), Project-wide Philosophy (TDD, Thin Slices), and a **Tech Stack Matrix**.
- **Backend AGENTS.md** (`library/`): .NET 9 norms, NUnit/NExpect, Repository Ports, EF Core.
- **Frontend AGENTS.md** (`examples/web/client/`): Angular 21, Signals, Functional services, Accessibility.

#### 2. The Tech Stack Matrix
Add a table to the root `AGENTS.md` that maps directories to their technology:
 Path | Stack | Core Patterns | Local Guide |
------|-------|---------------|-------------|
 `library/` | .NET 9, EF Core | DDD, Ports/Adapters | `library/AGENTS.md` |
 `examples/web/` | Angular 21, Tailwind | Signals, Standalone, Functional | `examples/web/client/AGENTS.md` |

#### 3. "Modernity First" Protocol
Explicitly add a section to all instructions:
> **CRITICAL: MODERN NORMS ONLY.** This project uses cutting-edge versions (Angular 21, .NET 9). If your training data suggests a pattern (e.g., `constructor` injection, `standalone: true` decorator, `NgModules`), but the project guidelines or existing code show a newer functional pattern (`inject()`, default standalone), **the project wins**. Never "revert" code to legacy patterns.

#### 4. Updated Investigation Order
1. `SYSTEM-DESIGN.md` (Architecture)
2. `AGENTS.md` (Root - Tech Stack Matrix)
3. **Local `AGENTS.md`** of the target module (Norms & Commands)
4. Build/Test files (`library/haas.sln` or `package.json`)
5. Sister test files (for TDD context)

#### 5. Common Antipatterns Catalog
Each `AGENTS.md` should have a "DO NOT" section based on recent corrections:
- **Angular**: "DO NOT use `standalone: true` (it is the default)", "DO NOT use `ngClass`", "DO NOT use `constructor` injection".
- **General**: "DO NOT guess commands; check the local `AGENTS.md` or `package.json` first".

### File Structure
- `AGENTS.md` (Refactored)
- `library/AGENTS.md` (New)
- `examples/web/client/AGENTS.md` (Refactored)

# Delivery Steps

### ✓ Step 1: Design the Hierarchical Instruction Model and Tech Stack Matrix
A "Global Policy" vs. "Regional Policy" structure will be designed.
- Move C#/.NET specific rules from the root `AGENTS.md` to a new `library/AGENTS.md`.
- Keep the root `AGENTS.md` focused on cross-cutting concerns: DDD architecture, project-wide dev philosophy, and the "Module Map".
- Create a Tech Stack Matrix in the root file that explicitly lists versions and paths for each major subsystem.

### ✓ Step 2: Draft Modernity Protocols and Update Investigation Order
The root `AGENTS.md` and local files will be updated with better discovery and modernity rules.
- Add a "Modernity First" protocol to the root file, instructing agents to prioritize local project norms over legacy training data for frameworks like Angular 20+ and .NET 9.
- Update the "Investigation Order" to include mandatory reading of local `AGENTS.md` files.
- Add "Common Antipatterns" sections to each instruction file to explicitly forbid common LLM mistakes (e.g., legacy Angular decorators).

### ✓ Step 3: Reconcile Instructions with Project Reality and Finalize Commands
All instruction files will be reconciled with the actual project state.
- Fix errors in the root `AGENTS.md` (e.g., removing references to non-existent `pnpm-workspace.yaml`).
- Ensure all relevant commands (pnpm, ng, dotnet) are documented in the appropriate guideline files.
- Review `examples/web/client/AGENTS.md` to ensure it covers the specific Vitest/Testing nuances found in the code.