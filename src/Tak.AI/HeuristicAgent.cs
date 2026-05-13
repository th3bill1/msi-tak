namespace Tak.AI;

using Tak.Core;

/// <summary>Heuristic agent using greedy evaluation</summary>
public class HeuristicAgent : Agent
{
    private readonly Random random;

    public override string Name => "Heuristic";

    public HeuristicAgent(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

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

        // 1. Immediate win
        if (testState.Result?.Winner == state.CurrentPlayer)
            return 100000;

        // 2. Block opponent's immediate win
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

        // 3. Prefer placement moves that increase control
        if (move is PlaceMove pm)
        {
            score += 10;
        }

        // 4. Prefer moves extending road
        if (move is SlideMove)
        {
            score += 5;
        }

        // 5. Count controlled flat stones
        int flatCount = 0;
        foreach (var (_, stack) in testState.Board.GetNonEmptySquares())
        {
            if (stack.Owner == state.CurrentPlayer && stack.TopPiece.IsFlat)
                flatCount++;
        }
        score += flatCount * 0.1;

        return score;
    }
}
