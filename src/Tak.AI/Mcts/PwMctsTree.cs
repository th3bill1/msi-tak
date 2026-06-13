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

    public PwMctsTree(GameState initialState, double explorationConstant, double c_pw, double alpha, int? seed = null)
    {
        root = new PwMctsNode(initialState, c_pw, alpha);
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.explorationConstant = explorationConstant;
        this.c_pw = c_pw;
        this.alpha = alpha;
    }

    public void RunIteration()
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
        double reward = Simulation(node, mover);
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

    private double Simulation(PwMctsNode node, Player perspective)
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
    public PwMctsNode? Parent { get; set; }
    public Move? Move { get; set; }
    public GameState State { get; }
    public int Visits { get; set; } = 0;
    public double Wins { get; set; } = 0;

    private Dictionary<Move, PwMctsNode>? children;
    private List<Move>? unvisitedChildren;
    private readonly double c_pw;
    private readonly double alpha;

    public PwMctsNode(GameState state, double c_pw, double alpha)
    {
        State = state;
        this.c_pw = c_pw;
        this.alpha = alpha;
    }

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

    public bool IsTerminal => State.Result != null;

    public void InitializeChildren()
    {
        if (unvisitedChildren != null)
            return;

        unvisitedChildren = GameRules.GetLegalMoves(State).ToList();
        children = new Dictionary<Move, PwMctsNode>();
    }

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
        double exploration = c * Math.Sqrt(Math.Log(Visits) / node.Visits);
        return exploitation + exploration;
    }

    public void Backpropagate(double reward)
    {
        Visits++;
        Wins += reward;

        Parent?.Backpropagate(1.0 - reward);
    }

    public IEnumerable<PwMctsNode> GetChildren() => children?.Values ?? Enumerable.Empty<PwMctsNode>();
}
