namespace Tak.AI.Mcts;

using Tak.Core;

/// <summary>MCTS tree with Progressive Widening</summary>
public class PwMctsTree
{
    private readonly PwMctsNode root;
    private readonly Random random;
    private readonly double explorationConstant;
    private readonly double c_pw;
    private readonly double alpha;

    /// <summary>Creates a progressive widening MCTS tree rooted at the supplied state.</summary>
    /// <param name="initialState">The state to search from.</param>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <param name="c_pw">The progressive widening scale constant.</param>
    /// <param name="alpha">The progressive widening growth exponent.</param>
    /// <param name="seed">The optional random seed.</param>
    public PwMctsTree(GameState initialState, double explorationConstant, double c_pw, double alpha, int? seed = null)
    {
        root = new PwMctsNode(initialState.Clone(), c_pw, alpha);
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.explorationConstant = explorationConstant;
        this.c_pw = c_pw;
        this.alpha = alpha;
    }

    /// <summary>Runs one progressive widening MCTS iteration.</summary>
    /// <param name="maxRolloutMoves">The maximum number of moves to simulate in the rollout.</param>
    public void RunIteration(int maxRolloutMoves = 512)
    {
        var node = Selection(root);

        if (!node.IsTerminal && !node.IsFullyExpanded)
        {
            node.InitializeChildren();
            var child = node.SelectUnvisitedChild(random);
            if (child != null)
                node = child;
        }

        Player mover = node.Parent?.State.CurrentPlayer ?? node.State.CurrentPlayer;
        double reward = Simulation(node, mover, maxRolloutMoves);
        node.Backpropagate(reward);
    }

    private PwMctsNode Selection(PwMctsNode node)
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

    private double Simulation(PwMctsNode node, Player perspective, int maxRolloutMoves)
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
            return 0.5;

        if (state.Result.Type == ResultType.Draw)
            return 0.5;

        return state.Result.Winner == perspective ? 1.0 : 0.0;
    }

    /// <summary>Chooses the best root move by visit count.</summary>
    /// <returns>The move belonging to the most visited root child.</returns>
    public Move GetBestMove()
    {
        root.InitializeChildren();
        var bestChild = root.SelectMostVisitedChild();
        
            // If no children were expanded, try to find any legal move from root
            if (bestChild?.Move == null)
            {
                var legalMoves = GameRules.GetLegalMoves(root.State).ToList();
                if (legalMoves.Count == 0)
                    throw new InvalidOperationException("No legal moves available from root state");
                return legalMoves[0];
            }
        
        return bestChild.Move;
    }
}

/// <summary>MCTS node with Progressive Widening support</summary>
public class PwMctsNode
{
    /// <summary>Gets or sets the parent node, or <see langword="null" /> for the root.</summary>
    public PwMctsNode? Parent { get; set; }

    /// <summary>Gets or sets the move that led from the parent to this node.</summary>
    public Move? Move { get; set; }

    /// <summary>Gets the game state represented by this node.</summary>
    public GameState State { get; }

    /// <summary>Gets or sets the number of visits to this node.</summary>
    public int Visits { get; set; } = 0;

    /// <summary>Gets or sets the accumulated reward for this node.</summary>
    public double Wins { get; set; } = 0;

    private Dictionary<Move, PwMctsNode>? children;
    private List<Move>? unvisitedChildren;
    private readonly double c_pw;
    private readonly double alpha;

    /// <summary>Creates a progressive widening node for the supplied game state.</summary>
    /// <param name="state">The game state represented by the node.</param>
    /// <param name="c_pw">The progressive widening scale constant.</param>
    /// <param name="alpha">The progressive widening growth exponent.</param>
    public PwMctsNode(GameState state, double c_pw, double alpha)
    {
        State = state;
        this.c_pw = c_pw;
        this.alpha = alpha;
    }

    /// <summary>Gets whether the node has reached its current progressive widening child limit.</summary>
    public bool IsFullyExpanded
    {
        get
        {
            if (unvisitedChildren == null)
                return false;

            // Progressive widening: k(n) = floor(c_pw * n^alpha)
            int maxChildren = (int)(c_pw * Math.Pow(Visits, alpha));
            return children != null && children.Count >= maxChildren;
        }
    }

    /// <summary>Gets whether the node represents a terminal game state.</summary>
    public bool IsTerminal => State.Result != null;

    /// <summary>Initializes the node's unvisited child move list.</summary>
    public void InitializeChildren()
    {
        if (unvisitedChildren != null)
            return;

        unvisitedChildren = GameRules.GetLegalMoves(State).ToList();
        children = new Dictionary<Move, PwMctsNode>();
    }

    /// <summary>Selects and expands one unvisited child if progressive widening permits it.</summary>
    /// <param name="random">The random source used to choose the move.</param>
    /// <returns>The newly expanded child node, or <see langword="null" /> if none can be expanded.</returns>
    public PwMctsNode? SelectUnvisitedChild(Random random)
    {
        if (unvisitedChildren == null || unvisitedChildren.Count == 0)
            return null;

        // Check if we can expand more children
        int maxChildren = (int)(c_pw * Math.Pow(Visits, alpha));
        if (children!.Count >= maxChildren && maxChildren < unvisitedChildren.Count)
            return null;

        var move = unvisitedChildren[random.Next(unvisitedChildren.Count)];
        unvisitedChildren.Remove(move);

        var childState = State.MakeMove(move);
        var child = new PwMctsNode(childState, c_pw, alpha) { Parent = this, Move = move };
        children[move] = child;

        return child;
    }

    /// <summary>Selects the expanded child with the highest UCT value.</summary>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <returns>The best child node, or <see langword="null" /> if there are no children.</returns>
    public PwMctsNode? SelectBestChild(double explorationConstant)
    {
        if (children == null || children.Count == 0)
            return null;

        double bestValue = double.NegativeInfinity;
        PwMctsNode? bestChild = null;

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

    /// <summary>Selects the expanded child with the most visits.</summary>
    /// <returns>The most visited child, or <see langword="null" /> if there are no children.</returns>
    public PwMctsNode? SelectMostVisitedChild()
    {
        if (children == null || children.Count == 0)
            return null;

        return children.Values.OrderByDescending(c => c.Visits).FirstOrDefault();
    }

    private double CalculateUct(PwMctsNode node, double c)
    {
        if (node.Visits == 0)
            return double.PositiveInfinity;

        double exploitation = node.Wins / node.Visits;
        double exploration = c * Math.Sqrt(Math.Log(Math.Max(1, Visits)) / node.Visits);
        return exploitation + exploration;
    }

    /// <summary>Backpropagates a reward through this node and its ancestors.</summary>
    /// <param name="reward">The reward to add at this node.</param>
    public void Backpropagate(double reward)
    {
        Visits++;
        Wins += reward;

        Parent?.Backpropagate(1.0 - reward);
    }

    /// <summary>Gets the expanded children of this node.</summary>
    /// <returns>The expanded child nodes.</returns>
    public IEnumerable<PwMctsNode> GetChildren() => children?.Values ?? Enumerable.Empty<PwMctsNode>();
}
