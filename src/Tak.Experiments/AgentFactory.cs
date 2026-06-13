namespace Tak.Experiments;

using Tak.AI;

/// <summary>Factory for creating agents from command line parameters</summary>
public class AgentFactory
{
    public static IReadOnlyList<string> SupportedAgentNames { get; } =
    [
        "random",
        "heuristic",
        "uct",
        "rave",
        "pw"
    ];

    public static IAgent CreateAgent(string agentName, int? seed = null, double explorationConstant = 1.414)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name cannot be empty.", nameof(agentName));

        return agentName.Trim().ToLowerInvariant() switch
        {
            "random" => new RandomAgent(seed),
            "heuristic" => new HeuristicAgent(seed),
            "uct" => new UctAgent(explorationConstant, seed),
            "rave" => new RaveAgent(explorationConstant, seed),
            "pw" => new ProgressiveWideningAgent(explorationConstant, seed: seed),
            _ => throw new ArgumentException($"Unknown agent: {agentName}. Supported agents: {string.Join(", ", SupportedAgentNames)}.", nameof(agentName))
        };
    }
}
