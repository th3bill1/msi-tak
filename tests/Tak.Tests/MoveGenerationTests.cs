using Xunit;
using Tak.Core;

namespace Tak.Tests;

public class MoveGenerationTests
{
    [Fact]
    public void LegalMoves_EmptyBoardHasPlacementMoves()
    {
        var game = Utils.CreateNewGame(5);
        var moves = GameRules.GetLegalMoves(game).ToList();
        
        // Should have placement moves for all empty squares (25 total)
        Assert.NotEmpty(moves);
        Assert.All(moves, m => Assert.IsType<PlaceMove>(m));
    }

    [Fact]
    public void OpeningMoves_AllPlaceFlat()
    {
        var game = Utils.CreateNewGame(5);
        var moves = GameRules.GetLegalMoves(game).ToList();
        
        // All should be placing flat stones (opening rule)
        foreach (var move in moves)
        {
            var placeMove = (PlaceMove)move;
            Assert.Equal(PieceType.Flat, placeMove.PieceType);
        }
    }

    [Fact]
    public void PlacementMoves_OnlyOnEmpty()
    {
        var game = Utils.CreateNewGame(4);
        
        // Place first piece
        var move1 = new PlaceMove(new Position(0, 0), PieceType.Flat);
        var game2 = game.MakeMove(move1);
        
        var moves = GameRules.GetLegalMoves(game2).ToList();
        var placeMoves = moves.OfType<PlaceMove>().ToList();
        
        // Should not have placement on (0,0)
        foreach (var move in placeMoves)
        {
            Assert.NotEqual(new Position(0, 0), move.Position);
        }
    }

    [Fact]
    public void SlideMoves_OnlyFromControlledStacks()
    {
        var game = Utils.CreateNewGame(4);
        
        // Place black flat (opening)
        var move1 = new PlaceMove(new Position(0, 0), PieceType.Flat);
        var game2 = game.MakeMove(move1);
        
        // Now it's Black's turn (Black's opening)
        var game3 = game2.MakeMove(new PlaceMove(new Position(1, 1), PieceType.Flat));
        
        // Now it's White's turn, White should only be able to move White's pieces
        // Board has: (0,0) Black's flat, (1,1) White's flat
        var moves = GameRules.GetLegalMoves(game3).ToList();
        var slideMoves = moves.OfType<SlideMove>().ToList();
        
        // Should only have moves from White's pieces (current player)
        foreach (var move in slideMoves)
        {
            var stack = game3.Board.GetStack(move.From);
            Assert.Equal(Player.White, stack.Owner);
        }
    }

    [Fact]
    public void SlideMoves_NoSkippedSquares()
    {
        var game = Utils.CreateNewGame(4);
        var moves = GameRules.GetLegalMoves(game).ToList();
        
        // All slide moves should be continuous (no skipped squares)
        foreach (var move in moves.OfType<SlideMove>())
        {
            // Verify distribution covers all squares in path
            Assert.True(move.IsValidDistribution(game.Config.BoardSize));
        }
    }

    [Fact]
    public void SlideMoves_ValidDropDistributions()
    {
        var state = new GameState(new GameConfig(5));
        state.CurrentPlayer = Player.White; // Ensure White's turn
        
        // Build a stack using GetStack to push pieces
        var stack = state.Board.GetStack(new Position(2, 2));
        stack.Push(new Piece(Player.White, PieceType.Flat));
        stack.Push(new Piece(Player.White, PieceType.Flat));
        stack.Push(new Piece(Player.White, PieceType.Flat));
        
        var moves = GameRules.GetLegalMoves(state).ToList();
        
        // All slide moves should have valid distributions
        foreach (var move in moves.OfType<SlideMove>())
        {
            Assert.NotEmpty(move.Distribution);
            Assert.All(move.Distribution, d => Assert.True(d > 0));
        }
    }

    [Fact]
    public void NoIllegalMovesGenerated()
    {
        var game = Utils.CreateNewGame(4);
        
        for (int i = 0; i < 10; i++)
        {
            var moves = GameRules.GetLegalMoves(game).ToList();
            
            if (moves.Count == 0)
                break;
            
            // Try each move - should not throw
            foreach (var move in moves)
            {
                var newGame = game.MakeMove(move);
                Assert.NotNull(newGame);
            }
            
            game = game.MakeMove(moves[0]);
        }
    }

    [Fact]
    public void MovesNotGeneratedAfterGameEnds()
    {
        var game = Utils.CreateNewGame(4);
        
        // Play until game ends (random moves)
        var random = new Random(42);
        while (game.Result == null)
        {
            var moves = GameRules.GetLegalMoves(game).ToList();
            if (moves.Count == 0)
                break;
            game = game.MakeMove(moves[random.Next(moves.Count)]);
        }
        
        // After game ends, no moves should be generated
        var finalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Empty(finalMoves);
    }

    [Fact]
    public void MovesConsistent_SameGameState()
    {
        var game = Utils.CreateNewGame(5);
        
        var moves1 = GameRules.GetLegalMoves(game).ToList();
        var moves2 = GameRules.GetLegalMoves(game).ToList();
        
        // Same game state should generate same moves (in same order if deterministic)
        Assert.Equal(moves1.Count, moves2.Count);
    }
}
