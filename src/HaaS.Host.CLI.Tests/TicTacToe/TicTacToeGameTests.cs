using NUnit.Framework;
using NExpect;
using static NExpect.Expectations;
using HaaS.Host.CLI.TicTacToe;

namespace HaaS.Host.CLI.Tests.TicTacToe;

[TestFixture]
public class TicTacToeGameTests
{
    [Test]
    public void PlaceAiMarker_WhenPositionAvailable_ShouldPlaceMarkerAndReturnTrue()
    {
        // Arrange
        var sut = Create();
        var position = 5;

        // Act
        var result = sut.TryPlaceAiMarker(position);

        // Assert
        Expect(result).To.Be.True();
        Expect(sut.Board[position - 1]).To.Equal('O');
        Expect(sut.AiHasMovedThisTurn).To.Be.True();
    }

    [Test]
    public void PlaceAiMarker_WhenAlreadyMoved_ShouldReturnFalse()
    {
        // Arrange
        var sut = Create();
        sut.TryPlaceAiMarker(1);

        // Act
        var result = sut.TryPlaceAiMarker(2);

        // Assert
        Expect(result).To.Be.False();
        Expect(sut.Board[1]).To.Equal(' ');
    }

    [Test]
    public void PlaceAiMarker_WhenPositionTaken_ShouldReturnFalse()
    {
        // Arrange
        var sut = Create();
        sut.PlacePlayerMarker(1);

        // Act
        var result = sut.TryPlaceAiMarker(1);

        // Assert
        Expect(result).To.Be.False();
        Expect(sut.Board[0]).To.Equal('X');
    }

    [Test]
    public void GetWinner_WhenThreeInARow_ShouldReturnWinner()
    {
        // Arrange
        var sut = Create();
        sut.PlacePlayerMarker(1);
        sut.PlacePlayerMarker(2);
        sut.PlacePlayerMarker(3);

        // Act
        var result = sut.GetWinner();

        // Assert
        Expect(result).To.Equal('X');
    }

    [Test]
    public void IsDraw_WhenBoardFullAndNoWinner_ShouldReturnTrue()
    {
        // Arrange
        var sut = Create();
        // X O X
        // X O O
        // O X X
        sut.PlacePlayerMarker(1); 
        sut.TryPlaceAiMarker(2); 
        sut.ResetTurn();
        sut.PlacePlayerMarker(3);
        
        sut.PlacePlayerMarker(4); 
        sut.TryPlaceAiMarker(5); 
        sut.ResetTurn();
        sut.TryPlaceAiMarker(6);
        sut.ResetTurn();

        sut.TryPlaceAiMarker(7); 
        sut.ResetTurn();
        sut.PlacePlayerMarker(8); 
        sut.PlacePlayerMarker(9);

        // Act
        var result = sut.IsDraw();

        // Assert
        Expect(result).To.Be.True();
    }

    private TicTacToeGame Create() => new SutBuilder().Build();
}

file sealed class SutBuilder
{
    public TicTacToeGame Build() => new();
}
