namespace Tak.AI;

using Tak.Core;
using Tak.AI.Mcts;

/// <summary>UCT agent with Progressive Widening</summary>
public class ProgressiveWideningAgent : Agent
{
    private readonly double explorationConstant;
    private readonly double c_pw;
    private readonly double alpha;
    private readonly int? seed;

    public override string Name => "PW";

    public ProgressiveWideningAgent(double explorationConstant = 1.414, double c_pw = 0.5, double alpha = 0.5, int? seed = null)
    {
        this.explorationConstant = explorationConstant;
        this.c_pw = c_pw;
        this.alpha = alpha;
        this.seed = seed;
    }

    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        iterationLimit ??= 1000;

        var tree = new PwMctsTree(state, explorationConstant, c_pw, alpha, seed);

        for (int i = 0; i < iterationLimit; i++)
        {
            tree.RunIteration();
        }

        return tree.GetBestMove();
    }
}
