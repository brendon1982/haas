---
sessionId: session-260717-232835-dw0a
---

# Requirements

### Overview & Goals
The TicTacToe example in the `HaaS.Host.CLI` project is a key demonstration of how to use the HaaS framework for interactive, tool-using agent sessions. This plan aims to refactor it to be a "good readable example" that is clean, simple, and follows the project's architectural principles without being over-burdened with heavy infrastructure.

### Scope
- **In Scope:**
  - Refactoring of `TicTacToeGame`, `TicTacToeModule`, and `TicTacToeSignalSource` within the `HaaS.Host.CLI` project.
  - Organization of these files into a dedicated `TicTacToe` directory.
  - Separation of pure game logic from CLI/AI formatting.
  - Alignment of tests with project standards (TDD, SutBuilder, NExpect).
- **Out of Scope:**
  - Moving TicTacToe to `HaaS.Domain` or `HaaS.Application` (per user preference).
  - Adding complex features like persistence or multi-agent strategies to this example.


# Technical Design

### Current Implementation
Currently, TicTacToe is implemented as a set of classes in the root of `HaaS.Host.CLI`. `TicTacToeGame` contains both game logic and formatting for the AI. `TicTacToeSignalSource` handles the entire CLI game loop, including human input and board drawing, alongside HaaS signal production.

### Proposed Changes
The example will be refactored to demonstrate a clean separation of concerns:
- **`TicTacToeGame.cs`**: A pure logic class representing the game state and rules.
- **`TicTacToeSignalSource.cs`**: A HaaS `SignalSource` that acts as the CLI "adapter", handling human input/output and triggering the agent loop.
- **`TicTacToeModule.cs`**: The orchestrator that wires up the HaaS host, registers the game, and defines the tools the AI uses to play.

### File Structure
The files will be moved to:
- `src\HaaS.Host.CLI\TicTacToe\TicTacToeGame.cs`
- `src\HaaS.Host.CLI\TicTacToe\TicTacToeModule.cs`
- `src\HaaS.Host.CLI\TicTacToe\TicTacToeSignalSource.cs`
- `src\HaaS.Host.CLI.Tests\TicTacToe\TicTacToeGameTests.cs`

### Key Decisions
- **Separation of Formatting**: Formatting for the AI (e.g., "how the board looks in text") will be moved from the game logic to the tool registration layer. This shows how HaaS tools can adapt domain data for LLMs.
- **CLI Interaction**: The CLI board drawing and human move logic will stay in the `SignalSource`, as it serves as the primary interface for this "CLI" adapter.
- **Testing Patterns**: Tests will be updated to use the `SutBuilder` and `NExpect` patterns, providing a high-quality example of how to test HaaS components.


# Testing

### Validation Approach
I will verify the refactoring by ensuring that:
1. The project builds and the TicTacToe module can still be run.
2. All tests pass and are easier to read.
3. The AI still plays the game correctly using the refactored tools.

### Key Scenarios
- **Human Move**: Verify that a human move updates the game state and triggers an AI signal.
- **AI Move**: Verify that the AI can call `get_board`, `get_valid_moves`, and `place_marker` successfully.
- **Game End**: Verify that the game correctly detects win/draw states and terminates the session.


# Delivery Steps

### ✓ Step 1: Organize TicTacToe files into a dedicated directory
Move and organize TicTacToe files into a dedicated directory.

- Create `src\HaaS.Host.CLI\TicTacToe` directory.
- Move `TicTacToeGame.cs`, `TicTacToeModule.cs`, and `TicTacToeSignalSource.cs` into the new directory.
- Update namespaces and imports to reflect the new location.
- Ensure the project still builds after the move.

### ✓ Step 2: Refactor TicTacToeGame to focus on pure logic
Refactor TicTacToeGame to be a pure domain-style logic class.

- Remove all CLI and AI-facing string formatting logic (`FormatBoard`, `FormatValidMoves`, etc.).
- Ensure it only handles game state (board, turns) and rules (valid moves, winner detection).
- Use clean, descriptive method names (e.g., `PlaceMarker`, `GetWinner`, `IsValidMove`).
- Update the class to be a better example of a testable logic component.

### ✓ Step 3: Refactor TicTacToeModule and Tool registrations
Clean up TicTacToeModule and implement better tool registrations.

- Move tool registration logic into a dedicated method or class.
- Use the `ToolProvider` to register tools with clear, helpful descriptions for the AI.
- Implement tool handlers that do the necessary formatting for the AI's consumption (instead of having that logic in the game class).
- Simplify the HaaS host configuration in `RunAsync`.

### ✓ Step 4: Optimize TicTacToeSignalSource for readability
Simplify and improve TicTacToeSignalSource.

- Refactor the `ListenAsync` loop to be more readable.
- Move CLI drawing logic to a separate helper or presenter if it helps clarity.
- Ensure the signal production and handling are the focal points of the class.
- Add comments explaining how the `SignalSource` interacts with the HaaS engine.

### ✓ Step 5: Update and align tests with project standards
Update tests to follow project standards and verify the new structure.

- Move `TicTacToeGameTests.cs` to a matching directory in the test project.
- Update tests to use the `SutBuilder` pattern and `NExpect` as per `AGENTS.md`.
- Ensure tests focus on the core game logic and the new refactored state.
- Run all tests to verify the solution.