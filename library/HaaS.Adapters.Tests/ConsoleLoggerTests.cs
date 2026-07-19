using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Observability;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ConsoleLoggerTests
{
    [Test]
    public void LogInformation_WritesFormattedLineWithLevel()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleLogger(writer);

        // Act
        sut.LogInformation("Hello world");

        // Assert
        var output = writer.ToString();
        Expect(output).To.Match(@"\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.*\] \[INFO\] Hello world");
    }

    [Test]
    public void LogError_WritesExceptionAndMessage()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleLogger(writer);
        var ex = new InvalidOperationException("Something broke");

        // Act
        sut.LogError(ex, "Request failed");

        // Assert
        var output = writer.ToString();
        Expect(output).To.Match(@"\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.*\] \[ERROR\] Request failed");
        Expect(output).To.Contain("InvalidOperationException: Something broke");
    }

    [Test]
    public void MultipleLogs_EachOnSeparateLine()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleLogger(writer);

        // Act
        sut.LogInformation("first");
        sut.LogWarning("second");

        // Assert
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);
        Expect(lines).To.Contain.Exactly(2);
    }

    [Test]
    public void StructuredArgs_AreResolvedInOutput()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleLogger(writer);

        // Act
        sut.LogInformation("User {0} logged in from {1}", "alice", "10.0.0.1");

        // Assert
        var output = writer.ToString();
        Expect(output).To.Contain("User alice logged in from 10.0.0.1");
    }

    [Test]
    public void InvalidFormat_DoesNotCrash_AndPrintsRawMessageWithArgs()
    {
        // Arrange
        var writer = new StringWriter();
        var sut = new ConsoleLogger(writer);

        // Act
        sut.LogInformation("Invalid {Source}", "chat");

        // Assert
        var output = writer.ToString();
        Expect(output).To.Contain("Invalid {Source} (Args: chat)");
    }
}
