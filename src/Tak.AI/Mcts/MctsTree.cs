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
        root = new MctsNode(initialState.Clone());
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.explorationConstant = explorationConstant;
    }

    /// <summary>Run one MCTS iteration</summary>
    public void RunIteration(int maxRolloutMoves = 512)
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

        // Simulation - reward is from the perspective of the player who moved INTO `node`.
        // If `node` is the root (no parent), there is no "last mover", so we evaluate from
        // the current player's perspective; root statistics aren't used for move selection anyway.
        Player mover = node.Parent?.State.CurrentPlayer ?? node.State.CurrentPlayer;
        double reward = Simulation(node, mover, maxRolloutMoves);

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

    /// <summary>Simulation phase - random playout from `node`, returning reward from `perspective`'s POV</summary>
    private double Simulation(MctsNode node, Player perspective, int maxRolloutMoves)
    {
        var state = node.State.Clone();
        int depth = 0;

        while (state.Result == null && depth < maxRolloutMoves)
        {
            var moves = GameRules.GetLegalMoves(state).ToList();
            if (moves.Count == 0)
                break;

            var move = moves[random.Next(moves.Count)];
            state = state.MakeMove(move);
            depth++;
        }

        return EvaluateTerminalState(state, perspective);
    }

    private double EvaluateTerminalState(GameState state, Player perspective)
    {
        if (state.Result == null)
            return 0.5; // No result (e.g. stalemate at depth limit) — treat as draw

        if (state.Result.Type == ResultType.Draw)
            return 0.5;

        return state.Result.Winner == perspective ? 1.0 : 0.0;
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
