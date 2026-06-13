namespace Tak.Experiments;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Tak.Core;

public sealed record AgentPair(string FirstAgent, string SecondAgent)
{
    public string FirstDisplayName => AgentFactory.GetDisplayName(FirstAgent);
    public string SecondDisplayName => AgentFactory.GetDisplayName(SecondAgent);
    public string PairName => $"{FirstDisplayName} vs {SecondDisplayName}";
}

public sealed class AllPairsGameResultRecord
{
    public string RunId { get; set; } = string.Empty;
    public int GameNumber { get; set; }
    public string PairName { get; set; } = string.Empty;
    public string WhiteAgent { get; set; } = string.Empty;
    public string BlackAgent { get; set; } = string.Empty;
    public int BoardSize { get; set; }
    public string Winner { get; set; } = string.Empty;
    public string WinnerAgent { get; set; } = string.Empty;
    public string ResultType { get; set; } = string.Empty;
    public int MoveCount { get; set; }
    public long DurationMs { get; set; }
    public int Seed { get; set; }
    public int IterationsLimit { get; set; }
    public int? MoveTimeLimitMs { get; set; }
    public string? Error { get; set; }
}

public sealed record PairSummary(
    string PairName,
    int Games,
    int CompletedGames,
    int Errors,
    IReadOnlyDictionary<string, int> AgentWins,
    int Draws,
    double AverageMoves,
    double AverageDurationMs,
    int RoadWins,
    int FlatWins);

public sealed record AgentRankingSummary(
    string Agent,
    int Games,
    int Wins,
    int Losses,
    int Draws,
    double WinRate);

public sealed record AllPairsSuiteSummary(
    string RunId,
    int TotalGames,
    int CompletedGames,
    int ErrorGames,
    IReadOnlyList<PairSummary> PairSummaries,
    IReadOnlyList<AgentRankingSummary> Ranking,
    string OutputPath);

internal sealed record PlannedAllPairsGame(
    int GameNumber,
    int PairGameNumber,
    AgentPair Pair,
    string WhiteAgent,
    string BlackAgent,
    int Seed);

internal sealed record ExistingCsvLoadResult(
    IReadOnlyList<AllPairsGameResultRecord> Records,
    int InvalidRows,
    int IgnoredRows);

/// <summary>Runs an all-pairs experiment over the configured AI agents.</summary>
public sealed class AllPairsSuite
{
    private const string CsvHeader = "RunId,GameNumber,PairName,WhiteAgent,BlackAgent,BoardSize,Winner,WinnerAgent,ResultType,MoveCount,DurationMs,Seed,IterationsLimit,MoveTimeLimitMs,Error";

    private readonly GameConfig config;
    private readonly IReadOnlyList<string> agentNames;
    private readonly int gamesPerPair;
    private readonly int iterationLimit;
    private readonly TimeSpan? moveTimeLimit;
    private readonly int baseSeed;
    private readonly string outputPath;
    private readonly double exploration;
    private readonly bool includeSelfPlay;
    private readonly bool resume;
    private string runId;

    public AllPairsSuite(
        GameConfig config,
        IReadOnlyList<string> agentNames,
        int gamesPerPair,
        int iterationLimit,
        TimeSpan? moveTimeLimit,
        int baseSeed,
        string outputPath,
        double exploration = 1.414,
        bool includeSelfPlay = false,
        bool resume = false,
        string? runId = null)
    {
        this.config = config;
        this.agentNames = agentNames.Select(AgentFactory.NormalizeAgentName).ToArray();
        this.gamesPerPair = gamesPerPair;
        this.iterationLimit = iterationLimit;
        this.moveTimeLimit = moveTimeLimit;
        this.baseSeed = baseSeed;
        this.outputPath = outputPath;
        this.exploration = exploration;
        this.includeSelfPlay = includeSelfPlay;
        this.resume = resume;
        this.runId = string.IsNullOrWhiteSpace(runId) ? DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture) : runId;

        if (gamesPerPair < 1)
            throw new ArgumentException("gamesPerPair must be at least 1.", nameof(gamesPerPair));
    }

    /// <summary>Generate all unique unordered pairs from the supplied agent names.</summary>
    public static IReadOnlyList<AgentPair> GeneratePairs(IReadOnlyList<string> agents, bool includeSelfPlay = false)
    {
        var normalized = agents
            .Select(AgentFactory.NormalizeAgentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pairs = new List<AgentPair>();
        for (int first = 0; first < normalized.Length; first++)
        {
            var secondStart = includeSelfPlay ? first : first + 1;
            for (int second = secondStart; second < normalized.Length; second++)
            {
                pairs.Add(new AgentPair(normalized[first], normalized[second]));
            }
        }

        return pairs;
    }

    /// <summary>Runs every configured pair, writes CSV output, and prints a summary.</summary>
    public AllPairsSuiteSummary Run()
    {
        var pairs = GeneratePairs(agentNames, includeSelfPlay);
        if (pairs.Count == 0)
            throw new ArgumentException("The all-pairs suite needs at least two agents, or use --include-self-play with one agent.");

        var plannedGames = BuildPlannedGames(pairs);
        var plannedKeys = plannedGames.Select(MakeScheduleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var records = new List<AllPairsGameResultRecord>(pairs.Count * gamesPerPair);
        var completedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int invalidExistingRows = 0;
        int ignoredExistingRows = 0;

        EnsureOutputDirectory(outputPath);
        if (resume)
        {
            var loadResult = LoadExistingRecords(outputPath, plannedKeys);
            records.AddRange(loadResult.Records);
            invalidExistingRows = loadResult.InvalidRows;
            ignoredExistingRows = loadResult.IgnoredRows;

            if (records.Count > 0)
                runId = records[0].RunId;

            foreach (var record in records)
                completedKeys.Add(MakeScheduleKey(record));
        }

        Console.WriteLine("Starting all-pairs suite");
        Console.WriteLine($"Agents: {string.Join(", ", agentNames.Select(AgentFactory.GetDisplayName))}");
        Console.WriteLine($"Pairs: {pairs.Count}");
        Console.WriteLine($"Games per pair: {gamesPerPair} total, split evenly by color");
        Console.WriteLine($"Board: {config.BoardSize}x{config.BoardSize}");
        Console.WriteLine($"Iteration limit: {iterationLimit}");
        Console.WriteLine($"Move time limit: {FormatMoveTimeLimit(moveTimeLimit)}");
        Console.WriteLine($"Seed: {baseSeed}");
        Console.WriteLine($"Run id: {runId}");
        if (resume)
        {
            Console.WriteLine($"Resume: {records.Count} valid existing rows kept, {invalidExistingRows} invalid rows dropped, {ignoredExistingRows} non-matching rows ignored");
            Console.WriteLine($"Remaining games: {plannedGames.Count(game => !completedKeys.Contains(MakeScheduleKey(game)))}");
        }
        Console.WriteLine();

        if (resume)
            RewriteCsv(outputPath, records);

        using var writer = new StreamWriter(outputPath, resume, new UTF8Encoding(false));
        if (!resume)
            WriteCsvHeader(writer);

        bool cancelRequested = false;
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancelRequested = true;
            Console.WriteLine();
            Console.WriteLine("Cancellation requested. Finishing the current game and writing a partial summary...");
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            foreach (var group in plannedGames.GroupBy(game => game.Pair.PairName))
            {
                Console.WriteLine($"=== {group.Key} ===");
                var ranAnyGame = false;

                foreach (var plannedGame in group)
                {
                    if (completedKeys.Contains(MakeScheduleKey(plannedGame)))
                        continue;

                    if (cancelRequested)
                        break;

                    ranAnyGame = true;
                    var record = PlaySingleGame(plannedGame.GameNumber, plannedGame.Pair.PairName, plannedGame.WhiteAgent, plannedGame.BlackAgent, plannedGame.Seed);

                    records.Add(record);
                    completedKeys.Add(MakeScheduleKey(record));
                    WriteCsvRecord(writer, record);
                    writer.Flush();

                    Console.WriteLine($"Game {plannedGame.PairGameNumber:D2}/{gamesPerPair}: {record.WhiteAgent} vs {record.BlackAgent} -> {record.WinnerAgent} ({record.ResultType}) [{record.MoveCount} moves, {record.DurationMs}ms, seed {record.Seed}]");
                }

                if (!ranAnyGame)
                    Console.WriteLine("Already complete.");

                Console.WriteLine();

                if (cancelRequested)
                    break;
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        var summary = BuildSummary(runId, records, outputPath);
        PrintSummary(summary);
        return summary;
    }

    /// <summary>Build aggregate summaries from game rows.</summary>
    public static AllPairsSuiteSummary BuildSummary(string runId, IReadOnlyList<AllPairsGameResultRecord> records, string outputPath)
    {
        var pairSummaries = records
            .GroupBy(record => record.PairName)
            .Select(group =>
            {
                var completed = group.Where(record => !IsError(record)).ToArray();
                var pairAgents = group
                    .SelectMany(record => new[] { record.WhiteAgent, record.BlackAgent })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(agent => agent, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var agentWins = pairAgents.ToDictionary(
                    agent => agent,
                    agent => completed.Count(record => !IsDraw(record) && SameAgent(record.WinnerAgent, agent)),
                    StringComparer.OrdinalIgnoreCase);

                return new PairSummary(
                    group.Key,
                    group.Count(),
                    completed.Length,
                    group.Count(IsError),
                    agentWins,
                    completed.Count(IsDraw),
                    completed.Length > 0 ? completed.Average(record => record.MoveCount) : 0,
                    completed.Length > 0 ? completed.Average(record => record.DurationMs) : 0,
                    completed.Count(record => string.Equals(record.ResultType, ResultType.Road.ToString(), StringComparison.OrdinalIgnoreCase)),
                    completed.Count(record => string.Equals(record.ResultType, ResultType.Flat.ToString(), StringComparison.OrdinalIgnoreCase)));
            })
            .OrderBy(summary => summary.PairName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ranking = BuildRanking(records);
        var errorGames = records.Count(IsError);
        return new AllPairsSuiteSummary(runId, records.Count, records.Count - errorGames, errorGames, pairSummaries, ranking, outputPath);
    }

    /// <summary>Build the overall ranking table from game rows.</summary>
    public static IReadOnlyList<AgentRankingSummary> BuildRanking(IReadOnlyList<AllPairsGameResultRecord> records)
    {
        var agentNames = records
            .SelectMany(record => new[] { record.WhiteAgent, record.BlackAgent })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<AgentRankingSummary>();
        foreach (var agent in agentNames)
        {
            int games = 0;
            int wins = 0;
            int losses = 0;
            int draws = 0;

            foreach (var record in records.Where(record => !IsError(record) && (SameAgent(record.WhiteAgent, agent) || SameAgent(record.BlackAgent, agent))))
            {
                games++;
                if (IsDraw(record))
                {
                    draws++;
                }
                else if (SameAgent(record.WinnerAgent, agent))
                {
                    wins++;
                }
                else
                {
                    losses++;
                }
            }

            rows.Add(new AgentRankingSummary(agent, games, wins, losses, draws, games > 0 ? wins * 100.0 / games : 0));
        }

        return rows
            .OrderByDescending(row => row.WinRate)
            .ThenByDescending(row => row.Wins)
            .ThenBy(row => row.Agent, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<PlannedAllPairsGame> BuildPlannedGames(IReadOnlyList<AgentPair> pairs)
    {
        var plannedGames = new List<PlannedAllPairsGame>(pairs.Count * gamesPerPair);

        for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
        {
            var pair = pairs[pairIndex];
            for (int gameIndex = 0; gameIndex < gamesPerPair; gameIndex++)
            {
                var firstIsWhite = gameIndex % 2 == 0;
                var whiteAgent = firstIsWhite ? pair.FirstAgent : pair.SecondAgent;
                var blackAgent = firstIsWhite ? pair.SecondAgent : pair.FirstAgent;
                plannedGames.Add(new PlannedAllPairsGame(
                    pairIndex * gamesPerPair + gameIndex + 1,
                    gameIndex + 1,
                    pair,
                    whiteAgent,
                    blackAgent,
                    unchecked(baseSeed + pairIndex * 10000 + gameIndex)));
            }
        }

        return plannedGames;
    }

    private AllPairsGameResultRecord PlaySingleGame(int gameNumber, string pairName, string whiteAgentName, string blackAgentName, int seed)
    {
        var whiteDisplayName = AgentFactory.GetDisplayName(whiteAgentName);
        var blackDisplayName = AgentFactory.GetDisplayName(blackAgentName);
        var sw = Stopwatch.StartNew();
        var state = Utils.CreateNewGame(config.BoardSize);
        GameResult? result = null;
        string? error = null;

        try
        {
            var whiteAgent = AgentFactory.CreateAgent(whiteAgentName, unchecked(seed * 2), exploration);
            var blackAgent = AgentFactory.CreateAgent(blackAgentName, unchecked(seed * 2 + 1), exploration);

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

        var winner = error != null
            ? "Error"
            : result?.Winner == Player.White
                ? "White"
                : result?.Winner == Player.Black
                    ? "Black"
                    : "Draw";

        var winnerAgent = error != null
            ? "Error"
            : result?.Winner == Player.White
                ? whiteDisplayName
                : result?.Winner == Player.Black
                    ? blackDisplayName
                    : "Draw";

        return new AllPairsGameResultRecord
        {
            RunId = runId,
            GameNumber = gameNumber,
            PairName = pairName,
            WhiteAgent = whiteDisplayName,
            BlackAgent = blackDisplayName,
            BoardSize = config.BoardSize,
            Winner = winner,
            WinnerAgent = winnerAgent,
            ResultType = error != null ? "Error" : result?.Type.ToString() ?? "Error",
            MoveCount = result?.MoveCount ?? state.MoveHistory.Count,
            DurationMs = sw.ElapsedMilliseconds,
            Seed = seed,
            IterationsLimit = iterationLimit,
            MoveTimeLimitMs = moveTimeLimit.HasValue ? (int)moveTimeLimit.Value.TotalMilliseconds : null,
            Error = error
        };
    }

    private static void PrintSummary(AllPairsSuiteSummary summary)
    {
        Console.WriteLine("=== ALL-PAIRS SUMMARY ===");
        Console.WriteLine($"Run id: {summary.RunId}");
        Console.WriteLine($"Total games: {summary.TotalGames}");
        Console.WriteLine($"Completed games: {summary.CompletedGames}");
        Console.WriteLine($"Errors: {summary.ErrorGames}");
        Console.WriteLine();

        foreach (var pair in summary.PairSummaries)
        {
            Console.WriteLine($"=== {pair.PairName} ===");
            Console.WriteLine($"Games: {pair.Games}");
            foreach (var win in pair.AgentWins.OrderBy(win => win.Key, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"{win.Key} wins: {win.Value}");
            Console.WriteLine($"Draws: {pair.Draws}");
            if (pair.Errors > 0)
                Console.WriteLine($"Errors: {pair.Errors}");
            Console.WriteLine($"Average moves: {pair.AverageMoves.ToString("F1", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Average duration: {pair.AverageDurationMs.ToString("F0", CultureInfo.InvariantCulture)} ms");
            Console.WriteLine($"Road wins: {pair.RoadWins}");
            Console.WriteLine($"Flat wins: {pair.FlatWins}");
            Console.WriteLine();
        }

        Console.WriteLine("=== OVERALL RANKING ===");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-18} {1,5} {2,5} {3,7} {4,6} {5,8}", "Agent", "Games", "Wins", "Losses", "Draws", "WinRate"));
        foreach (var row in summary.Ranking)
        {
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-18} {1,5} {2,5} {3,7} {4,6} {5,7:F1}%",
                row.Agent,
                row.Games,
                row.Wins,
                row.Losses,
                row.Draws,
                row.WinRate));
        }

        Console.WriteLine();
        Console.WriteLine($"Results written to: {summary.OutputPath}");
        Console.WriteLine("Reproducibility note: fixed --seed produces fixed per-game seeds; use --move-time-ms 0 for the most deterministic search-agent runs.");
    }

    private static void WriteCsvHeader(TextWriter writer)
    {
        writer.WriteLine(CsvHeader);
    }

    private static void WriteCsvRecord(TextWriter writer, AllPairsGameResultRecord record)
    {
        writer.WriteLine(string.Join(",",
            Escape(record.RunId),
            Escape(record.GameNumber.ToString(CultureInfo.InvariantCulture)),
            Escape(record.PairName),
            Escape(record.WhiteAgent),
            Escape(record.BlackAgent),
            Escape(record.BoardSize.ToString(CultureInfo.InvariantCulture)),
            Escape(record.Winner),
            Escape(record.WinnerAgent),
            Escape(record.ResultType),
            Escape(record.MoveCount.ToString(CultureInfo.InvariantCulture)),
            Escape(record.DurationMs.ToString(CultureInfo.InvariantCulture)),
            Escape(record.Seed.ToString(CultureInfo.InvariantCulture)),
            Escape(record.IterationsLimit.ToString(CultureInfo.InvariantCulture)),
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

    private ExistingCsvLoadResult LoadExistingRecords(string path, HashSet<string> plannedKeys)
    {
        if (!File.Exists(path))
            return new ExistingCsvLoadResult([], 0, 0);

        var records = new List<AllPairsGameResultRecord>();
        int invalidRows = 0;
        int ignoredRows = 0;
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || string.Equals(line.Trim(), CsvHeader, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!TryParseCsvRecord(line, out var record))
            {
                invalidRows++;
                continue;
            }

            var key = MakeScheduleKey(record);
            if (!plannedKeys.Contains(key) || record.BoardSize != config.BoardSize || record.IterationsLimit != iterationLimit || record.MoveTimeLimitMs != (moveTimeLimit.HasValue ? (int)moveTimeLimit.Value.TotalMilliseconds : null))
            {
                ignoredRows++;
                continue;
            }

            if (seenKeys.Add(key))
                records.Add(record);
            else
                ignoredRows++;
        }

        return new ExistingCsvLoadResult(records.OrderBy(record => record.GameNumber).ToArray(), invalidRows, ignoredRows);
    }

    private static void RewriteCsv(string path, IReadOnlyList<AllPairsGameResultRecord> records)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        WriteCsvHeader(writer);
        foreach (var record in records.OrderBy(record => record.GameNumber))
            WriteCsvRecord(writer, record);
    }

    private static bool TryParseCsvRecord(string line, out AllPairsGameResultRecord record)
    {
        record = new AllPairsGameResultRecord();
        var fields = ParseCsvLine(line);
        if (fields.Count != 15)
            return false;

        if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gameNumber)
            || !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var boardSize)
            || !int.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var moveCount)
            || !long.TryParse(fields[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out var durationMs)
            || !int.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
            || !int.TryParse(fields[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterationsLimit))
        {
            return false;
        }

        int? moveTimeLimitMs = null;
        if (!string.IsNullOrWhiteSpace(fields[13]))
        {
            if (!int.TryParse(fields[13], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMoveTimeLimitMs))
                return false;

            moveTimeLimitMs = parsedMoveTimeLimitMs;
        }

        record = new AllPairsGameResultRecord
        {
            RunId = fields[0],
            GameNumber = gameNumber,
            PairName = fields[2],
            WhiteAgent = fields[3],
            BlackAgent = fields[4],
            BoardSize = boardSize,
            Winner = fields[6],
            WinnerAgent = fields[7],
            ResultType = fields[8],
            MoveCount = moveCount,
            DurationMs = durationMs,
            Seed = seed,
            IterationsLimit = iterationsLimit,
            MoveTimeLimitMs = moveTimeLimitMs,
            Error = string.IsNullOrWhiteSpace(fields[14]) ? null : fields[14]
        };

        return true;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(ch);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }

    private static bool IsError(AllPairsGameResultRecord record)
    {
        return string.Equals(record.ResultType, "Error", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(record.Error);
    }

    private static bool IsDraw(AllPairsGameResultRecord record)
    {
        return string.Equals(record.ResultType, ResultType.Draw.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.Winner, "Draw", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.WinnerAgent, "Draw", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameAgent(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string MakeScheduleKey(PlannedAllPairsGame game)
    {
        return string.Join("|",
            game.Pair.PairName,
            AgentFactory.GetDisplayName(game.WhiteAgent),
            AgentFactory.GetDisplayName(game.BlackAgent),
            game.Seed.ToString(CultureInfo.InvariantCulture));
    }

    private static string MakeScheduleKey(AllPairsGameResultRecord record)
    {
        return string.Join("|",
            record.PairName,
            record.WhiteAgent,
            record.BlackAgent,
            record.Seed.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatMoveTimeLimit(TimeSpan? limit)
    {
        return limit.HasValue ? $"{limit.Value.TotalMilliseconds:0}ms" : "none";
    }
}
