namespace Tak.AI.Mcts;

using Tak.Core;

/// <summary>MCTS tree manager</summary>
public class MctsTree
{
    private readonly MctsNode root;
    private readonly Random random;
    private readonly double explorationConstant;

    public MctsTree(GameState initialState, double explorationConstant, int? seed = null)
    {
        root = new MctsNode(initialState);
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.explorationConstant = explorationConstant;
    }

    /// <summary>Run one MCTS iteration</summary>
    public void RunIteration()
    {
        // Selection
        var node = Selection(root);

        // Expansion
        if (!node.IsTerminal && !node.IsFullyExpanded)
        {
            node.InitializeChildren();
            var child = node.SelectUnvisitedChild(random);
            if (child != null)
                node = child;
        }

        // Simulation
        double reward = Simulation(node);

        // Backpropagation
        node.Backpropagate(reward);
    }

    /// <summary>Selection phase - traverse tree using UCT</summary>
    private MctsNode Selection(MctsNode node)
    {
        while (!node.IsTerminal && node.IsFullyExpanded)
        {
            var bestChild = node.SelectBestChild(explorationConstant);
            if (bestChild == null)
                return node;
            node = bestChild;
        }
        return node;
    }

    /// <summary>Simulation phase - random playout</summary>
    private double Simulation(MctsNode node)
    {
        var state = node.State.Clone();

        while (state.Result == null)
        {
            var moves = GameRules.GetLegalMoves(state).ToList();
            if (moves.Count == 0)
                break;

            var move = moves[random.Next(moves.Count)];
            state = state.MakeMove(move);
        }

        return EvaluateTerminalState(state, root.State.CurrentPlayer);
    }

    private double EvaluateTerminalState(GameState state, Player rootPlayer)
    {
        if (state.Result == null)
            return 0.0; // Shouldn't happen

        if (state.Result.Type == ResultType.Draw)
            return 0.5;

        return state.Result.Winner == rootPlayer ? 1.0 : 0.0;
    }

    /// <summary>Choose best move (most visited child)</summary>
    public Move GetBestMove()
    {
        root.InitializeChildren();
        var bestChild = root.SelectMostVisitedChild();
        
            // If no children were expanded (e.g., game already won or no legal moves found during search)
            // try to find any legal move from root
            if (bestChild?.Move == null)
            {
                var legalMoves = GameRules.GetLegalMoves(root.State).ToList();
                if (legalMoves.Count == 0)
                    throw new InvalidOperationException("No legal moves available from root state");
            
                // Return first legal move as fallback (shouldn't normally happen)
                return legalMoves[0];
            }
        return bestChild.Move;
    }

    /// <summary>Get root node</summary>
    public MctsNode GetRoot() => root;

    /// <summary>Get statistics</summary>
    public (int visits, double winRate) GetRootStats()
    {
        return (root.Visits, root.Wins / Math.Max(1, root.Visits));
    }
}
