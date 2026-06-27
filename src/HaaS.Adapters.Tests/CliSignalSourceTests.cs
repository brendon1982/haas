using static NExpect.Expectations;
using HaaS.Adapters.Signal;
using NExpect;
using NUnit.Framework;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class CliSignalSourceTests
{
    [Test]
    public async Task Listen_CallsHandlerForEachNonEmptyLine()
    {
        // Arrange
        var input = "hello\nworld\n\n";
        var expectedCount = 2;
        var expectedPayloads = new[] { "hello", "world" };
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput(input)
            .Build();
        var signals = new List<SignalValue>();

        // Act
        await sut.ListenAsync(signal =>
        {
            signals.Add(signal);
            return Task.CompletedTask;
        });

        // Assert
        Expect(signals).To.Contain.Exactly(expectedCount);
        Expect(signals[0].Payload).To.Equal(expectedPayloads[0]);
        Expect(signals[0].Source).To.Equal(sut.Type);
        Expect(signals[1].Payload).To.Equal(expectedPayloads[1]);
        Expect(signals[1].Source).To.Equal(sut.Type);
    }

    [Test]
    public async Task Listen_StopsOnEmptyLineWithoutCallingHandler()
    {
        // Arrange
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput("\n")
            .Build();
        var handlerCalled = false;

        // Act
        await sut.ListenAsync(_ =>
        {
            handlerCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        Expect(handlerCalled).To.Be.False();
    }

    [Test]
    public async Task Listen_StopsWhenInputEndsAfterLine()
    {
        // Arrange
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput("hello\n")
            .Build();
        var signals = new List<SignalValue>();

        // Act
        await sut.ListenAsync(signal =>
        {
            signals.Add(signal);
            return Task.CompletedTask;
        });

        // Assert
        Expect(signals).To.Contain.Exactly(1);
    }

    [Test]
    public void Type_IsCli()
    {
        // Arrange
        var sut = CliSignalSourceSutBuilder.Create().Build();

        // Act & Assert
        Expect(sut.Type).To.Equal("cli");
    }
}

// --- harness (local) ---

file sealed class CliSignalSourceSutBuilder
{
    private string _input = "";
    private TextWriter _output = new StringWriter();

    private CliSignalSourceSutBuilder() { }

    public static CliSignalSourceSutBuilder Create() => new();

    public CliSignalSourceSutBuilder WithInput(string input)
    {
        _input = input;
        return this;
    }

    public CliSignalSource Build() => new(new StringReader(_input), _output);
}
