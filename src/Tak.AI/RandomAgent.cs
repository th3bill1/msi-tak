namespace Tak.AI;

using Tak.Core;

/// <summary>Random agent - selects random legal moves</summary>
public class RandomAgent : Agent
{
    private readonly Random random;

    /// <inheritdoc />
    public override string Name => "Random";

    /// <summary>Create a random agent with an optional deterministic seed.</summary>
    /// <param name="seed">The optional random seed.</param>
    public RandomAgent(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Choose a legal move uniformly at random.</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="timeLimit">The optional time limit. This agent does not use it.</param>
    /// <param name="iterationLimit">The optional iteration limit. This agent does not use it.</param>
    /// <returns>The selected move.</returns>
    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        var moves = GameRules.GetLegalMoves(state).ToList();
        if (moves.Count == 0)
            throw new InvalidOperationException("No legal moves available");
        return moves[random.Next(moves.Count)];
    }
}
