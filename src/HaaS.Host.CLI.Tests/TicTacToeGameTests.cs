using NUnit.Framework;
using NExpect;
using static NExpect.Expectations;

namespace HaaS.Host.CLI.Tests;

[TestFixture]
public class TicTacToeGameTests
{
    [Test]
    public void PlaceMarker_WhenPositionAvailable_ShouldPlaceMarkerAndReturnSuccess()
    {
        // Arrange
        var sut = Create();
        var position = 5;

        // Act
        var result = sut.PlaceMarker(position);

        // Assert
        Expect(result).To.Contain($"Placed O at position {position}");
        Expect(sut.Board[position - 1]).To.Equal('O');
        Expect(sut.HasMovedThisTurn).To.Be.True();
    }

    [Test]
    public void PlaceMarker_WhenAlreadyMoved_ShouldReturnError()
    {
        // Arrange
        var sut = Create();
        sut.PlaceMarker(1);

        // Act
        var result = sut.PlaceMarker(2);

        // Assert
        Expect(result).To.Contain("already placed your marker this turn");
        Expect(sut.Board[1]).To.Equal(' ');
    }

    [Test]
    public void PlaceMarker_WhenPositionTaken_ShouldReturnError()
    {
        // Arrange
        var sut = Create();
        sut.PlacePlayer(1);

        // Act
        var result = sut.PlaceMarker(1);

        // Assert
        Expect(result).To.Contain("is not available");
        Expect(sut.Board[0]).To.Equal('X');
    }

    private TicTacToeGame Create() => new SutBuilder().Build();
}

file sealed class SutBuilder
{
    public TicTacToeGame Build() => new();
}
