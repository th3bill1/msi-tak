namespace Tak.Experiments;

using Tak.AI;

/// <summary>Factory for creating agents from command line parameters</summary>
public class AgentFactory
{
    public static IAgent CreateAgent(string agentName, int? seed = null, double explorationConstant = 1.414)
    {
        return agentName.ToLowerInvariant() switch
        {
            "random" => new RandomAgent(seed),
            "heuristic" => new HeuristicAgent(seed),
            "uct" => new UctAgent(explorationConstant, seed),
            "rave" => new RaveAgent(explorationConstant, seed),
            "pw" => new ProgressiveWideningAgent(explorationConstant, seed: seed),
            _ => throw new ArgumentException($"Unknown agent: {agentName}")
        };
    }
}
