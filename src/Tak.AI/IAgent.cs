namespace Tak.AI;

using Tak.Core;

/// <summary>Common interface for all AI agents</summary>
public interface IAgent
{
    /// <summary>Agent name for logging/display</summary>
    string Name { get; }

    /// <summary>Choose a move for the given game state</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="timeLimit">The optional time limit for choosing a move.</param>
    /// <param name="iterationLimit">The optional search iteration limit.</param>
    /// <returns>The selected move.</returns>
    Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null);
}

/// <summary>Base class for agents</summary>
public abstract class Agent : IAgent, IGameAgent
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null);

    Move IGameAgent.ChooseMove(GameState state, TimeSpan? timeLimit) => ChooseMove(state, timeLimit);
}
