# Implementation Considerations

## Tool Call Auth Injection via SDK Extension Events

### Problem

Custom external tools (Slack, Jira, email, PagerDuty, etc.) need session-level authentication — tokens that were resolved from the original signal, secrets fetched from Keycloak or a vault, or per-session credentials selected by policy.

The agent loop is managed by pi-coding-agent. It calls tools based on LLM decisions. Without interception, each tool would need to independently resolve auth — duplicating logic and violating the principle that auth flows through from the signal.

### SDK Mechanism: `tool_call` Extension Event

The pi-coding-agent SDK fires a `tool_call` extension event **before** every tool execution. The event provides:

- `event.input` — the tool parameters, **mutable in place** before execution
- `ToolCallEventResult` — can return `{ block: true, reason: "..." }` to reject the call

This is the hook for injecting session auth into custom tool calls.

### How It Works

1. **Registration** — An extension handler is registered for `"tool_call"` at session start (or built into the harness adapter that wraps `createAgentSession`):

```typescript
pi.on("tool_call", (event) => {
  // Only intercept tools that need auth
  if (event.toolName === "slack_post" || event.toolName === "jira_create") {
    event.input.authToken = session.authToken;
    event.input.headers = { Authorization: `Bearer ${session.jwt}` };
  }
  // No return value means "proceed"
});
```

2. **Enrichment** — The handler reads auth from the session context (originally resolved from the signal at ingress) and writes it into the tool's input parameters. The tool's `execute` function receives the enriched input.

3. **Blocking** — If auth is missing or expired, the handler returns `{ block: true, reason: "Session auth expired" }`. The agent loop sees the tool call was rejected and can report the error to the LLM or abort.

### What This Means for Tool Design

Custom tools should **not** resolve auth internally. They accept `authToken` (or similar) as a parameter, and the `tool_call` handler injects it. This keeps tools pure — they transform input to output, auth is injected by the harness.

```typescript
const slackPost = defineTool({
  name: "slack_post",
  parameters: Type.Object({
    channel: Type.String(),
    message: Type.String(),
    authToken: Type.Optional(Type.String()),  // injected by harness
  }),
  execute: async (_toolCallId, params) => {
    // Use params.authToken — already injected
    const response = await fetch("https://slack.com/api/chat.postMessage", {
      headers: { Authorization: `Bearer ${params.authToken}` },
      body: JSON.stringify({ channel: params.channel, text: params.message }),
    });
    return { content: [{ type: "text", text: "Posted" }], details: {} };
  },
});
```

### Scope and Boundaries

| Scope | Details |
|-------|---------|
| **Custom tools only** | Built-in tools (read, bash, edit, write, grep, find, ls) operate on local files and don't accept auth params. The interception handler should skip them. |
| **Session-level auth** | The handler has access to the session context. Auth is resolved once at signal ingress by `AuthProvider` and carried through the session. |
| **Tool-specific credentials** | If a tool needs a specific API key (e.g., a Jira token different from the Slack token), the handler can look up the right credential per `event.toolName` from a credential store configured at the session or signal level. |
| **No re-validation** | The SDK docs note: "Later `tool_call` handlers see earlier mutations. No re-validation is performed after mutation." Only trusted harness code should register `tool_call` handlers. |

### Where This Slot In

- **Adapter layer** — The `tool_call` handler is registered by the agent strategy adapter (`PiCodingAgentStrategy`) or a dedicated observability/auth adapter, wrapping `createAgentSession`.
- **Session context** — Auth data flows from the signal → session → tool_call handler. It never touches the domain layer — pure cross-cutting concern at the adapter boundary.
