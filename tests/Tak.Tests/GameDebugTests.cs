namespace Tak.Tests;

using Xunit;
using Tak.Core;

public class GameDebugTests
{
    [Fact]
    public void Debug_FirstMoveEndsGame()
    {
        // Simple test to debug game ending after first move
        var game = Utils.CreateNewGame(4);
        Assert.Null(game.Result);
        Assert.Equal(Player.White, game.CurrentPlayer);
        
        var initialMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.NotEmpty(initialMoves);
        
        // Make first move
        var firstMove = initialMoves.First();
        game = game.MakeMove(firstMove);
        
        // After first move, game should not end
        Assert.Null(game.Result);
        Assert.Equal(Player.Black, game.CurrentPlayer);
        
        // Black should have legal moves
        var secondMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.NotEmpty(secondMoves);
    }
}
