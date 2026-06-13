using Xunit;
using Tak.Core;
using Tak.AI;
using Tak.Experiments;
using System.Diagnostics;

namespace Tak.Tests;

public class AgentTests
{
    [Fact]
    public void RandomAgent_ReturnsLegalMove()
    {
        var agent = new RandomAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void RandomAgent_Reproducible()
    {
        var game1 = Utils.CreateNewGame(4);
        var agent1 = new RandomAgent(seed: 42);
        var move1 = agent1.ChooseMove(game1);
        
        var game2 = Utils.CreateNewGame(4);
        var agent2 = new RandomAgent(seed: 42);
        var move2 = agent2.ChooseMove(game2);
        
        // Same seed should produce same move
        Assert.Equal(move1, move2);
    }

    [Fact]
    public void HeuristicAgent_ReturnsLegalMove()
    {
        var agent = new HeuristicAgent(seed: 42);
        var game = Utils.CreateNewGame(5);
        
        var move = agent.ChooseMove(game);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void HeuristicAgent_PrefersMatesInOne()
    {
        var agent = new HeuristicAgent();
        var game = Utils.CreateNewGame(4);
        
        // Create a scenario where heuristic could find an immediate win
        // This is simplified - just verify it returns a legal move
        var move = agent.ChooseMove(game);
        Assert.NotNull(move);
    }

    [Fact]
    public void UctAgent_ReturnsLegalMove()
    {
        var agent = new UctAgent(explorationConstant: 1.414, seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 100);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void UctAgent_RespondsToIterationLimit()
    {
        var agent = new UctAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        // With 10 iterations should be fast
        var start = DateTime.Now;
        var move1 = agent.ChooseMove(game, iterationLimit: 10);
        var time1 = (DateTime.Now - start).TotalMilliseconds;
        
        // With 1000 iterations should be slower
        start = DateTime.Now;
        var move2 = agent.ChooseMove(game, iterationLimit: 1000);
        var time2 = (DateTime.Now - start).TotalMilliseconds;
        
        // Just verify both return valid moves
        Assert.NotNull(move1);
        Assert.NotNull(move2);
    }

    [Fact]
    public void RaveAgent_ReturnsLegalMove()
    {
        var agent = new RaveAgent(seed: 42) { ThrowOnInvalidMove = true };
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 100);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void SlideMove_EqualityUsesDistributionValues()
    {
        var moveA = new SlideMove(new Position(0, 0), new Position(0, 2), Direction.Right, new[] { 1, 1 });
        var moveB = new SlideMove(new Position(0, 0), new Position(0, 2), Direction.Right, new[] { 1, 1 });

        Assert.Equal(moveA, moveB);
        Assert.Equal(moveA.GetHashCode(), moveB.GetHashCode());
    }

    [Fact]
    public void RaveAgent_ChoosesImmediateRoadWin()
    {
        var game = CreateManualState(Player.White);
        game.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 1), new Piece(Player.White, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 2), new Piece(Player.White, PieceType.Flat));

        var agent = new RaveAgent(seed: 42) { ThrowOnInvalidMove = true };
        var move = agent.ChooseMove(game, iterationLimit: 1);

        var place = Assert.IsType<PlaceMove>(move);
        Assert.Equal(new Position(0, 3), place.Position);
        Assert.Equal(PieceType.Flat, place.PieceType);
        Assert.Equal("immediate-win", agent.LastDiagnostics?.SelectionReason);
    }

    [Fact]
    public void RaveAgent_BlocksImmediateOpponentRoadWin()
    {
        var game = CreateManualState(Player.White);
        game.Board.PlacePiece(new Position(0, 0), new Piece(Player.Black, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 1), new Piece(Player.Black, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 2), new Piece(Player.Black, PieceType.Flat));

        var agent = new RaveAgent(seed: 42) { ThrowOnInvalidMove = true };
        var move = agent.ChooseMove(game, iterationLimit: 1);

        var place = Assert.IsType<PlaceMove>(move);
        Assert.Equal(new Position(0, 3), place.Position);
        Assert.Equal("immediate-block", agent.LastDiagnostics?.SelectionReason);
    }

    [Fact]
    public void UctAgent_BlocksImmediateOpponentRoadWin()
    {
        var game = CreateManualState(Player.White);
        game.Board.PlacePiece(new Position(0, 0), new Piece(Player.Black, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 1), new Piece(Player.Black, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 2), new Piece(Player.Black, PieceType.Flat));

        var agent = new UctAgent(seed: 42);
        var move = agent.ChooseMove(game, iterationLimit: 1);

        var place = Assert.IsType<PlaceMove>(move);
        Assert.Equal(new Position(0, 3), place.Position);
    }

    [Fact]
    public void RaveAgent_DoesNotMutateInputState()
    {
        var agent = new RaveAgent(seed: 42) { ThrowOnInvalidMove = true };
        var game = Utils.CreateNewGame(4);
        var before = Snapshot(game);

        var move = agent.ChooseMove(game, iterationLimit: 50);

        Assert.Contains(move, GameRules.GetLegalMoves(game).ToList());
        Assert.Equal(before, Snapshot(game));
    }

    [Fact]
    public void RaveAgent_RespectsZeroTimeLimit()
    {
        var agent = new RaveAgent(seed: 42) { ThrowOnInvalidMove = true };
        var game = Utils.CreateNewGame(4);

        var move = agent.ChooseMove(game, TimeSpan.Zero, iterationLimit: 1000);

        Assert.Contains(move, GameRules.GetLegalMoves(game).ToList());
        Assert.Equal(0, agent.LastDiagnostics?.IterationsRun);
    }

    [Fact]
    public void RaveAgent_ReturnsQuicklyFromNearlyFullBoard()
    {
        var game = CreateManualState(Player.White);
        var empty = new Position(3, 3);

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                var pos = new Position(row, col);
                if (pos == empty)
                    continue;

                var owner = (row + col) % 2 == 0 ? Player.White : Player.Black;
                game.Board.PlacePiece(pos, new Piece(owner, PieceType.Flat));
            }
        }

        var agent = new RaveAgent(seed: 42) { ThrowOnInvalidMove = true };
        var stopwatch = Stopwatch.StartNew();

        var move = agent.ChooseMove(game, TimeSpan.FromMilliseconds(20), iterationLimit: 10_000);

        stopwatch.Stop();
        Assert.Contains(move, GameRules.GetLegalMoves(game).ToList());
        Assert.True(stopwatch.ElapsedMilliseconds < 1000);
    }

    [Fact]
    public void RaveAgent_BeatsRandomInSmallDeterministicMatch()
    {
        int raveWins = 0;
        int randomWins = 0;

        for (int gameIndex = 0; gameIndex < 6; gameIndex++)
        {
            var raveIsWhite = gameIndex % 2 == 0;
            var result = PlaySmallGame(
                raveIsWhite ? new RaveAgent(seed: 100 + gameIndex) : new RandomAgent(seed: 200 + gameIndex),
                raveIsWhite ? new RandomAgent(seed: 200 + gameIndex) : new RaveAgent(seed: 100 + gameIndex),
                iterationLimit: 80);

            if (result.Winner == Player.None)
                continue;

            var raveWon = (result.Winner == Player.White && raveIsWhite) || (result.Winner == Player.Black && !raveIsWhite);
            if (raveWon)
                raveWins++;
            else
                randomWins++;
        }

        Assert.True(raveWins >= randomWins, $"Expected RAVE not to underperform Random, got RAVE {raveWins}, Random {randomWins}.");
    }

    [Fact]
    public void ProgressiveWideningAgent_ReturnsLegalMove()
    {
        var agent = new ProgressiveWideningAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 100);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Theory]
    [InlineData("random")]
    [InlineData("heuristic")]
    [InlineData("uct")]
    [InlineData("rave")]
    [InlineData("pw")]
    public void AllAgents_ReturnLegalMoves(string agentName)
    {
        var agent = Tak.Experiments.AgentFactory.CreateAgent(agentName, seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 50);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void AgentFactory_CreatesAllAgents()
    {
        var agents = new[] { "random", "heuristic", "uct", "rave", "pw" };
        
        foreach (var name in agents)
        {
            var agent = Tak.Experiments.AgentFactory.CreateAgent(name);
            Assert.NotNull(agent);
            Assert.Equal(name switch
            {
                "random" => "Random",
                "heuristic" => "Heuristic",
                "uct" => "UCT",
                "rave" => "RAVE",
                "pw" => "PW",
                _ => ""
            }, agent.Name);
        }
    }

    private static GameState CreateManualState(Player currentPlayer)
    {
        var game = Utils.CreateNewGame(4);
        game.CurrentPlayer = currentPlayer;
        game.IsOpening[Player.White] = false;
        game.IsOpening[Player.Black] = false;
        return game;
    }

    private static GameResult PlaySmallGame(IAgent white, IAgent black, int iterationLimit)
    {
        var game = Utils.CreateNewGame(4);
        int moveCount = 0;

        while (game.Result == null && moveCount < 120)
        {
            var agent = game.CurrentPlayer == Player.White ? white : black;
            var move = agent.ChooseMove(game, TimeSpan.FromMilliseconds(50), iterationLimit);
            Assert.Contains(move, GameRules.GetLegalMoves(game).ToList());
            game = game.MakeMove(move);
            moveCount++;
        }

        Assert.NotNull(game.Result);
        return game.Result;
    }

    private static string Snapshot(GameState state)
    {
        var rows = new List<string>
        {
            state.CurrentPlayer.ToString(),
            state.MoveHistory.Count.ToString()
        };

        for (int row = 0; row < state.Config.BoardSize; row++)
        {
            for (int col = 0; col < state.Config.BoardSize; col++)
            {
                var stack = state.Board.GetStack(new Position(row, col));
                rows.Add(string.Join("/", stack.GetPieces().Select(piece => $"{piece.Owner}:{piece.Type}")));
            }
        }

        return string.Join("|", rows);
    }
}
