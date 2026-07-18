# Requirements

### Overview & Goals
Migrate the HaaS CLI from `Spectre.Console` to `Terminal.Gui` to provide a more robust and native TUI experience. This move specifically addresses the pain point of manual line-splitting and scrolling management by leveraging a formal UI framework.

### Scope
- **In Scope:**
    - Implementing a `Terminal.Gui` application loop.
    - Porting the Main Menu to a `ListView`-based window.
    - Porting the AI Chat to a multi-view layout (History, Input, Logs).
    - Porting Tic-Tac-Toe to a grid-based interactive view.
    - Redirecting all logs to a dedicated `TextView` in the UI.
- **Out of Scope:**
    - Retaining any `Spectre.Console` dependencies in the final version.
    - Adding new features beyond what was already implemented in Spectre.

### Functional Requirements
- The UI must maintain the split-screen layout (Main/Logs).
- Chat history must be scrollable using standard UI patterns (scrollbars/keys).
- The "Input" box must be a focused text field.
- The Tic-Tac-Toe board must be interactive (e.g., clicking or keying into cells).

# Technical Design

### Key Decisions
- **`GuiLayoutManager`**: Replace `CliLayoutManager`. It will manage the `Toplevel` application and the main/log view split using `Pos` and `Dim`.
- **`GuiLoggingProvider`**: Capture logs and append them to a `TextView` widget.
- **Asynchrony**: Use `Application.MainLoop.Invoke` or similar to update UI components from background threads (AI thinking).

### File Structure Changes
- `src/HaaS.Host.CLI/Infrastructure/`
    - `GuiLayoutManager.cs` (New)
    - `GuiLoggingProvider.cs` (New)
- Update all `ICliModule` implementations to work with the new UI loop.

# Delivery Steps

### ✓ Step 1: Initialize Terminal.Gui and Basic Layout
- Create the core `GuiLayoutManager` that starts the application loop.
- Implement the split-screen layout (Content pane on top, Logs pane on bottom).
- Implement a `GuiLoggingProvider` to route logs to the bottom pane.

### ✓ Step 2: Port Main Menu
- Refactor `CliMenu.cs` to use `Terminal.Gui` widgets (Window, ListView).
- Ensure the menu can launch the Chat and Tic-Tac-Toe modules.

### ✓ Step 3: Implement AI Chat View
- Create a `ChatView` with a scrollable history area and an input field.
- Ensure messages (User/Assistant) are added dynamically and the view follows the tail.
- Handle the "AI is thinking" state with a UI indicator.

### ✓ Step 4: Implement Tic-Tac-Toe View
- Create a `TicTacToeView` that renders the game board using `Button` or `Label` widgets.
- Ensure the game state updates correctly in the UI.

### ✓ Step 5: Final Cleanup and Spectre Removal
- Remove `Spectre.Console` package and any leftover code.
- Verify the solution builds and runs correctly.
