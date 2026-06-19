import { describe, expect, it, vi } from "vitest";
import { PassThrough } from "node:stream";
import { CliSignalSource } from "./cli-signal-source.js";

describe("CliSignalSource", () => {
  it("calls handler for each non-empty line", async () => {
    // Arrange
    const input = new PassThrough();
    const source = new CliSignalSource(input);
    const handler = vi.fn();

    // Act
    const listenPromise = source.listen(handler);
    input.write("hello\n");
    input.write("world\n");
    input.end("\n");

    await listenPromise;

    // Assert
    expect(handler).toHaveBeenCalledTimes(2);
    expect(handler).toHaveBeenCalledWith({ payload: "hello", source: "cli" });
    expect(handler).toHaveBeenCalledWith({ payload: "world", source: "cli" });
  });

  it("stops on empty line without calling handler", async () => {
    // Arrange
    const input = new PassThrough();
    const source = new CliSignalSource(input);
    const handler = vi.fn();

    // Act
    const listenPromise = source.listen(handler);
    input.end("\n");

    await listenPromise;

    // Assert
    expect(handler).not.toHaveBeenCalled();
  });

  it("stops when input ends after a line", async () => {
    // Arrange
    const input = new PassThrough();
    const source = new CliSignalSource(input);
    const handler = vi.fn();

    // Act
    const listenPromise = source.listen(handler);
    input.write("hello\n");
    input.end();

    await listenPromise;

    // Assert
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it("shutdown aborts an active listen", async () => {
    // Arrange
    const input = new PassThrough();
    const source = new CliSignalSource(input);
    const handler = vi.fn();
    let firstCallDone = false;

    const listenPromise = source.listen(handler);

    // Write one line and wait for it to be processed
    input.write("hello\n");
    await vi.waitFor(() => {
      if (handler.mock.calls.length > 0) firstCallDone = true;
      expect(firstCallDone).toBe(true);
    });

    // Act
    source.shutdown();
    await listenPromise;

    // Write more data after shutdown — should be ignored
    input.write("world\n");
    await new Promise((resolve) => setTimeout(resolve, 50));

    // Assert
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it("reports type as cli", () => {
    // Arrange
    const source = new CliSignalSource(new PassThrough());

    // Act & Assert
    expect(source.type).toBe("cli");
  });
});
