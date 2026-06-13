namespace Tak.Experiments;

using Tak.AI;

/// <summary>Factory for creating agents from command-line parameters.</summary>
public class AgentFactory
{
    private static readonly Dictionary<string, string> DisplayNamesByKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["random"] = "Random",
        ["heuristic"] = "Heuristic",
        ["uct"] = "UCT",
        ["rave"] = "RAVE",
        ["pw"] = "PW"
    };

    public static IReadOnlyList<string> SupportedAgentNames { get; } =
    [
        "random",
        "heuristic",
        "uct",
        "rave",
        "pw"
    ];

    /// <summary>Normalize and validate an agent name for command-line use.</summary>
    public static string NormalizeAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name cannot be empty.", nameof(agentName));

        var normalized = agentName.Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            "mcts" => "uct",
            "progressivewidening" => "pw",
            "progressive-widening" => "pw",
            "progressive_widening" => "pw",
            _ => normalized
        };

        if (!DisplayNamesByKey.ContainsKey(normalized))
            throw new ArgumentException($"Unknown agent: {agentName}. Supported agents: {string.Join(", ", SupportedAgentNames)}.", nameof(agentName));

        return normalized;
    }

    /// <summary>Return the display name used in experiment output for an agent.</summary>
    public static string GetDisplayName(string agentName) => DisplayNamesByKey[NormalizeAgentName(agentName)];

    /// <summary>Creates an AI agent from a command-line agent name.</summary>
    public static IAgent CreateAgent(string agentName, int? seed = null, double explorationConstant = 1.414)
    {
        return NormalizeAgentName(agentName) switch
        {
            "random" => new RandomAgent(seed),
            "heuristic" => new HeuristicAgent(seed),
            "uct" => new UctAgent(explorationConstant, seed),
            "rave" => new RaveAgent(explorationConstant, seed),
            "pw" => new ProgressiveWideningAgent(explorationConstant, seed: seed),
            _ => throw new InvalidOperationException("Validated agent name was not handled.")
        };
    }
}
