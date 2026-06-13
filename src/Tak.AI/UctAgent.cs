namespace Tak.AI;

using System.Diagnostics;
using Tak.Core;
using Tak.AI.Mcts;

/// <summary>Standard UCT (Upper Confidence Bounds applied to Trees) agent</summary>
public class UctAgent : Agent
{
    private readonly double explorationConstant;
    private readonly Random random;

    public override string Name => "UCT";

    public UctAgent(double explorationConstant = 1.414, int? seed = null)
    {
        this.explorationConstant = explorationConstant;
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        iterationLimit ??= 1000;
        var legalMoves = GameRules.GetLegalMoves(state).ToList();
        if (legalMoves.Count == 0)
            throw new InvalidOperationException("No legal moves available");

        var immediateWin = TacticalMoveFinder.FindImmediateWinningMove(state, legalMoves);
        if (immediateWin != null)
            return immediateWin;

        var immediateBlock = TacticalMoveFinder.FindImmediateOpponentWinBlock(state, legalMoves);
        if (immediateBlock != null)
            return immediateBlock;

        var tree = new MctsTree(state, explorationConstant, random.Next());
        var stopwatch = Stopwatch.StartNew();
        var maxRolloutMoves = Math.Max(32, state.Config.BoardSize * state.Config.BoardSize * 2);

        for (int i = 0; i < iterationLimit && (!timeLimit.HasValue || stopwatch.Elapsed < timeLimit.Value); i++)
        {
            tree.RunIteration(maxRolloutMoves);
        }

        var selected = tree.GetBestMove();
        return legalMoves.Contains(selected) ? selected : legalMoves[0];
    }
}
