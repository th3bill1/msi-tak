namespace Tak.Experiments;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Tak.AI;
using Tak.Core;

public sealed class TournamentResultRecord
{
    public int GameId { get; set; }
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public int BoardSize { get; set; }
    public string WhiteAgent { get; set; } = string.Empty;
    public string BlackAgent { get; set; } = string.Empty;
    public string Winner { get; set; } = string.Empty;
    public string ResultType { get; set; } = string.Empty;
    public int MoveCount { get; set; }
    public long DurationMs { get; set; }
    public double AverageMoveTimeMs { get; set; }
    public double SimulationsPerSecond { get; set; }
    public int Seed { get; set; }
    public int WhiteSeed { get; set; }
    public int BlackSeed { get; set; }
    public int IterationLimit { get; set; }
    public int? MoveTimeLimitMs { get; set; }
    public string? Error { get; set; }
}

public sealed record TournamentSummary(
    string RunId,
    int TotalGames,
    int CompletedGames,
    int ErrorGames,
    int WhiteWins,
    int BlackWins,
    int DrawGames,
    double AverageMoveCount,
    double AverageDurationMs,
    IReadOnlyDictionary<string, int> ResultTypeCounts,
    string OutputPath);

public sealed class Tournament
{
    private readonly GameConfig config;
    private readonly AgentSpec whiteSpec;
    private readonly AgentSpec blackSpec;
    private readonly int totalGames;
    private readonly int iterationLimit;
    private readonly TimeSpan? moveTimeLimit;
    private readonly int baseSeed;
    private readonly string outputPath;
    private readonly string runId;

    public Tournament(
        GameConfig config,
        AgentSpec whiteSpec,
        AgentSpec blackSpec,
        int totalGames,
        int iterationLimit,
        TimeSpan? moveTimeLimit,
        int baseSeed,
        string outputPath,
        string? runId = null)
    {
        this.config = config;
        this.whiteSpec = whiteSpec;
        this.blackSpec = blackSpec;
        this.totalGames = totalGames;
        this.iterationLimit = iterationLimit;
        this.moveTimeLimit = moveTimeLimit;
        this.baseSeed = baseSeed;
        this.outputPath = outputPath;
        this.runId = string.IsNullOrWhiteSpace(runId) ? DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") : runId;
    }

    public TournamentSummary Run()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        long totalMoveCount = 0;
        long totalDurationMs = 0;
        int whiteWins = 0;
        int blackWins = 0;
        int drawGames = 0;
        int errorGames = 0;

        Console.WriteLine($"Starting tournament: {whiteSpec.Name} vs {blackSpec.Name}");
        Console.WriteLine($"Board: {config.BoardSize}x{config.BoardSize}");
        Console.WriteLine($"Games: {totalGames}");
        Console.WriteLine($"Iteration limit: {iterationLimit}");
        Console.WriteLine($"Move time limit: {FormatMoveTimeLimit(moveTimeLimit)}");
        Console.WriteLine($"Seed: {baseSeed}");
        Console.WriteLine($"Run id: {runId}");
        Console.WriteLine();

        EnsureOutputDirectory(outputPath);
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
        WriteCsvHeader(writer);

        for (int gameIndex = 0; gameIndex < totalGames; gameIndex++)
        {
            var record = PlaySingleGame(gameIndex);
            totalMoveCount += record.MoveCount;
            totalDurationMs += record.DurationMs;
            Increment(counts, record.ResultType);

            if (string.Equals(record.ResultType, "Error", StringComparison.OrdinalIgnoreCase))
            {
                errorGames++;
            }
            else if (string.Equals(record.ResultType, ResultType.Draw.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(record.Winner, "Draw", StringComparison.OrdinalIgnoreCase))
            {
                drawGames++;
            }
            else if (string.Equals(record.Winner, whiteSpec.Name, StringComparison.OrdinalIgnoreCase))
            {
                whiteWins++;
            }
            else if (string.Equals(record.Winner, blackSpec.Name, StringComparison.OrdinalIgnoreCase))
            {
                blackWins++;
            }

            WriteCsvRecord(writer, record);
            Console.WriteLine($"Game {record.GameId:D2}/{totalGames}: {record.WhiteAgent} vs {record.BlackAgent} -> {record.Winner} ({record.ResultType}) [{record.MoveCount} moves, {record.DurationMs}ms, seed {record.Seed}]");
        }

        var completedGames = totalGames - errorGames;
        var averageMoveCount = completedGames > 0 ? totalMoveCount / (double)completedGames : 0;
        var averageDurationMs = completedGames > 0 ? totalDurationMs / (double)completedGames : 0;

        Console.WriteLine();
        Console.WriteLine("=== TOURNAMENT SUMMARY ===");
        Console.WriteLine($"Run id: {runId}");
        Console.WriteLine($"Total games: {totalGames}");
        Console.WriteLine($"Completed games: {completedGames}");
        Console.WriteLine($"Errors: {errorGames}");
        Console.WriteLine($"Draws: {drawGames}");
        Console.WriteLine($"{whiteSpec.Name}: {whiteWins} wins ({FormatRate(whiteWins, completedGames)})");
        Console.WriteLine($"{blackSpec.Name}: {blackWins} wins ({FormatRate(blackWins, completedGames)})");
        Console.WriteLine($"Average move count: {averageMoveCount:F1}");
        Console.WriteLine($"Average duration: {averageDurationMs:F1}ms");
        Console.WriteLine($"Win type distribution: {FormatDistribution(counts)}");
        Console.WriteLine($"Results written to: {outputPath}");
        Console.WriteLine("Reproducibility note: a fixed --seed records per-game seeds; complete determinism still depends on the agents and timing limits used.");

        return new TournamentSummary(runId, totalGames, completedGames, errorGames, whiteWins, blackWins, drawGames, averageMoveCount, averageDurationMs, counts, outputPath);
    }

    private TournamentResultRecord PlaySingleGame(int gameIndex)
    {
        var whiteSeed = unchecked(baseSeed + gameIndex * 2);
        var blackSeed = unchecked(baseSeed + gameIndex * 2 + 1);
        var gameSeed = unchecked(baseSeed + gameIndex);
        var sw = Stopwatch.StartNew();
        var state = Utils.CreateNewGame(config.BoardSize);
        GameResult? result = null;
        string? error = null;

        var whiteAgent = whiteSpec.CreateAgent(whiteSeed);
        var blackAgent = blackSpec.CreateAgent(blackSeed);

        try
        {
            while (state.Result == null)
            {
                var agent = state.CurrentPlayer == Player.White ? whiteAgent : blackAgent;
                var move = agent.ChooseMove(state, moveTimeLimit, iterationLimit);
                state = state.MakeMove(move);
            }

            result = state.Result;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            sw.Stop();
        }

        var moveCount = result?.MoveCount ?? state.MoveHistory.Count;
        var resultType = error != null ? "Error" : result?.Type.ToString() ?? "Error";
        var winner = error != null
            ? "Error"
            : result?.Winner == Player.White
                ? whiteSpec.Name
                : result?.Winner == Player.Black
                    ? blackSpec.Name
                    : "Draw";

        return new TournamentResultRecord
        {
            GameId = gameIndex + 1,
            RunId = runId,
            TimestampUtc = DateTimeOffset.UtcNow,
            BoardSize = config.BoardSize,
            WhiteAgent = whiteSpec.Name,
            BlackAgent = blackSpec.Name,
            Winner = winner,
            ResultType = resultType,
            MoveCount = moveCount,
            DurationMs = sw.ElapsedMilliseconds,
            AverageMoveTimeMs = moveCount > 0 ? sw.ElapsedMilliseconds / (double)moveCount : 0,
            SimulationsPerSecond = moveCount > 0 && sw.ElapsedMilliseconds > 0 ? moveCount * 1000.0 / sw.ElapsedMilliseconds : 0,
            Seed = gameSeed,
            WhiteSeed = whiteSeed,
            BlackSeed = blackSeed,
            IterationLimit = iterationLimit,
            MoveTimeLimitMs = moveTimeLimit.HasValue ? (int)moveTimeLimit.Value.TotalMilliseconds : null,
            Error = error,
        };
    }

    private static void WriteCsvHeader(TextWriter writer)
    {
        writer.WriteLine("GameId,RunId,TimestampUtc,BoardSize,WhiteAgent,BlackAgent,Winner,ResultType,MoveCount,DurationMs,AverageMoveTimeMs,SimulationsPerSecond,Seed,WhiteSeed,BlackSeed,IterationLimit,MoveTimeLimitMs,Error");
    }

    private static void WriteCsvRecord(TextWriter writer, TournamentResultRecord record)
    {
        writer.WriteLine(string.Join(",",
            Escape(record.GameId.ToString(CultureInfo.InvariantCulture)),
            Escape(record.RunId),
            Escape(record.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
            Escape(record.BoardSize.ToString(CultureInfo.InvariantCulture)),
            Escape(record.WhiteAgent),
            Escape(record.BlackAgent),
            Escape(record.Winner),
            Escape(record.ResultType),
            Escape(record.MoveCount.ToString(CultureInfo.InvariantCulture)),
            Escape(record.DurationMs.ToString(CultureInfo.InvariantCulture)),
            Escape(record.AverageMoveTimeMs.ToString("F3", CultureInfo.InvariantCulture)),
            Escape(record.SimulationsPerSecond.ToString("F3", CultureInfo.InvariantCulture)),
            Escape(record.Seed.ToString(CultureInfo.InvariantCulture)),
            Escape(record.WhiteSeed.ToString(CultureInfo.InvariantCulture)),
            Escape(record.BlackSeed.ToString(CultureInfo.InvariantCulture)),
            Escape(record.IterationLimit.ToString(CultureInfo.InvariantCulture)),
            Escape(record.MoveTimeLimitMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Escape(record.Error ?? string.Empty)));
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void EnsureOutputDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static string FormatRate(int wins, int total)
    {
        return total > 0 ? $"{wins * 100.0 / total:F1}%" : "n/a";
    }

    private static string FormatDistribution(Dictionary<string, int> counts)
    {
        if (counts.Count == 0)
            return "none";

        return string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string FormatMoveTimeLimit(TimeSpan? limit)
    {
        return limit.HasValue ? $"{limit.Value.TotalMilliseconds:0}ms" : "none";
    }
}
