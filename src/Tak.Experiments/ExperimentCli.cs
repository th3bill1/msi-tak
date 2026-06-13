namespace Tak.Experiments;

using System.Globalization;

public sealed record ExperimentOptions(
    string Suite,
    int BoardSize,
    string WhiteAgent,
    string BlackAgent,
    int Games,
    int GamesPerPair,
    IReadOnlyList<string> AgentNames,
    bool IncludeSelfPlay,
    bool Resume,
    int IterationLimit,
    int MoveTimeLimitMs,
    int Seed,
    double Exploration,
    string OutputPath,
    bool HelpRequested);

public sealed record AgentSpec(string Name, Func<int, Tak.AI.IAgent> CreateAgent);

/// <summary>Parses and formats command-line options for the experiment runner.</summary>
public static class ExperimentCli
{
    /// <summary>Parse experiment command-line arguments into a validated options record.</summary>
    public static ExperimentOptions Parse(string[] args)
    {
        string? suite = null;
        int? boardSize = null;
        string whiteAgent = "random";
        string blackAgent = "heuristic";
        int? games = null;
        int? gamesPerPair = null;
        int? iterationLimit = null;
        int? moveTimeLimitMs = null;
        int? seed = null;
        double exploration = 1.414;
        string? outputPath = null;
        IReadOnlyList<string>? agentNames = null;
        bool includeSelfPlay = false;
        bool resume = false;
        bool helpRequested = false;
        bool sawSingleMatchOption = false;

        for (int i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            switch (argument)
            {
                case "--help":
                case "-h":
                case "/?":
                    helpRequested = true;
                    break;
                case "--suite":
                    suite = ParseValue(args, ref i, argument).Trim().ToLowerInvariant();
                    break;
                case "--board":
                case "--board-size":
                    boardSize = ParsePositiveInt(args, ref i, argument);
                    break;
                case "--white":
                case "--agent-a":
                    whiteAgent = ParseValue(args, ref i, argument);
                    sawSingleMatchOption = true;
                    break;
                case "--black":
                case "--agent-b":
                    blackAgent = ParseValue(args, ref i, argument);
                    sawSingleMatchOption = true;
                    break;
                case "--games":
                    games = ParsePositiveInt(args, ref i, argument);
                    sawSingleMatchOption = true;
                    break;
                case "--games-per-pair":
                    gamesPerPair = ParsePositiveInt(args, ref i, argument);
                    break;
                case "--agents":
                    agentNames = ParseAgentList(ParseValue(args, ref i, argument));
                    break;
                case "--include-self-play":
                    includeSelfPlay = true;
                    break;
                case "--resume":
                case "--append":
                    resume = true;
                    break;
                case "--iterations":
                    iterationLimit = ParseNonNegativeInt(args, ref i, argument);
                    break;
                case "--move-time-ms":
                    moveTimeLimitMs = ParseNonNegativeInt(args, ref i, argument);
                    break;
                case "--seed":
                    seed = ParseInt(args, ref i, argument);
                    break;
                case "--exploration":
                    exploration = ParseDouble(args, ref i, argument);
                    break;
                case "--output":
                    outputPath = ParseOutputPath(args, ref i, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        var effectiveSuite = suite ?? (sawSingleMatchOption ? "single" : "all-pairs");
        if (effectiveSuite is not "single" and not "all-pairs")
            throw new ArgumentException($"Unknown suite name: {effectiveSuite}. Supported suites: single, all-pairs.");

        var effectiveBoardSize = boardSize ?? (effectiveSuite == "all-pairs" ? 5 : 4);
        _ = new Tak.Core.GameConfig(effectiveBoardSize);

        var effectiveAgents = agentNames ?? AgentFactory.SupportedAgentNames;
        effectiveAgents = effectiveAgents.Select(AgentFactory.NormalizeAgentName).ToArray();
        _ = AgentFactory.NormalizeAgentName(whiteAgent);
        _ = AgentFactory.NormalizeAgentName(blackAgent);

        return new ExperimentOptions(
            effectiveSuite,
            effectiveBoardSize,
            whiteAgent,
            blackAgent,
            games ?? 2,
            gamesPerPair ?? 20,
            effectiveAgents,
            includeSelfPlay,
            resume,
            iterationLimit ?? (effectiveSuite == "all-pairs" ? 1000 : 100),
            moveTimeLimitMs ?? (effectiveSuite == "all-pairs" ? 2000 : 50),
            seed ?? Random.Shared.Next(),
            exploration,
            outputPath ?? GetDefaultOutputPath(effectiveSuite),
            helpRequested);
    }

    /// <summary>Return the CLI usage text for the experiment runner.</summary>
    public static string GetUsage()
    {
        var supportedAgents = string.Join(", ", AgentFactory.SupportedAgentNames);

        return $"""
Tak.Experiments tournament runner

Default command:
  dotnet run --project src/Tak.Experiments

Usage:
  dotnet run --project src/Tak.Experiments -- [options]

Options:
  --suite <name>        Experiment suite: all-pairs or single (default: all-pairs, unless single-match options are used)
  --games-per-pair <n>  Games per unordered pair for all-pairs; split evenly by color (default: 20)
  --agents <list>       Comma-separated agents for all-pairs (default: all supported agents)
  --include-self-play   Include Agent vs same Agent pairs in all-pairs
  --resume, --append    Keep valid rows in --output and append only missing all-pairs games
  --games <n>           Total games to play, alternating colors (default: 2)
  --board, --board-size  Board size: 4, 5, or 6 (default: 5 for all-pairs, 4 for single)
  --white, --agent-a     White-side agent (default: random)
  --black, --agent-b     Black-side agent (default: heuristic)
  --iterations <n>      Iteration limit for search agents (default: 100)
  --move-time-ms <n>    Per-move time limit in milliseconds; use 0 for none (default: 50)
  --seed <n>            Base seed recorded per game (default: random)
  --exploration <n>     Exploration constant used by UCT-style agents (default: 1.414)
  --output <path>       CSV output path (default: timestamped results/all_pairs_*.csv, or results/tournament.csv for single)
  --help                Show this help text

All-pairs example:
  dotnet run --project src/Tak.Experiments -- --suite all-pairs --games-per-pair 20 --board 5 --iterations 1000 --move-time-ms 2000

Supported agents:
  {supportedAgents}
""";
    }

    /// <summary>Convert a millisecond time limit into a nullable <see cref="TimeSpan"/>.</summary>
    /// <summary>Converts a millisecond limit into an optional time span.</summary>
    public static TimeSpan? ToMoveTimeLimit(int moveTimeLimitMs) => moveTimeLimitMs > 0 ? TimeSpan.FromMilliseconds(moveTimeLimitMs) : null;

    private static int ParsePositiveInt(string[] args, ref int index, string optionName)
    {
        var value = ParseInt(args, ref index, optionName);
        if (value < 1)
            throw new ArgumentException($"{optionName} must be at least 1.");

        return value;
    }

    private static int ParseNonNegativeInt(string[] args, ref int index, string optionName)
    {
        var value = ParseInt(args, ref index, optionName);
        if (value < 0)
            throw new ArgumentException($"{optionName} must be 0 or greater.");

        return value;
    }

    private static int ParseInt(string[] args, ref int index, string optionName)
    {
        var value = ParseValue(args, ref index, optionName);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException($"Invalid integer for {optionName}: {value}");

        return parsed;
    }

    private static double ParseDouble(string[] args, ref int index, string optionName)
    {
        var value = ParseValue(args, ref index, optionName);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException($"Invalid number for {optionName}: {value}");

        return parsed;
    }

    private static string ParseValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }

    private static string ParseOutputPath(string[] args, ref int index, string optionName)
    {
        var value = ParseValue(args, ref index, optionName);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{optionName} cannot be empty.");

        if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException($"{optionName} contains invalid path characters: {value}");

        return value;
    }

    private static IReadOnlyList<string> ParseAgentList(string value)
    {
        var agents = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AgentFactory.NormalizeAgentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (agents.Length == 0)
            throw new ArgumentException("--agents must include at least one supported agent name.");

        return agents;
    }

    private static string GetDefaultOutputPath(string suite)
    {
        if (suite == "all-pairs")
        {
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            return Path.Combine("results", $"all_pairs_{stamp}.csv");
        }

        return Path.Combine("results", "tournament.csv");
    }
}
