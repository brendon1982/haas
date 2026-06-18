import type { SessionResult } from "../domain/value-objects/session-result.js";

export class SessionResultTestBuilder {
  private output = "default output";
  private sessionId = "sess-default";

  private constructor() {}

  static create(): SessionResultTestBuilder {
    return new SessionResultTestBuilder();
  }

  withOutput(output: string): SessionResultTestBuilder {
    this.output = output;
    return this;
  }

  withSessionId(sessionId: string): SessionResultTestBuilder {
    this.sessionId = sessionId;
    return this;
  }

  build(): SessionResult {
    return { output: this.output, sessionId: this.sessionId };
  }
}
