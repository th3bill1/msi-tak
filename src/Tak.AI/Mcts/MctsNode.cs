namespace Tak.AI.Mcts;

using Tak.Core;

/// <summary>MCTS tree node</summary>
public class MctsNode
{
    /// <summary>Parent node (null for root)</summary>
    public MctsNode? Parent { get; set; }

    /// <summary>The move that led to this node from parent</summary>
    public Move? Move { get; set; }

    /// <summary>Game state at this node</summary>
    public GameState State { get; }

    /// <summary>Number of times this node was visited</summary>
    public int Visits { get; set; } = 0;

    /// <summary>Number of wins (or accumulated reward)</summary>
    public double Wins { get; set; } = 0;

    /// <summary>Child nodes (lazy-initialized)</summary>
    private Dictionary<Move, MctsNode>? children;

    /// <summary>Unvisited children</summary>
    private List<Move>? unvisitedChildren;

    /// <summary>Creates a node for the supplied game state.</summary>
    /// <param name="state">The game state represented by the node.</param>
    public MctsNode(GameState state)
    {
        State = state;
    }

    /// <summary>Check if node is fully expanded (all legal children have been added to the tree).
    /// Returns false before InitializeChildren has been called - that case means "not yet expanded at all",
    /// which must trigger expansion, not be treated as already complete.</summary>
    public bool IsFullyExpanded => unvisitedChildren != null && unvisitedChildren.Count == 0;

    /// <summary>Check if node is terminal (game over)</summary>
    public bool IsTerminal => State.Result != null;

    /// <summary>Initialize unvisited children list</summary>
    public void InitializeChildren()
    {
        if (unvisitedChildren != null)
            return;

        unvisitedChildren = GameRules.GetLegalMoves(State).ToList();
        children = new Dictionary<Move, MctsNode>();
    }

    /// <summary>Get an unvisited child and add it to visited</summary>
    /// <param name="random">The random source used to select an unvisited child.</param>
    /// <returns>The newly expanded child node, or <see langword="null" /> if none are available.</returns>
    public MctsNode? SelectUnvisitedChild(Random random)
    {
        if (unvisitedChildren == null || unvisitedChildren.Count == 0)
            return null;

        var move = unvisitedChildren[random.Next(unvisitedChildren.Count)];
        unvisitedChildren.Remove(move);

        var childState = State.MakeMove(move);
        var child = new MctsNode(childState) { Parent = this, Move = move };
        children![move] = child;

        return child;
    }

    /// <summary>Get best child by UCT formula</summary>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <returns>The child with the highest UCT value, or <see langword="null" /> if there are no children.</returns>
    public MctsNode? SelectBestChild(double explorationConstant)
    {
        if (children == null || children.Count == 0)
            return null;

        double bestValue = double.NegativeInfinity;
        MctsNode? bestChild = null;

        foreach (var child in children.Values)
        {
            double uct = CalculateUct(child, explorationConstant);
            if (uct > bestValue)
            {
                bestValue = uct;
                bestChild = child;
            }
        }

        return bestChild;
    }

    /// <summary>Get child with most visits</summary>
    /// <returns>The most visited child, or <see langword="null" /> if there are no children.</returns>
    public MctsNode? SelectMostVisitedChild()
    {
        if (children == null || children.Count == 0)
            return null;

        return children.Values.OrderByDescending(c => c.Visits).FirstOrDefault();
    }

    private double CalculateUct(MctsNode node, double c)
    {
        if (node.Visits == 0)
            return double.PositiveInfinity;

        double exploitation = node.Wins / node.Visits;
        double exploration = c * Math.Sqrt(Math.Log(Math.Max(1, Visits)) / node.Visits);
        return exploitation + exploration;
    }

    /// <summary>Backpropagate result up the tree.
    /// Reward is expressed from the perspective of the player who made the move into THIS node.
    /// Because Tak is a two-player zero-sum game with alternating moves, the move into the parent
    /// was made by the opponent, so the reward must be negated (1 - r) at each level.</summary>
    /// <param name="reward">The reward to add at this node.</param>
    public void Backpropagate(double reward)
    {
        Visits++;
        Wins += reward;

        Parent?.Backpropagate(1.0 - reward);
    }

    /// <summary>Get all children (initialized or not)</summary>
    /// <returns>The expanded child nodes.</returns>
    public IEnumerable<MctsNode> GetChildren()
    {
        return children?.Values ?? Enumerable.Empty<MctsNode>();
    }
}
