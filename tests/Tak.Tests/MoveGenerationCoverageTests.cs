using Xunit;
using Tak.Core;

namespace Tak.Tests;

public class MoveGenerationCoverageTests
{
    [Theory]
    [InlineData(4, 16)]
    [InlineData(5, 25)]
    [InlineData(6, 36)]
    public void EmptyBoard_GeneratesOneOpeningPlacementPerDestination(int boardSize, int expectedCount)
    {
        var game = Utils.CreateNewGame(boardSize);
        var moves = GameRules.GetLegalMoves(game).ToList();

        Assert.Equal(expectedCount, moves.Count);
        Assert.All(moves, move =>
        {
            var placement = Assert.IsType<PlaceMove>(move);
            Assert.Equal(PieceType.Flat, placement.PieceType);
            Assert.True(placement.Position.IsValid(boardSize));
        });
    }

    [Fact]
    public void GeneratedMoves_HaveNoDuplicatesAndCanAllBeApplied()
    {
        var game = CreateSlideState(5);
        var moves = GameRules.GetLegalMoves(game).ToList();

        Assert.Equal(moves.Count, moves.Distinct().Count());
        foreach (var move in moves)
        {
            var next = game.MakeMove(move);
            Assert.Equal(game.MoveHistory.Count + 1, next.MoveHistory.Count);
        }
    }

    [Fact]
    public void SlideMoves_GeneratedInAllDirectionsFromCentralStack()
    {
        var game = CreateManualState(5, Player.White);
        game.Board.GetStack(new Position(2, 2)).Push(new Piece(Player.White, PieceType.Flat));

        var directions = GameRules.GetLegalMoves(game)
            .OfType<SlideMove>()
            .Where(move => move.From == new Position(2, 2) && move.PiecesCarried == 1)
            .Select(move => move.Direction)
            .ToHashSet();

        Assert.Contains(Direction.Up, directions);
        Assert.Contains(Direction.Down, directions);
        Assert.Contains(Direction.Left, directions);
        Assert.Contains(Direction.Right, directions);
    }

    [Fact]
    public void SlideMoves_IncludeShorterThanMaximumDistanceAndAccurateDestination()
    {
        var game = CreateManualState(5, Player.White);
        game.Board.GetStack(new Position(2, 2)).Push(new Piece(Player.White, PieceType.Flat));

        var oneStepRight = GameRules.GetLegalMoves(game)
            .OfType<SlideMove>()
            .SingleOrDefault(move =>
                move.From == new Position(2, 2)
                && move.Direction == Direction.Right
                && move.Distribution.SequenceEqual(new[] { 1 }));

        Assert.NotNull(oneStepRight);
        Assert.Equal(new Position(2, 3), oneStepRight.To);
    }

    [Fact]
    public void SlideMoves_GenerateValidDropPatternsForMultiPieceStack()
    {
        var game = CreateManualState(5, Player.White);
        var source = game.Board.GetStack(new Position(2, 2));
        source.Push(new Piece(Player.White, PieceType.Flat));
        source.Push(new Piece(Player.White, PieceType.Flat));
        source.Push(new Piece(Player.White, PieceType.Flat));

        var slides = GameRules.GetLegalMoves(game)
            .OfType<SlideMove>()
            .Where(move => move.From == new Position(2, 2))
            .ToList();

        Assert.Contains(slides, move => move.Direction == Direction.Right && move.Distribution.SequenceEqual(new[] { 3 }));
        Assert.Contains(slides, move => move.Direction == Direction.Right && move.Distribution.SequenceEqual(new[] { 1, 2 }));
        Assert.Contains(slides, move => move.Direction == Direction.Right && move.Distribution.SequenceEqual(new[] { 2, 1 }));

        Assert.All(slides, move =>
        {
            Assert.True(move.IsValidDistribution(game.Config.BoardSize));
            Assert.Equal(move.PiecesCarried, move.Distribution.Sum());
            Assert.True(move.To.IsValid(game.Config.BoardSize));
            Assert.True(PathLength(move.From, move.To) == move.Distribution.Length);
        });
    }

    [Fact]
    public void SlideMoves_RespectBoardEdgesWallsAndCapstones()
    {
        var game = CreateManualState(5, Player.White);
        game.Board.GetStack(new Position(0, 0)).Push(new Piece(Player.White, PieceType.Flat));
        game.Board.GetStack(new Position(0, 1)).Push(new Piece(Player.Black, PieceType.Wall));
        game.Board.GetStack(new Position(1, 0)).Push(new Piece(Player.Black, PieceType.Capstone));

        var slides = GameRules.GetLegalMoves(game).OfType<SlideMove>().Where(move => move.From == new Position(0, 0)).ToList();

        Assert.DoesNotContain(slides, move => move.Direction is Direction.Up or Direction.Left);
        Assert.DoesNotContain(slides, move => move.To == new Position(0, 1));
        Assert.DoesNotContain(slides, move => move.To == new Position(1, 0));
    }

    [Fact]
    public void RandomLegalGames_GeneratedMovesRemainApplicableAndStateInvariantsHold()
    {
        var random = new Random(1234);

        for (int gameIndex = 0; gameIndex < 3; gameIndex++)
        {
            var game = Utils.CreateNewGame(4);

            for (int ply = 0; ply < 30 && game.Result == null; ply++)
            {
                var legalMoves = GameRules.GetLegalMoves(game).ToList();
                Assert.Equal(legalMoves.Count, legalMoves.Distinct().Count());

                foreach (var move in legalMoves.Take(10))
                {
                    _ = game.MakeMove(move);
                }

                game = game.MakeMove(legalMoves[random.Next(legalMoves.Count)]);
                AssertBoardInvariants(game);
            }
        }
    }

    private static GameState CreateSlideState(int boardSize)
    {
        var game = CreateManualState(boardSize, Player.White);
        game.Board.GetStack(new Position(2, 2)).Push(new Piece(Player.White, PieceType.Flat));
        game.Board.GetStack(new Position(2, 2)).Push(new Piece(Player.White, PieceType.Flat));
        game.Board.GetStack(new Position(1, 2)).Push(new Piece(Player.Black, PieceType.Wall));
        game.Board.GetStack(new Position(2, 1)).Push(new Piece(Player.Black, PieceType.Flat));
        return game;
    }

    private static GameState CreateManualState(int boardSize, Player currentPlayer)
    {
        var game = Utils.CreateNewGame(boardSize);
        game.CurrentPlayer = currentPlayer;
        game.IsOpening[Player.White] = false;
        game.IsOpening[Player.Black] = false;
        return game;
    }

    private static int PathLength(Position from, Position to)
    {
        return Math.Abs(from.Row - to.Row) + Math.Abs(from.Col - to.Col);
    }

    private static void AssertBoardInvariants(GameState game)
    {
        Assert.True(game.FlatStoneReserve[Player.White] >= 0);
        Assert.True(game.FlatStoneReserve[Player.Black] >= 0);
        Assert.True(game.CapstonReserve[Player.White] >= 0);
        Assert.True(game.CapstonReserve[Player.Black] >= 0);

        foreach (var (pos, stack) in game.Board.GetNonEmptySquares())
        {
            Assert.True(pos.IsValid(game.Config.BoardSize));
            Assert.False(stack.IsEmpty);
            Assert.NotEqual(Player.None, stack.Owner);
            Assert.True(stack.Height > 0);
        }
    }
}
