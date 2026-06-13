namespace Tak.AI;

using System.Diagnostics;
using Tak.Core;
using Tak.AI.Mcts;

/// <summary>UCT agent with Progressive Widening</summary>
public class ProgressiveWideningAgent : Agent
{
    private readonly double explorationConstant;
    private readonly double c_pw;
    private readonly double alpha;
    private readonly Random random;

    /// <inheritdoc />
    public override string Name => "PW";

    /// <summary>Create a progressive widening MCTS agent with an optional deterministic seed.</summary>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <param name="c_pw">The progressive widening scale constant.</param>
    /// <param name="alpha">The progressive widening growth exponent.</param>
    /// <param name="seed">The optional random seed.</param>
    public ProgressiveWideningAgent(double explorationConstant = 1.414, double c_pw = 0.5, double alpha = 0.5, int? seed = null)
    {
        this.explorationConstant = explorationConstant;
        this.c_pw = c_pw;
        this.alpha = alpha;
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Choose a move using progressive widening MCTS within the provided limits.</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="timeLimit">The optional time limit.</param>
    /// <param name="iterationLimit">The optional iteration limit.</param>
    /// <returns>The selected move.</returns>
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

        var tree = new PwMctsTree(state, explorationConstant, c_pw, alpha, random.Next());
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
