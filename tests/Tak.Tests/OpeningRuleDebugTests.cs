namespace Tak.Tests;

using Xunit;
using Tak.Core;

public class OpeningRuleDebugTests
{
    [Fact]
    public void OpeningRule_Sequence()
    {
        var game = Utils.CreateNewGame(4);
        
        // Initial state
        Assert.Equal(Player.White, game.CurrentPlayer);
        Assert.True(game.IsOpening[Player.White]);
        Assert.True(game.IsOpening[Player.Black]);
        
        // White's opening move
        var game2 = game.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Flat));
        
        // After White's opening move
        Assert.Equal(Player.Black, game2.CurrentPlayer);
        Assert.False(game2.IsOpening[Player.White]);
        Assert.True(game2.IsOpening[Player.Black]);
        
        // Check what piece was placed
        var stack = game2.Board.GetStack(new Position(0, 0));
        Assert.False(stack.IsEmpty);
        Assert.Equal(Player.Black, stack.Owner); // Opening rule: White places Black's flat
        
        // Black's opening move
        var game3 = game2.MakeMove(new PlaceMove(new Position(1, 1), PieceType.Flat));
        
        // After Black's opening move
        Assert.Equal(Player.White, game3.CurrentPlayer);
        Assert.False(game3.IsOpening[Player.White]);
        Assert.False(game3.IsOpening[Player.Black]);
        
        // Check what piece was placed
        var stack2 = game3.Board.GetStack(new Position(1, 1));
        Assert.False(stack2.IsEmpty);
        Assert.Equal(Player.White, stack2.Owner); // Opening rule: Black places White's flat
    }
}
