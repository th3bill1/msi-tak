using Xunit;
using Tak.AI;
using Tak.Core;
using Tak.Experiments;
using System.IO;

namespace Tak.Tests;

public class AiAndExperimentCoverageTests
{
    [Fact]
    public void RandomAgent_ReturnsOnlyLegalMoveWhenOnlyOneOpeningDestinationExists()
    {
        var game = Utils.CreateNewGame(4);
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                if (row == 3 && col == 3)
                    continue;

                game.Board.PlacePiece(new Position(row, col), new Piece(Player.Black, PieceType.Flat));
            }
        }

        var onlyMove = Assert.Single(GameRules.GetLegalMoves(game));
        var selected = new RandomAgent(seed: 42).ChooseMove(game);

        Assert.Equal(onlyMove, selected);
    }

    [Fact]
    public void HeuristicAgent_ChoosesImmediateRoadWin()
    {
        var game = CreateManualState(Player.White);
        game.Board.PlacePiece(new Position(0, 0), new Piece(Player.White, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 1), new Piece(Player.White, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 2), new Piece(Player.White, PieceType.Flat));

        var move = new HeuristicAgent(seed: 42).ChooseMove(game);

        var place = Assert.IsType<PlaceMove>(move);
        Assert.Equal(new Position(0, 3), place.Position);
    }

    [Fact]
    public void HeuristicAgent_BlocksImmediateOpponentRoadWin()
    {
        var game = CreateManualState(Player.White);
        game.Board.PlacePiece(new Position(0, 0), new Piece(Player.Black, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 1), new Piece(Player.Black, PieceType.Flat));
        game.Board.PlacePiece(new Position(0, 2), new Piece(Player.Black, PieceType.Flat));

        var move = new HeuristicAgent(seed: 42).ChooseMove(game);

        var place = Assert.IsType<PlaceMove>(move);
        Assert.Equal(new Position(0, 3), place.Position);
    }

    [Theory]
    [InlineData("uct")]
    [InlineData("rave")]
    [InlineData("pw")]
    public void SearchAgents_ReturnLegalMovesQuicklyAcrossEarlyMiddleAndLateStates(string agentName)
    {
        var agent = AgentFactory.CreateAgent(agentName, seed: 42);

        foreach (var state in new[] { Utils.CreateNewGame(4), CreateMiddleGame(), CreateLateGame() })
        {
            var before = Snapshot(state);
            var move = agent.ChooseMove(state, TimeSpan.FromMilliseconds(100), iterationLimit: 10_000);

            Assert.Contains(move, GameRules.GetLegalMoves(state).ToList());
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Theory]
    [InlineData("random")]
    [InlineData("heuristic")]
    [InlineData("uct")]
    [InlineData("rave")]
    [InlineData("pw")]
    public void Agents_ReturnLegalMovesDuringDeterministicRandomGames(string agentName)
    {
        var random = new Random(55);
        var agent = AgentFactory.CreateAgent(agentName, seed: 99);
        var game = Utils.CreateNewGame(4);

        for (int ply = 0; ply < 20 && game.Result == null; ply++)
        {
            var move = ply % 3 == 0
                ? agent.ChooseMove(game, TimeSpan.FromMilliseconds(100), iterationLimit: 100)
                : GameRules.GetLegalMoves(game).ElementAt(random.Next(GameRules.GetLegalMoves(game).Count()));

            Assert.Contains(move, GameRules.GetLegalMoves(game).ToList());
            game = game.MakeMove(move);
        }
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void Heuristic_DoesNotUnderperformRandomInSmallDeterministicMatch()
    {
        var (heuristicWins, randomWins) = PlayMatch(
            gameCount: 8,
            createA: seed => new HeuristicAgent(seed),
            createB: seed => new RandomAgent(seed));

        Assert.True(heuristicWins >= randomWins, $"Expected Heuristic not to underperform Random, got Heuristic {heuristicWins}, Random {randomWins}.");
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void Rave_DoesNotUnderperformRandomInSmallDeterministicMatch()
    {
        var (raveWins, randomWins) = PlayMatch(
            gameCount: 6,
            createA: seed => new RaveAgent(seed: seed),
            createB: seed => new RandomAgent(seed),
            iterationLimit: 80);

        Assert.True(raveWins >= randomWins, $"Expected RAVE not to underperform Random, got RAVE {raveWins}, Random {randomWins}.");
    }

    [Fact]
    public void ExperimentRunner_WritesCsvWithExpectedColumns()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-tournament-{Guid.NewGuid():N}.csv");
        try
        {
            var tournament = new Tournament(
                new GameConfig(4),
                new AgentSpec("Random", seed => new RandomAgent(seed)),
                new AgentSpec("Heuristic", seed => new HeuristicAgent(seed)),
                totalGames: 2,
                iterationLimit: 5,
                moveTimeLimit: TimeSpan.FromMilliseconds(50),
                baseSeed: 123,
                outputPath: outputPath);

            tournament.Run();

            var lines = File.ReadAllLines(outputPath);
            Assert.True(lines.Length >= 2);
            Assert.Equal(
                "GameId,RunId,TimestampUtc,BoardSize,WhiteAgent,BlackAgent,Winner,ResultType,MoveCount,DurationMs,AverageMoveTimeMs,SimulationsPerSecond,Seed,WhiteSeed,BlackSeed,IterationLimit,MoveTimeLimitMs,Error",
                lines[0]);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void AgentFactory_RejectsUnknownAgentClearly()
    {
        var ex = Assert.Throws<ArgumentException>(() => AgentFactory.CreateAgent("not-an-agent"));
        Assert.Contains("Unknown agent", ex.Message);
    }

    [Fact]
    public void Program_RejectedInvalidAgentNameClearly()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-invalid-agent-{Guid.NewGuid():N}.csv");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = Program.Main(new[] { "--white", "not-an-agent", "--output", outputPath });

            Assert.Equal(1, exitCode);
            Assert.Contains("Unknown agent", stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);

            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void Program_RunsQuickDefaultTournamentAndCreatesCsv()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-default-{Guid.NewGuid():N}.csv");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = Program.Main(new[]
            {
                "--games", "2",
                "--board", "4",
                "--seed", "123",
                "--output", outputPath
            });

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("TOURNAMENT SUMMARY", stdout.ToString());
            Assert.Empty(stderr.ToString());

            var lines = File.ReadAllLines(outputPath);
            Assert.True(lines.Length >= 2);
            Assert.StartsWith("GameId,RunId,TimestampUtc,BoardSize,WhiteAgent,BlackAgent", lines[0]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);

            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void ExperimentRunner_FixedSeedProducesSameWinnersResultTypesAndMoveCounts()
    {
        var first = RunTinyTournament(seed: 777);
        var second = RunTinyTournament(seed: 777);

        Assert.Equal(ProjectStableColumns(first), ProjectStableColumns(second));
    }

    private static (int aWins, int bWins) PlayMatch(
        int gameCount,
        Func<int, IAgent> createA,
        Func<int, IAgent> createB,
        int iterationLimit = 50)
    {
        int aWins = 0;
        int bWins = 0;

        for (int gameIndex = 0; gameIndex < gameCount; gameIndex++)
        {
            bool aIsWhite = gameIndex % 2 == 0;
            var a = createA(1000 + gameIndex);
            var b = createB(2000 + gameIndex);
            var result = PlayGame(aIsWhite ? a : b, aIsWhite ? b : a, iterationLimit);

            if (result.Winner == Player.None)
                continue;

            bool aWon = (result.Winner == Player.White && aIsWhite) || (result.Winner == Player.Black && !aIsWhite);
            if (aWon)
                aWins++;
            else
                bWins++;
        }

        return (aWins, bWins);
    }

    private static GameResult PlayGame(IAgent white, IAgent black, int iterationLimit)
    {
        var game = Utils.CreateNewGame(4);
        int moveCount = 0;

        while (game.Result == null && moveCount < 100)
        {
            var agent = game.CurrentPlayer == Player.White ? white : black;
            var move = agent.ChooseMove(game, TimeSpan.FromMilliseconds(100), iterationLimit);
            Assert.Contains(move, GameRules.GetLegalMoves(game).ToList());
            game = game.MakeMove(move);
            moveCount++;
        }

        Assert.NotNull(game.Result);
        return game.Result;
    }

    private static string[] RunTinyTournament(int seed)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-repro-{Guid.NewGuid():N}.csv");
        try
        {
            var tournament = new Tournament(
                new GameConfig(4),
                new AgentSpec("Random", agentSeed => new RandomAgent(agentSeed)),
                new AgentSpec("Heuristic", agentSeed => new HeuristicAgent(agentSeed)),
                totalGames: 2,
                iterationLimit: 5,
                moveTimeLimit: TimeSpan.FromMilliseconds(50),
                baseSeed: seed,
                outputPath: outputPath);

            tournament.Run();
            return File.ReadAllLines(outputPath).Skip(1).ToArray();
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    private static string ProjectStableColumns(string[] records)
    {
        return string.Join(Environment.NewLine, records.Select(record =>
        {
            var columns = record.Split(',');
            return string.Join(',', columns[0], columns[3], columns[4], columns[5], columns[6], columns[7], columns[8], columns[12], columns[13], columns[14], columns[15], columns[16]);
        }));
    }

    private static GameState CreateManualState(Player currentPlayer)
    {
        var game = Utils.CreateNewGame(4);
        game.CurrentPlayer = currentPlayer;
        game.IsOpening[Player.White] = false;
        game.IsOpening[Player.Black] = false;
        return game;
    }

    private static GameState CreateMiddleGame()
    {
        var game = Utils.CreateNewGame(4);
        var moves = new Move[]
        {
            new PlaceMove(new Position(0, 0), PieceType.Flat),
            new PlaceMove(new Position(3, 3), PieceType.Flat),
            new PlaceMove(new Position(0, 1), PieceType.Flat),
            new PlaceMove(new Position(3, 2), PieceType.Flat),
            new PlaceMove(new Position(1, 1), PieceType.Wall),
            new PlaceMove(new Position(2, 2), PieceType.Wall)
        };

        foreach (var move in moves)
        {
            game = game.MakeMove(move);
        }

        return game;
    }

    private static GameState CreateLateGame()
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

        return game;
    }

    private static string Snapshot(GameState state)
    {
        var parts = new List<string>
        {
            state.CurrentPlayer.ToString(),
            state.MoveHistory.Count.ToString(),
            state.FlatStoneReserve[Player.White].ToString(),
            state.FlatStoneReserve[Player.Black].ToString(),
            state.CapstonReserve[Player.White].ToString(),
            state.CapstonReserve[Player.Black].ToString()
        };

        for (int row = 0; row < state.Config.BoardSize; row++)
        {
            for (int col = 0; col < state.Config.BoardSize; col++)
            {
                var stack = state.Board.GetStack(new Position(row, col));
                parts.Add(string.Join("/", stack.GetPieces().Select(piece => $"{piece.Owner}:{piece.Type}")));
            }
        }

        return string.Join("|", parts);
    }
}
