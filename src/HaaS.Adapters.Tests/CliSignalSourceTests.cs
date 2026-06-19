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
        var source = new CliSignalSource(input, output);
        var signals = new List<SignalValue>();

        // Act
        await source.ListenAsync(signal =>
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
        var source = new CliSignalSource(input, output);
        var handlerCalled = false;

        // Act
        await source.ListenAsync(_ =>
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
        var source = new CliSignalSource(input, output);
        var signals = new List<SignalValue>();

        // Act
        await source.ListenAsync(signal =>
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
        var source = new CliSignalSource(new StringReader(""), new StringWriter());

        // Act & Assert
        Assert.That(source.Type, Is.EqualTo("cli"));
    }
}
