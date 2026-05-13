namespace Tak.AI.Mcts;

using Tak.Core;

/// <summary>MCTS tree with RAVE support</summary>
public class RaveMctsTree
{
    private readonly RaveMctsNode root;
    private readonly Random random;
    private readonly double explorationConstant;

    public RaveMctsTree(GameState initialState, double explorationConstant, int? seed = null)
    {
        root = new RaveMctsNode(initialState);
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.explorationConstant = explorationConstant;
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

        double reward = Simulation(node);
        node.BackpropagateWithRave(reward, root.State.CurrentPlayer);
    }

    private RaveMctsNode Selection(RaveMctsNode node)
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

    private double Simulation(RaveMctsNode node)
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
            return 0.0;

        if (state.Result.Type == ResultType.Draw)
            return 0.5;

        return state.Result.Winner == rootPlayer ? 1.0 : 0.0;
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

/// <summary>MCTS node with RAVE support</summary>
public class RaveMctsNode
{
    public RaveMctsNode? Parent { get; set; }
    public Move? Move { get; set; }
    public GameState State { get; }
    public int Visits { get; set; } = 0;
    public double Wins { get; set; } = 0;

    private Dictionary<Move, RaveMctsNode>? children;
    private List<Move>? unvisitedChildren;
    private Dictionary<Move, RaveData>? raveStats;

    public RaveMctsNode(GameState state)
    {
        State = state;
    }

    public bool IsFullyExpanded => unvisitedChildren == null || unvisitedChildren.Count == 0;
    public bool IsTerminal => State.Result != null;

    public void InitializeChildren()
    {
        if (unvisitedChildren != null)
            return;

        unvisitedChildren = GameRules.GetLegalMoves(State).ToList();
        children = new Dictionary<Move, RaveMctsNode>();
        raveStats = new Dictionary<Move, RaveData>();

        foreach (var move in unvisitedChildren)
        {
            raveStats[move] = new RaveData();
        }
    }

    public RaveMctsNode? SelectUnvisitedChild(Random random)
    {
        if (unvisitedChildren == null || unvisitedChildren.Count == 0)
            return null;

        var move = unvisitedChildren[random.Next(unvisitedChildren.Count)];
        unvisitedChildren.Remove(move);

        var childState = State.MakeMove(move);
        var child = new RaveMctsNode(childState) { Parent = this, Move = move };
        children![move] = child;

        return child;
    }

    public RaveMctsNode? SelectBestChild(double explorationConstant)
    {
        if (children == null || children.Count == 0)
            return null;

        double bestValue = double.NegativeInfinity;
        RaveMctsNode? bestChild = null;

        foreach (var (move, child) in children)
        {
            double uct = CalculateRaveMixedValue(child, move, explorationConstant);
            if (uct > bestValue)
            {
                bestValue = uct;
                bestChild = child;
            }
        }

        return bestChild;
    }

    public RaveMctsNode? SelectMostVisitedChild()
    {
        if (children == null || children.Count == 0)
            return null;

        return children.Values.OrderByDescending(c => c.Visits).FirstOrDefault();
    }

    private double CalculateRaveMixedValue(RaveMctsNode node, Move move, double c)
    {
        if (node.Visits == 0)
            return double.PositiveInfinity;

        // UCT value
        double exploitation = node.Wins / node.Visits;
        double exploration = c * Math.Sqrt(Math.Log(Visits) / node.Visits);
        double uctValue = exploitation + exploration;

        // RAVE value
        if (raveStats != null && raveStats.TryGetValue(move, out var rave))
        {
            double raveValue = rave.GetRaveValue();

            // Beta weighting: blend UCT and RAVE
            int raveVisits = rave.RaveVisits;
            double beta = raveVisits / (node.Visits + raveVisits + 0.001);

            return beta * raveValue + (1 - beta) * uctValue;
        }

        return uctValue;
    }

    public void BackpropagateWithRave(double reward, Player rootPlayer)
    {
        Visits++;
        Wins += reward;

        Parent?.BackpropagateWithRave(reward, rootPlayer);
    }

    public IEnumerable<RaveMctsNode> GetChildren() => children?.Values ?? Enumerable.Empty<RaveMctsNode>();
}
