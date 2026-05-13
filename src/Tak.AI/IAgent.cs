namespace Tak.AI;

using Tak.Core;

/// <summary>Common interface for all AI agents</summary>
public interface IAgent
{
    /// <summary>Agent name for logging/display</summary>
    string Name { get; }

    /// <summary>Choose a move for the given game state</summary>
    Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null);
}

/// <summary>Base class for agents</summary>
public abstract class Agent : IAgent, IGameAgent
{
    public abstract string Name { get; }
    public abstract Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null);

    Move IGameAgent.ChooseMove(GameState state, TimeSpan? timeLimit) => ChooseMove(state, timeLimit);
}
