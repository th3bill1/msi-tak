using Tak.Experiments;
using Tak.Core;
using Xunit;

namespace Tak.Tests;

public class AllPairsSuiteTests
{
    [Fact]
    public void GeneratePairs_ReturnsExpectedUniqueUnorderedPairs()
    {
        var pairs = AllPairsSuite.GeneratePairs(new[] { "random", "heuristic", "uct", "rave" })
            .Select(pair => pair.PairName)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Random vs Heuristic",
                "Random vs UCT",
                "Random vs RAVE",
                "Heuristic vs UCT",
                "Heuristic vs RAVE",
                "UCT vs RAVE"
            },
            pairs);
    }

    [Fact]
    public void Run_CreatesCsvWithExpectedHeadersForTinyTournament()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-all-pairs-{Guid.NewGuid():N}.csv");
        var stdout = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(stdout);

            var suite = new AllPairsSuite(
                new GameConfig(4),
                new[] { "random", "heuristic" },
                gamesPerPair: 2,
                iterationLimit: 1,
                moveTimeLimit: null,
                baseSeed: 123,
                outputPath: outputPath);

            var summary = suite.Run();

            Assert.Equal(2, summary.TotalGames);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("OVERALL RANKING", stdout.ToString());

            var lines = File.ReadAllLines(outputPath);
            Assert.Equal(3, lines.Length);
            Assert.Equal(
                "RunId,GameNumber,PairName,WhiteAgent,BlackAgent,BoardSize,Winner,WinnerAgent,ResultType,MoveCount,DurationMs,Seed,IterationsLimit,MoveTimeLimitMs,Error",
                lines[0]);
        }
        finally
        {
            Console.SetOut(originalOut);

            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void BuildSummary_CountsWinsLossesDrawsAndResultTypes()
    {
        var records = new[]
        {
            CreateRecord(1, "RAVE", "UCT", "White", "RAVE", "Road"),
            CreateRecord(2, "UCT", "RAVE", "White", "UCT", "Flat"),
            CreateRecord(3, "RAVE", "UCT", "Draw", "Draw", "Draw"),
            CreateRecord(4, "UCT", "RAVE", "Error", "Error", "Error", error: "boom")
        };

        var summary = AllPairsSuite.BuildSummary("test-run", records, "unused.csv");
        var pair = Assert.Single(summary.PairSummaries);

        Assert.Equal(4, summary.TotalGames);
        Assert.Equal(3, summary.CompletedGames);
        Assert.Equal(1, summary.ErrorGames);
        Assert.Equal(4, pair.Games);
        Assert.Equal(1, pair.Errors);
        Assert.Equal(1, pair.RoadWins);
        Assert.Equal(1, pair.FlatWins);
        Assert.Equal(1, pair.Draws);
        Assert.Equal(1, pair.AgentWins["RAVE"]);
        Assert.Equal(1, pair.AgentWins["UCT"]);

        var rave = summary.Ranking.Single(row => row.Agent == "RAVE");
        Assert.Equal(3, rave.Games);
        Assert.Equal(1, rave.Wins);
        Assert.Equal(1, rave.Losses);
        Assert.Equal(1, rave.Draws);
    }

    [Fact]
    public void Parse_RejectsInvalidAllPairsAgentClearly()
    {
        var ex = Assert.Throws<ArgumentException>(() => ExperimentCli.Parse(new[] { "--suite", "all-pairs", "--agents", "random,not-an-agent" }));
        Assert.Contains("Unknown agent", ex.Message);
    }

    [Fact]
    public void Run_WithResumeKeepsValidRowsDropsPartialLineAndAppendsMissingGames()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-all-pairs-resume-{Guid.NewGuid():N}.csv");
        var stdout = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            File.WriteAllLines(outputPath, new[]
            {
                "RunId,GameNumber,PairName,WhiteAgent,BlackAgent,BoardSize,Winner,WinnerAgent,ResultType,MoveCount,DurationMs,Seed,IterationsLimit,MoveTimeLimitMs,Error",
                "existing-run,1,Random vs Heuristic,Random,Heuristic,4,Black,Heuristic,Road,18,100,123,1,,",
                "existing-run,2,Random vs Heuristic,Heuristic,Random,4,White"
            });

            Console.SetOut(stdout);

            var suite = new AllPairsSuite(
                new GameConfig(4),
                new[] { "random", "heuristic" },
                gamesPerPair: 2,
                iterationLimit: 1,
                moveTimeLimit: null,
                baseSeed: 123,
                outputPath: outputPath,
                resume: true);

            var summary = suite.Run();

            Assert.Equal(2, summary.TotalGames);
            Assert.Contains("1 invalid rows dropped", stdout.ToString());

            var lines = File.ReadAllLines(outputPath);
            Assert.Equal(3, lines.Length);
            Assert.Contains(",1,Random vs Heuristic,Random,Heuristic,", lines[1]);
            Assert.Contains(",2,Random vs Heuristic,Heuristic,Random,", lines[2]);
            Assert.DoesNotContain(lines, line => line.EndsWith(",White", StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);

            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void Run_WithFixedSeedProducesSameStableColumns()
    {
        var first = RunTinySuite(seed: 777);
        var second = RunTinySuite(seed: 777);

        Assert.Equal(ProjectStableColumns(first), ProjectStableColumns(second));
    }

    private static AllPairsGameResultRecord CreateRecord(
        int gameNumber,
        string whiteAgent,
        string blackAgent,
        string winner,
        string winnerAgent,
        string resultType,
        string? error = null)
    {
        return new AllPairsGameResultRecord
        {
            RunId = "test-run",
            GameNumber = gameNumber,
            PairName = "RAVE vs UCT",
            WhiteAgent = whiteAgent,
            BlackAgent = blackAgent,
            BoardSize = 4,
            Winner = winner,
            WinnerAgent = winnerAgent,
            ResultType = resultType,
            MoveCount = 10 + gameNumber,
            DurationMs = 20 + gameNumber,
            Seed = 100 + gameNumber,
            IterationsLimit = 1,
            MoveTimeLimitMs = null,
            Error = error
        };
    }

    private static string[] RunTinySuite(int seed)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tak-all-pairs-repro-{Guid.NewGuid():N}.csv");
        var stdout = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(stdout);

            var suite = new AllPairsSuite(
                new GameConfig(4),
                new[] { "random", "heuristic" },
                gamesPerPair: 2,
                iterationLimit: 1,
                moveTimeLimit: null,
                baseSeed: seed,
                outputPath: outputPath,
                runId: "fixed-run");

            suite.Run();
            return File.ReadAllLines(outputPath).Skip(1).ToArray();
        }
        finally
        {
            Console.SetOut(originalOut);

            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    private static string ProjectStableColumns(string[] records)
    {
        return string.Join(Environment.NewLine, records.Select(record =>
        {
            var columns = record.Split(',');
            return string.Join(',', columns[0], columns[1], columns[2], columns[3], columns[4], columns[5], columns[6], columns[7], columns[8], columns[9], columns[11], columns[12], columns[13], columns[14]);
        }));
    }
}
