# Implementation Considerations

## Scoped tool context

Authentication and source policy attributes enter HaaS with `IncomingSignal.Context`.
`SignalContext` contains an `AuthenticationContext` and immutable source attributes;
it is separate from `Signal`, whose payload is the only user content supplied to an
agent.

`RunSessionUseCase` creates a `SignalExecutionContext` for each execution after it
has resolved the session and authorization. The context contains the session ID,
source, current authentication context, and source attributes. It deliberately
does not contain the payload or conversation history.

Tools that need session or caller information receive `ISignalContextAccessor` by
constructor injection:

```csharp
public sealed class IncidentTool(ISignalContextAccessor signalContext)
{
    public Task<string> CreateAsync(string title)
    {
        var execution = signalContext.Current;
        var caller = execution.Authentication.Identity;
        // Use caller identity and source attributes to select an injected service.
        return Task.FromResult($"Created incident for {caller.Key}: {title}");
    }
}
```

`Current` throws when a tool runs outside a signal execution. A tool must not
receive `SignalContext` as an LLM-visible method parameter: constructor injection
keeps authentication, claims, attributes, and credential references out of tool
schemas and model messages.

`ISignalContextScope` is an application-facing initializer. The scoped
infrastructure implementation backs both it and `ISignalContextAccessor`; only
`RunSessionUseCase` should push and dispose the context. `ISignalScopeAccessor`
continues to provide the current DI scope to `ToolProvider` so registered tool
handlers are resolved from the same signal scope.

## Credential references

`CredentialReference(Name, Provider, Reference)` is an opaque lookup handle, not a
secret container. Ingress adapters may attach references to
`AuthenticationContext`; they must never attach access tokens, API keys,
passwords, or resolved secret values.

Tools needing a provider-specific credential should inject the source- or
vault-specific resolver that owns that provider. The tool uses the current
execution context to select the appropriate opaque reference and passes it to the
resolver. HaaS intentionally does not define a common credential-vault port or
transport resolved secrets through signals, queues, sessions, policy requests,
tool arguments, logs, or persisted chat messages.

## Policy boundary

Policies are evaluated before the agent loop. `SessionStart` is evaluated for
every new or resumed signal. `ToolResolution` is evaluated once for each tool in
the configured session tool belt; only allowed configured tools are passed in
`AgentExecutionRequest`. A tool handler therefore consumes the scoped context
without adding an authorization parameter or reauthorizing each individual tool
call.

Policy requests contain only the session ID, gate, source, current identity,
current attributes, and an optional candidate tool name. They exclude payload,
history, authentication method, and credential references. Governance decisions
and logs likewise use safe rule and condition-category metadata rather than claim
or attribute values.
