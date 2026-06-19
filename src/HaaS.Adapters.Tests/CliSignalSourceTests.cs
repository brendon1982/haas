using HaaS.Adapters.Signal;
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
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput("hello\nworld\n\n")
            .Build();
        var signals = new List<SignalValue>();

        // Act
        await sut.ListenAsync(signal =>
        {
            signals.Add(signal);
            return Task.CompletedTask;
        });

        // Assert
        Assert.That(signals, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(signals[0].Payload, Is.EqualTo("hello"));
            Assert.That(signals[0].Source, Is.EqualTo("cli"));
            Assert.That(signals[1].Payload, Is.EqualTo("world"));
            Assert.That(signals[1].Source, Is.EqualTo("cli"));
        });
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
        Assert.That(handlerCalled, Is.False);
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
        Assert.That(signals, Has.Count.EqualTo(1));
    }

    [Test]
    public void Type_IsCli()
    {
        // Arrange
        var sut = CliSignalSourceSutBuilder.Create().Build();

        // Act & Assert
        Assert.That(sut.Type, Is.EqualTo("cli"));
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
