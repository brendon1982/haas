export interface Signal {
  readonly payload: string;
  readonly source: string;
  readonly sessionId?: string;
}
