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

    public MctsNode(GameState state)
    {
        State = state;
    }

    /// <summary>Check if node is fully expanded</summary>
    public bool IsFullyExpanded => unvisitedChildren == null || unvisitedChildren.Count == 0;

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
        double exploration = c * Math.Sqrt(Math.Log(Visits) / node.Visits);
        return exploitation + exploration;
    }

    /// <summary>Backpropagate result up the tree</summary>
    public void Backpropagate(double reward)
    {
        Visits++;
        Wins += reward;

        Parent?.Backpropagate(reward);
    }

    /// <summary>Get all children (initialized or not)</summary>
    public IEnumerable<MctsNode> GetChildren()
    {
        return children?.Values ?? Enumerable.Empty<MctsNode>();
    }
}
