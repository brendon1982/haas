import type { Signal } from "../domain/value-objects/signal.js";

export class SignalTestBuilder {
  private payload = "default prompt";
  private source = "test";
  private sessionId: string | undefined;

  private constructor() {}

  static create(): SignalTestBuilder {
    return new SignalTestBuilder();
  }

  withPayload(payload: string): SignalTestBuilder {
    this.payload = payload;
    return this;
  }

  withSource(source: string): SignalTestBuilder {
    this.source = source;
    return this;
  }

  withSessionId(sessionId: string): SignalTestBuilder {
    this.sessionId = sessionId;
    return this;
  }

  build(): Signal {
    return {
      payload: this.payload,
      source: this.source,
      ...(this.sessionId !== undefined ? { sessionId: this.sessionId } : {}),
    };
  }
}
