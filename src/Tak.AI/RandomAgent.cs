namespace Tak.AI;

using Tak.Core;

/// <summary>Random agent - selects random legal moves</summary>
public class RandomAgent : Agent
{
    private readonly Random random;

    public override string Name => "Random";

    public RandomAgent(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        var moves = GameRules.GetLegalMoves(state).ToList();
        if (moves.Count == 0)
            throw new InvalidOperationException("No legal moves available");
        return moves[random.Next(moves.Count)];
    }
}
