namespace Tak.AI;

using Tak.Core;
using Tak.AI.Mcts;

/// <summary>UCT agent with RAVE (Rapid Action Value Estimation)</summary>
public class RaveAgent : Agent
{
    private readonly double explorationConstant;
    private readonly int? seed;

    public override string Name => "RAVE";

    public RaveAgent(double explorationConstant = 1.414, int? seed = null)
    {
        this.explorationConstant = explorationConstant;
        this.seed = seed;
    }

    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        iterationLimit ??= 1000;

        var tree = new RaveMctsTree(state, explorationConstant, seed);

        for (int i = 0; i < iterationLimit; i++)
        {
            tree.RunIteration();
        }

        return tree.GetBestMove();
    }
}
