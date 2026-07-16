---
sessionId: session-260716-230726-1xo6
---

# Requirements

### Overview & Goals
The goal is to improve the Tic-Tac-Toe implementation by leveraging the recently added capability to register tools on instances resolved from the IoC container. This will result in cleaner code, better separation of concerns, and a great example of the framework's features.

### Scope
- **In Scope**:
    - Refactoring `TicTacToeGame` to include tool-specific logic.
    - Updating `TicTacToeModule` to use generic tool registration.
    - Transitioning from manual instance management to IoC-managed lifecycle for the game state.
- **Out of Scope**:
    - Changing the game rules or AI logic.
    - Modifying the UI or signal handling flow.


# Technical Design

### Current Implementation
- `TicTacToeModule` manually creates a `TicTacToeGame` instance.
- It registers tools using lambdas that close over this instance.
- `place_marker` logic is defined inside `TicTacToeModule` instead of the domain object.

### Proposed Changes
1.  **`TicTacToeGame` (Domain)**:
    - Add `PlaceMarker(int position)`:
        ```csharp
        public string PlaceMarker(int position)
        {
            if (HasMovedThisTurn)
                return "You have already placed your marker this turn. Wait for the next turn.";
            if (!TryPlace(position))
                return $"Position {position} is not available. Choose from: {FormatValidMoves()}.";
            return $"Placed O at position {position}. Your turn is over. Wait for the player to move before your next turn.";
        }
        ```
2.  **`TicTacToeModule` (Adapter/Host)**:
    - Update service registration:
        ```csharp
        services.AddSingleton<TicTacToeGame>();
        ```
    - Update tool registration:
        ```csharp
        var toolProvider = host.Services.GetRequiredService<IToolProvider>();
        toolProvider.Register<TicTacToeGame>("get_board", "...", g => (Func<string>)g.FormatBoard);
        toolProvider.Register<TicTacToeGame>("get_valid_moves", "...", g => (Func<string>)g.FormatValidMoves);
        toolProvider.Register<TicTacToeGame>("place_marker", "...", g => (Func<int, string>)g.PlaceMarker);
        ```

### File Structure
- `src/HaaS.Host.CLI/TicTacToeGame.cs`: Modified to add `PlaceMarker`.
- `src/HaaS.Host.CLI/TicTacToeModule.cs`: Refactored to use IoC registration.

### Risks
- **Service Lifetime**: Ensuring `TicTacToeGame` is correctly resolved as a singleton so both the signal source and tools share the same state.
- **Registration Timing**: Tools must be registered before the agent starts processing signals. The current pattern of registering after `host.Build()` but before `host.RunAsync()` is safe.


# Delivery Steps

### ✓ Step 1: Enhance TicTacToeGame with tool-compatible methods
Add a tool-friendly method to the game logic and ensure methods are suitable for tool registration.

- Add `PlaceMarker(int position)` method to `TicTacToeGame` that returns a `string` response, encapsulating the logic previously found in `TicTacToeModule`.
- Keep existing `FormatBoard` and `FormatValidMoves` methods as they are already suitable for tool calls.
- Ensure `TicTacToeGame` is properly structured to be resolved as a singleton.

### ✓ Step 2: Refactor TicTacToeModule to use IoC tool registration
Refactor the module to use the new IoC-based tool registration pattern.

- Remove the manual instantiation of `TicTacToeGame` and the local `_game` field.
- Register `TicTacToeGame` as a singleton in the `IServiceCollection` within `RunAsync`.
- Use `toolProvider.Register<TicTacToeGame>(...)` with appropriate method selectors and delegate casts for `get_board`, `get_valid_moves`, and `place_marker` tools.
- Ensure the registration happens after the host is built but before it starts, or via a hosted service (current implementation does it after `Build()` which is fine for this CLI module).