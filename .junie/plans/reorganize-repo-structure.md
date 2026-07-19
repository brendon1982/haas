---
sessionId: session-260719-162112-1itb
---

# Requirements

### Overview & Goals
The goal is to reorganize the repository to better accommodate a growing number of examples (starting with CLI and adding Web later) by separating the core HaaS library from example implementations.

### Scope
- **In Scope:**
  - Moving core library projects to a `library/` folder.
  - Moving the CLI host and tests to an `examples/CLI/` folder.
  - Updating solution files and project references to reflect the new structure.
  - Updating `AGENTS.md` and `SYSTEM-DESIGN.md` documentation.
- **Out of Scope:**
  - Creating the actual web examples (this will happen in a later task).
  - Changing any project logic or functionality.


# Technical Design

### Current Implementation
The project currently has all projects under a single `src/` directory, managed by a single `haas.sln` solution file.

### Proposed Changes
#### Directory Structure
```
haas/
├── library/                 # Core HaaS projects
│   ├── HaaS.Domain/
│   ├── HaaS.Application/
│   ├── HaaS.Adapters/
│   ├── HaaS.Infrastructure/
│   └── haas.sln             # Solution for the library
├── examples/                # Example implementations
│   └── CLI/
│       ├── HaaS.Host.CLI/
│       └── haas-cli.sln     # Solution for the CLI example
└── AGENTS.md, SYSTEM-DESIGN.md, etc.
```

#### Solution Management
- **Library Solution (`library/haas.sln`)**: Will contain only the 8 core projects (Domain, Application, Adapters, Infrastructure and their tests).
- **CLI Solution (`examples/CLI/haas-cli.sln`)**: Will contain `HaaS.Host.CLI` and `HaaS.Host.CLI.Tests`, and will reference the necessary library projects.

#### Project References
Relative paths in `HaaS.Host.CLI.csproj` and `HaaS.Host.CLI.Tests.csproj` will be updated from `..\` to `..\..\..\library\`.

#### Documentation Updates
- `AGENTS.md`: Update build/test commands to use `library\haas.sln` and run commands to use the new CLI path. Fix the existing typo `src_csharp/haas/`.
- `SYSTEM-DESIGN.md`: Update paths in the Layer Definitions section.

### Risks
- **Broken References**: Moving projects can break relative paths. I will verify and fix all `<ProjectReference>` elements in the moved project files.
- **Build Scripts**: Any existing build scripts or CI configurations (not visible in the root) might need updates.


# Delivery Steps

### ✓ Step 1: Reorganize Core Library Projects
Reorganize the core HaaS library projects into a dedicated directory.
- Create the `library` directory in the project root.
- Move `HaaS.Domain`, `HaaS.Domain.Tests`, `HaaS.Application`, `HaaS.Application.Tests`, `HaaS.Adapters`, `HaaS.Adapters.Tests`, `HaaS.Infrastructure`, and `HaaS.Infrastructure.Tests` from `src` to `library/`.
- Move `src/haas.sln` to `library/haas.sln`.
- Update `library/haas.sln` to only include the core library projects and fix their relative paths.
- Move `src/.junie/plans/simplify-haas-engine-usage.md` to `.junie/plans/`.

### ✓ Step 2: Reorganize CLI Example and Update References
Reorganize the CLI example into the examples directory and set up its own solution.
- Create the `examples/CLI` directory.
- Move `HaaS.Host.CLI` and `HaaS.Host.CLI.Tests` from `src` to `examples/CLI/`.
- Update `HaaS.Host.CLI.csproj` and `HaaS.Host.CLI.Tests.csproj` to reference library projects at their new relative paths (e.g., `../../../library/HaaS.Infrastructure/HaaS.Infrastructure.csproj`).
- Create a new solution `examples/CLI/haas-cli.sln` and add the CLI projects to it.
- Remove the now-empty `src` directory. (Note: directory currently locked, will retry later)

### ✓ Step 3: Update Documentation and Verify Build
Update the project documentation to reflect the new repository structure.
- Update `AGENTS.md` to use the new solution and project paths in commands and architecture descriptions.
- Update `SYSTEM-DESIGN.md` to reflect the new paths for the domain, application, adapter, and infrastructure layers.
- Verify that the solutions build and tests pass.