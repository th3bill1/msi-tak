using Xunit;
using Tak.Core;

namespace Tak.Tests;

public class CoreFeatureCoverageTests
{
    [Theory]
    [InlineData(4, 15, 0)]
    [InlineData(5, 21, 1)]
    [InlineData(6, 30, 1)]
    public void NewGame_InitializesBoardPlayerOpeningAndReserves(int size, int flats, int capstones)
    {
        var game = Utils.CreateNewGame(size);

        Assert.Equal(Player.White, game.CurrentPlayer);
        Assert.True(game.IsOpening[Player.White]);
        Assert.True(game.IsOpening[Player.Black]);
        Assert.Equal(flats, game.FlatStoneReserve[Player.White]);
        Assert.Equal(flats, game.FlatStoneReserve[Player.Black]);
        Assert.Equal(capstones, game.CapstonReserve[Player.White]);
        Assert.Equal(capstones, game.CapstonReserve[Player.Black]);

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                Assert.True(game.Board.IsEmpty(new Position(row, col)));
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void GameConfig_RejectsUnsupportedBoardSizes(int size)
    {
        Assert.Throws<ArgumentException>(() => new GameConfig(size));
    }

    [Theory]
    [InlineData(PieceType.Flat)]
    [InlineData(PieceType.Wall)]
    [InlineData(PieceType.Capstone)]
    public void NormalPlacement_PlacesSelectedPieceConsumesCorrectReserveAndSwitchesTurn(PieceType pieceType)
    {
        var game = CreateManualState(5, Player.White);
        var beforeFlats = game.FlatStoneReserve[Player.White];
        var beforeCaps = game.CapstonReserve[Player.White];

        var next = game.MakeMove(new PlaceMove(new Position(2, 2), pieceType));
        var stack = next.Board.GetStack(new Position(2, 2));

        Assert.Equal(Player.Black, next.CurrentPlayer);
        Assert.Equal(new Piece(Player.White, pieceType), stack.TopPiece);

        if (pieceType == PieceType.Capstone)
        {
            Assert.Equal(beforeCaps - 1, next.CapstonReserve[Player.White]);
            Assert.Equal(beforeFlats, next.FlatStoneReserve[Player.White]);
        }
        else
        {
            Assert.Equal(beforeFlats - 1, next.FlatStoneReserve[Player.White]);
            Assert.Equal(beforeCaps, next.CapstonReserve[Player.White]);
        }
    }

    [Fact]
    public void OpeningRule_ImplementedAsTwoOpponentFlatPlacementsWithoutReserveConsumption()
    {
        var game = Utils.CreateNewGame(4);
        var initialWhiteFlats = game.FlatStoneReserve[Player.White];
        var initialBlackFlats = game.FlatStoneReserve[Player.Black];

        var afterWhiteOpening = game.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Flat));
        var afterBlackOpening = afterWhiteOpening.MakeMove(new PlaceMove(new Position(1, 1), PieceType.Flat));

        Assert.Equal(Player.Black, afterWhiteOpening.Board.GetStack(new Position(0, 0)).Owner);
        Assert.Equal(Player.White, afterBlackOpening.Board.GetStack(new Position(1, 1)).Owner);
        Assert.Equal(Player.White, afterBlackOpening.CurrentPlayer);
        Assert.False(afterBlackOpening.IsOpening[Player.White]);
        Assert.False(afterBlackOpening.IsOpening[Player.Black]);
        Assert.Equal(initialWhiteFlats, afterBlackOpening.FlatStoneReserve[Player.White]);
        Assert.Equal(initialBlackFlats, afterBlackOpening.FlatStoneReserve[Player.Black]);
    }

    [Fact]
    public void Placement_InvalidTargetsAndEmptyReservesThrow()
    {
        var game = CreateManualState(4, Player.White);

        Assert.Throws<ArgumentOutOfRangeException>(() => game.MakeMove(new PlaceMove(new Position(-1, 0), PieceType.Flat)));

        var occupied = game.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Flat));
        Assert.Throws<InvalidOperationException>(() => occupied.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Flat)));

        var noFlats = CreateManualState(4, Player.White);
        noFlats.FlatStoneReserve[Player.White] = 0;
        Assert.Throws<InvalidOperationException>(() => noFlats.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Flat)));
        Assert.Throws<InvalidOperationException>(() => noFlats.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Wall)));

        var noCapstone = CreateManualState(4, Player.White);
        Assert.Throws<InvalidOperationException>(() => noCapstone.MakeMove(new PlaceMove(new Position(0, 0), PieceType.Capstone)));
    }

    [Fact]
    public void Slide_PreservesStackOrderChangesTopOwnershipAndSwitchesTurn()
    {
        var game = CreateManualState(4, Player.White);
        var source = game.Board.GetStack(new Position(1, 1));
        source.Push(new Piece(Player.Black, PieceType.Flat));
        source.Push(new Piece(Player.White, PieceType.Flat));

        var next = game.MakeMove(new SlideMove(new Position(1, 1), new Position(1, 3), Direction.Right, new[] { 1, 1 }));

        Assert.True(next.Board.GetStack(new Position(1, 1)).IsEmpty);
        Assert.Equal(Player.Black, next.Board.GetStack(new Position(1, 2)).Owner);
        Assert.Equal(Player.White, next.Board.GetStack(new Position(1, 3)).Owner);
        Assert.Equal(Player.Black, next.CurrentPlayer);
    }

    [Fact]
    public void Slide_OntoOccupiedSquareChangesControlToMovedTopPiece()
    {
        var game = CreateManualState(4, Player.White);
        game.Board.GetStack(new Position(1, 1)).Push(new Piece(Player.White, PieceType.Flat));
        game.Board.GetStack(new Position(1, 2)).Push(new Piece(Player.Black, PieceType.Flat));

        var next = game.MakeMove(new SlideMove(new Position(1, 1), new Position(1, 2), Direction.Right, new[] { 1 }));

        var target = next.Board.GetStack(new Position(1, 2));
        Assert.Equal(2, target.Height);
        Assert.Equal(Player.White, target.Owner);
    }

    [Fact]
    public void GeneratedSlides_DoNotMoveEmptyOrOpponentStacksOrBeyondCarryLimit()
    {
        var game = CreateManualState(4, Player.White);
        game.Board.GetStack(new Position(0, 0)).Push(new Piece(Player.Black, PieceType.Flat));
        for (int i = 0; i < 6; i++)
        {
            game.Board.GetStack(new Position(1, 1)).Push(new Piece(Player.White, PieceType.Flat));
        }

        var slides = GameRules.GetLegalMoves(game).OfType<SlideMove>().ToList();

        Assert.All(slides, move =>
        {
            Assert.Equal(Player.White, game.Board.GetStack(move.From).Owner);
            Assert.True(move.PiecesCarried <= game.Config.BoardSize);
            Assert.True(move.To.IsValid(game.Config.BoardSize));
        });
        Assert.DoesNotContain(slides, move => move.From == new Position(0, 0));
    }

    [Fact]
    public void WallsBlockFlatsAndCapstonesFlattenOneAdjacentWall()
    {
        var game = CreateManualState(5, Player.White);
        game.Board.GetStack(new Position(2, 1)).Push(new Piece(Player.White, PieceType.Flat));
        game.Board.GetStack(new Position(2, 2)).Push(new Piece(Player.Black, PieceType.Wall));

        var flatSlides = GameRules.GetLegalMoves(game).OfType<SlideMove>().ToList();
        Assert.DoesNotContain(flatSlides, move => move.From == new Position(2, 1) && move.To == new Position(2, 2));

        var capstoneGame = CreateManualState(5, Player.White);
        capstoneGame.Board.GetStack(new Position(2, 1)).Push(new Piece(Player.White, PieceType.Capstone));
        capstoneGame.Board.GetStack(new Position(2, 2)).Push(new Piece(Player.Black, PieceType.Wall));
        capstoneGame.Board.GetStack(new Position(2, 3)).Push(new Piece(Player.Black, PieceType.Wall));

        var capstoneSlides = GameRules.GetLegalMoves(capstoneGame).OfType<SlideMove>().ToList();
        var flatten = Assert.Single(capstoneSlides.Where(move => move.From == new Position(2, 1) && move.To == new Position(2, 2)));
        Assert.DoesNotContain(capstoneSlides, move => move.To == new Position(2, 3));

        var next = capstoneGame.MakeMove(flatten);
        var flattened = next.Board.GetStack(new Position(2, 2));
        Assert.Equal(Player.White, flattened.Owner);
        Assert.Equal(PieceType.Flat, flattened.TopPiece.Type);
    }

    [Fact]
    public void RoadWin_DetectsHorizontalVerticalAndCapstoneConnections()
    {
        var horizontal = CreateManualState(4, Player.White);
        horizontal.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        horizontal.Board.PlacePiece(new Position(0, 1), new Piece(Player.White, PieceType.Flat));
        horizontal.Board.PlacePiece(new Position(0, 2), new Piece(Player.White, PieceType.Flat));

        Assert.Equal(ResultType.Road, horizontal.MakeMove(new PlaceMove(new Position(0, 3), PieceType.Flat)).Result?.Type);

        var vertical = CreateManualState(4, Player.White);
        vertical.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        vertical.Board.PlacePiece(new Position(1, 0), new Piece(Player.White, PieceType.Flat));
        vertical.Board.PlacePiece(new Position(2, 0), new Piece(Player.White, PieceType.Flat));

        Assert.Equal(Player.White, vertical.MakeMove(new PlaceMove(new Position(3, 0), PieceType.Flat)).Result?.Winner);

        var capstoneRoad = CreateManualState(5, Player.White);
        capstoneRoad.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        capstoneRoad.Board.PlacePiece(new Position(0, 1), new Piece(Player.White, PieceType.Capstone));
        capstoneRoad.Board.PlacePiece(new Position(0, 2), new Piece(Player.White, PieceType.Flat));
        capstoneRoad.Board.PlacePiece(new Position(0, 3), new Piece(Player.White, PieceType.Flat));

        Assert.Equal(Player.White, capstoneRoad.MakeMove(new PlaceMove(new Position(0, 4), PieceType.Flat)).Result?.Winner);
    }

    [Fact]
    public void RoadWin_DoesNotPassThroughOpponentPiecesOrWalls()
    {
        var opponentGap = CreateManualState(4, Player.White);
        opponentGap.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        opponentGap.Board.PlacePiece(new Position(0, 1), new Piece(Player.Black, PieceType.Flat));
        opponentGap.Board.PlacePiece(new Position(0, 2), new Piece(Player.White, PieceType.Flat));

        Assert.Null(opponentGap.MakeMove(new PlaceMove(new Position(0, 3), PieceType.Flat)).Result);

        var wallGap = CreateManualState(4, Player.White);
        wallGap.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        wallGap.Board.PlacePiece(new Position(0, 1), new Piece(Player.White, PieceType.Wall));
        wallGap.Board.PlacePiece(new Position(0, 2), new Piece(Player.White, PieceType.Flat));

        Assert.Null(wallGap.MakeMove(new PlaceMove(new Position(0, 3), PieceType.Flat)).Result);
    }

    [Fact]
    public void FullBoardFlatWin_UsesControlledFlatCountsAfterLastMove()
    {
        var game = CreateManualState(4, Player.White);
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                if (row == 3 && col == 3)
                    continue;

                var owner = (row + col) % 2 == 0 ? Player.White : Player.Black;
                game.Board.PlacePiece(new Position(row, col), new Piece(owner, PieceType.Flat));
            }
        }

        var final = game.MakeMove(new PlaceMove(new Position(3, 3), PieceType.Wall));

        Assert.Equal(ResultType.Flat, final.Result?.Type);
        Assert.Equal(Player.Black, final.Result?.Winner);
        Assert.Single(final.MoveHistory);
    }

    private static GameState CreateManualState(int boardSize, Player currentPlayer)
    {
        var game = Utils.CreateNewGame(boardSize);
        game.CurrentPlayer = currentPlayer;
        game.IsOpening[Player.White] = false;
        game.IsOpening[Player.Black] = false;
        return game;
    }
}
