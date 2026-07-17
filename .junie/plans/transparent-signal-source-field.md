---
sessionId: session-260717-223639-9t6q
---

# Requirements

### Overview & Goals
The goal is to prevent implementers of `ISignalSource` from providing manual (and potentially incorrect) values for the `Source` field in the `Signal` record. We will achieve this by separating the signal model into an internal `Signal` (with `Source`) and an external `IncomingSignal` (without `Source`).

### Scope
- **In Scope:**
    - Introducing `IncomingSignal` record in the Domain layer for use by adapters.
    - Modifying `ISignalSource` to use `IncomingSignal`.
    - Updating `BaseHaasEngine` to automatically inject the correct source type when converting `IncomingSignal` to `Signal`.
    - Cleaning up all existing `ISignalSource` implementations to remove redundant source assignments.
- **Out of Scope:**
    - Changing the internal `Signal` processing logic in Use Cases or Workers.
    - Modifying the persistence layer (since it already expects a full `Signal`).

### User Stories
- **As a Developer implementing a new Signal Source**, I want to only provide the payload and relevant context (like SessionId) without worrying about the internal "source type" identifier, so that my implementation is simpler and less error-prone.
- **As a System Maintainer**, I want the source of a signal to be guaranteed by the engine, so that I can rely on it for logging, routing, and policy enforcement.

# Technical Design

### Current Implementation
- `Signal` record requires `Source` as the second parameter in its primary constructor.
- `ISignalSource` implementations like `ChatSignalSource`, `CliSignalSource`, and `TicTacToeSignalSource` manually pass a string literal matching their `Type` property to the `Signal` constructor.
- `BaseHaasEngine` sets up the listening loop but doesn't currently touch the `Source` field of incoming signals.

### Proposed Changes
#### 1. Domain Model Split
We will introduce `IncomingSignal` to represent the data provided by the source. The existing `Signal` will be kept as the internal representation.

```csharp
// src/HaaS.Domain/ValueObjects/IncomingSignal.cs (New)
public record IncomingSignal(
    string Payload, 
    string? SessionId = null, 
    DateTimeOffset? ArrivedAt = null);

// src/HaaS.Domain/ValueObjects/Signal.cs (Keep as is)
public record Signal(
    string Payload, 
    string Source, 
    string? SessionId = null, 
    DateTimeOffset? ArrivedAt = null);
```

#### 2. Port Update
Update `ISignalSource` to use the new input model.

```csharp
// src/HaaS.Domain/Ports/ISignalSource.cs
public interface ISignalSource
{
    string Type { get; }
    Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler);
    Task ShutdownAsync();
}
```

#### 3. Engine Logic
`BaseHaasEngine` will bridge the two models. It knows the `Type` of the source from the registration.

```csharp
// src/HaaS.Infrastructure/BaseHaasEngine.cs
protected async Task RunSourceAsync(SignalSourceRegistration reg, CancellationToken ct)
{
    await reg.Source.ListenAsync(async incoming =>
    {
        // Bridge IncomingSignal to internal Signal, injecting the correct Source
        var signal = new Signal(
            incoming.Payload,
            reg.Source.Type,
            incoming.SessionId ?? reg.LastSessionId?.ToString(),
            incoming.ArrivedAt
        );

        var handle = await ProcessSignalAsync(signal, reg);
        
        if (Guid.TryParse(handle.SessionId, out var guid))
        {
            reg.LastSessionId = guid;
        }

        return handle;
    });
}
```

#### 4. Adapter Layer Cleanup
All implementers will be updated to use `IncomingSignal`.

```csharp
// Example: ChatSignalSource.cs
var handle = await handler(new IncomingSignal(line.Trim()));
```

### File Structure
- `src/HaaS.Domain/ValueObjects/IncomingSignal.cs` (Added)
- `src/HaaS.Domain/Ports/ISignalSource.cs` (Modified)
- `src/HaaS.Infrastructure/BaseHaasEngine.cs` (Modified)
- `src/HaaS.Host.CLI/ChatSignalSource.cs` (Modified)
- `src/HaaS.Host.CLI/CliSignalSource.cs` (Modified)
- `src/HaaS.Host.CLI/TicTacToeSignalSource.cs` (Modified)
- `src/HaaS.Infrastructure.Tests/MachineryIntegrationTests.cs` (Modified)
- `src/HaaS.Infrastructure.Tests/ServiceCollectionExtensionsTests.cs` (Modified)

# Testing

### Validation Approach
We will verify that:
1. Signal sources compile and run without providing a `Source` string.
2. The `BaseHaasEngine` correctly populates the `Source` field in the internal `Signal` object.
3. Integration tests correctly route signals from a `ManualSignalSource` using the new model.

### Key Scenarios
- **Direct Engine Flow:** Verify `CliSignalSource` → `DirectHaasEngine` → `RunSessionUseCase` correctly receives the `"cli"` source.
- **Queued Engine Flow:** Verify `ChatSignalSource` → `QueuedHaasEngine` → `SignalWorker` correctly receives the `"chat"` source.
- **Sticky Session:** Verify that if an `IncomingSignal` has no `SessionId`, it inherits the `LastSessionId` from the registration, and this is correctly passed to the internal `Signal`.

# Delivery Steps

### ✓ Step 1: Introduce IncomingSignal and update ISignalSource
Create the new record for external signals and update the port interface to use it.

- Create `src/HaaS.Domain/ValueObjects/IncomingSignal.cs`.
- Update `src/HaaS.Domain/Ports/ISignalSource.cs` to change the `handler` signature in `ListenAsync`.

### ✓ Step 2: Update BaseHaasEngine to bridge signal models
Implement the logic to convert `IncomingSignal` to `Signal` while injecting the source type.

- Modify `src/HaaS.Infrastructure/BaseHaasEngine.cs` in the `RunSourceAsync` method.
- Construct a new `Signal` using the incoming data and `reg.Source.Type`.

### ✓ Step 3: Refactor Adapters and Tests
Update all signal source implementations and tests to use the new `IncomingSignal` model.

- Update `ChatSignalSource.cs`, `CliSignalSource.cs`, and `TicTacToeSignalSource.cs` in `src/HaaS.Host.CLI/`.
- Update `MachineryIntegrationTests.cs` and `ServiceCollectionExtensionsTests.cs` in `src/HaaS.Infrastructure.Tests/`.