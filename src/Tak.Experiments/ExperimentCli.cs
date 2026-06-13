namespace Tak.Experiments;

using System.Globalization;

public sealed record ExperimentOptions(
    int BoardSize,
    string WhiteAgent,
    string BlackAgent,
    int Games,
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
        int boardSize = 4;
        string whiteAgent = "random";
        string blackAgent = "heuristic";
        int games = 2;
        int iterationLimit = 100;
        int moveTimeLimitMs = 50;
        int seed = Random.Shared.Next();
        double exploration = 1.414;
        string outputPath = "results/tournament.csv";
        bool helpRequested = false;

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
                case "--board":
                case "--board-size":
                    boardSize = ParsePositiveInt(args, ref i, argument);
                    break;
                case "--white":
                case "--agent-a":
                    whiteAgent = ParseValue(args, ref i, argument);
                    break;
                case "--black":
                case "--agent-b":
                    blackAgent = ParseValue(args, ref i, argument);
                    break;
                case "--games":
                    games = ParsePositiveInt(args, ref i, argument);
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
                    outputPath = ParseValue(args, ref i, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        return new ExperimentOptions(
            boardSize,
            whiteAgent,
            blackAgent,
            games,
            iterationLimit,
            moveTimeLimitMs,
            seed,
            exploration,
            outputPath,
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
  --games <n>           Total games to play, alternating colors (default: 2)
  --board, --board-size  Board size: 4, 5, or 6 (default: 4)
  --white, --agent-a     White-side agent (default: random)
  --black, --agent-b     Black-side agent (default: heuristic)
  --iterations <n>      Iteration limit for search agents (default: 100)
  --move-time-ms <n>    Per-move time limit in milliseconds; use 0 for none (default: 50)
  --seed <n>            Base seed recorded per game (default: random)
  --exploration <n>     Exploration constant used by UCT-style agents (default: 1.414)
  --output <path>       CSV output path (default: results/tournament.csv)
  --help                Show this help text

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
}