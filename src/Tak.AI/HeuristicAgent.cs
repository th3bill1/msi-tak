namespace Tak.AI;

using Tak.Core;

/// <summary>Heuristic agent using greedy evaluation</summary>
public class HeuristicAgent : Agent
{
    private readonly Random random;

    /// <inheritdoc />
    public override string Name => "Heuristic";

    /// <summary>Create a heuristic agent with an optional deterministic seed.</summary>
    /// <param name="seed">The optional random seed used to break score ties.</param>
    public HeuristicAgent(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Choose the highest-scoring legal move using the built-in heuristic.</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="timeLimit">The optional time limit. This agent does not use it.</param>
    /// <param name="iterationLimit">The optional iteration limit. This agent does not use it.</param>
    /// <returns>The selected move.</returns>
    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        var moves = GameRules.GetLegalMoves(state).ToList();
        if (moves.Count == 0)
            throw new InvalidOperationException("No legal moves available");

        var scored = moves.Select(m => (move: m, score: ScoreMove(state, m))).ToList();
        var maxScore = scored.Max(x => x.score);
        var bestMoves = scored.Where(x => x.score == maxScore).Select(x => x.move).ToList();

        return bestMoves[random.Next(bestMoves.Count)];
    }

    private double ScoreMove(GameState state, Move move)
    {
        var testState = state.Clone();
        testState = testState.MakeMove(move);

        double score = 0;

        if (testState.Result?.Winner == state.CurrentPlayer)
            return 100000;

        var opponentMoves = GameRules.GetLegalMoves(testState).ToList();
        foreach (var opMove in opponentMoves)
        {
            var opTestState = testState.Clone();
            opTestState = opTestState.MakeMove(opMove);
            if (opTestState.Result?.Winner == state.CurrentPlayer.Opponent())
            {
                score -= 10000;
                break;
            }
        }

        if (move is PlaceMove)
        {
            score += 2;
        }

        if (move is SlideMove)
        {
            score += 1;
        }

        return score;
    }
}
