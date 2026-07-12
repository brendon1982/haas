using NExpect;
using static NExpect.Expectations;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace HaaS.Application.Tests.UseCases;

[TestFixture]
public class EnqueueSignalUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldEnqueueSignalWithArrivedAtTimestamp()
    {
        // Arrange
        var queue = Substitute.For<ISignalQueue>();
        var timeProvider = Substitute.For<TimeProvider>();
        var logger = Substitute.For<ILogger>();
        var now = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        timeProvider.GetUtcNow().Returns(now);
        
        var sut = new EnqueueSignalUseCase(queue, timeProvider, logger);
        var signal = new Signal("test", "cli");

        // Act
        var sessionId = await sut.ExecuteAsync(signal);

        // Assert
        Expect(sessionId).Not.To.Be.Null();
        await queue.Received(1).EnqueueAsync(
            Arg.Is<Signal>(s => s.Payload == "test" && s.ArrivedAt == now && s.SessionId == sessionId),
            Arg.Is<Identity>(i => i == Identity.Anonymous)
        );
    }
}
