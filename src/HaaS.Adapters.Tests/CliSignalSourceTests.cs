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
        var input = new StringReader("hello\nworld\n\n");
        var output = new StringWriter();
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput(input)
            .WithOutput(output)
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
        var input = new StringReader("\n");
        var output = new StringWriter();
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput(input)
            .WithOutput(output)
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
        var input = new StringReader("hello\n");
        var output = new StringWriter();
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput(input)
            .WithOutput(output)
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
        var sut = CliSignalSourceSutBuilder.Create()
            .WithInput(new StringReader(""))
            .WithOutput(new StringWriter())
            .Build();

        // Act & Assert
        Assert.That(sut.Type, Is.EqualTo("cli"));
    }
}

// --- harness (local) ---

file sealed class CliSignalSourceSutBuilder
{
    private TextReader _input = new StringReader("");
    private TextWriter _output = new StringWriter();

    private CliSignalSourceSutBuilder() { }

    public static CliSignalSourceSutBuilder Create() => new();

    public CliSignalSourceSutBuilder WithInput(TextReader input)
    {
        _input = input;
        return this;
    }

    public CliSignalSourceSutBuilder WithOutput(TextWriter output)
    {
        _output = output;
        return this;
    }

    public CliSignalSource Build() => new(_input, _output);
}
