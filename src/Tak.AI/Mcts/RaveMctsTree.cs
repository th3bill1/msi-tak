namespace Tak.AI.Mcts;

using Tak.Core;

/// <summary>MCTS tree with RAVE (Rapid Action Value Estimation) support</summary>
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
        // Selection: walk down using UCT+RAVE, recording the path of nodes visited
        // and the (move, player) pairs played along the way.
        var path = new List<RaveMctsNode> { root };
        var trajectory = new List<(Move move, Player player)>();

        var node = root;
        while (!node.IsTerminal && node.IsFullyExpanded)
        {
            var bestChild = node.SelectBestChild(explorationConstant);
            if (bestChild == null) break;
            trajectory.Add((bestChild.Move!, node.State.CurrentPlayer));
            node = bestChild;
            path.Add(node);
        }

        // Expansion: add one new child for an untried action.
        if (!node.IsTerminal && !node.IsFullyExpanded)
        {
            node.InitializeChildren();
            var child = node.SelectUnvisitedChild(random);
            if (child != null)
            {
                trajectory.Add((child.Move!, node.State.CurrentPlayer));
                node = child;
                path.Add(node);
            }
        }

        // Simulation: random rollout from `node`, also recording the moves played
        // so RAVE can update all-moves-as-first statistics.
        var (terminalState, rolloutMoves) = Simulate(node);
        trajectory.AddRange(rolloutMoves);

        // Backpropagation with RAVE: walk the path, updating each node's Wins/Visits
        // from the perspective of the mover INTO that node, and updating raveStats for
        // every trajectory move that matches the side-to-move at each node.
        BackpropagateRave(path, trajectory, terminalState);
    }

    /// <summary>Random playout returning the terminal state and the list of moves played with the player who made each move.</summary>
    private (GameState terminalState, List<(Move move, Player player)> moves) Simulate(RaveMctsNode node)
    {
        var state = node.State.Clone();
        var moves = new List<(Move, Player)>();

        while (state.Result == null)
        {
            var legal = GameRules.GetLegalMoves(state).ToList();
            if (legal.Count == 0) break;

            var mover = state.CurrentPlayer;
            var move = legal[random.Next(legal.Count)];
            moves.Add((move, mover));
            state = state.MakeMove(move);
        }

        return (state, moves);
    }

    private void BackpropagateRave(
        List<RaveMctsNode> path,
        List<(Move move, Player player)> trajectory,
        GameState terminalState)
    {
        // path[i] is the node entered after applying trajectory[0..i-1].
        // For RAVE at path[i], the AMAF set is moves played FROM path[i] onwards in this
        // iteration — i.e. trajectory[i..end]. Using moves played before reaching path[i]
        // would credit raveStats with actions that happened on a different position.
        for (int i = 0; i < path.Count; i++)
        {
            var n = path[i];
            n.Visits++;

            var moverIntoNode = n.Parent?.State.CurrentPlayer ?? n.State.CurrentPlayer;
            n.Wins += RewardFor(terminalState, moverIntoNode);

            var sideToMove = n.State.CurrentPlayer;
            double rewardForSideToMove = RewardFor(terminalState, sideToMove);
            n.UpdateRaveFromTrajectory(trajectory, i, sideToMove, rewardForSideToMove);
        }
    }

    private static double RewardFor(GameState state, Player perspective)
    {
        if (state.Result == null) return 0.5;
        if (state.Result.Type == ResultType.Draw) return 0.5;
        return state.Result.Winner == perspective ? 1.0 : 0.0;
    }

    public Move GetBestMove()
    {
        root.InitializeChildren();
        var bestChild = root.SelectMostVisitedChild();

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

    public bool IsFullyExpanded => unvisitedChildren != null && unvisitedChildren.Count == 0;
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
            double value = CalculateRaveMixedValue(child, move, explorationConstant);
            if (value > bestValue)
            {
                bestValue = value;
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

        double exploitation = node.Wins / node.Visits;
        double exploration = c * Math.Sqrt(Math.Log(Visits) / node.Visits);
        double uctValue = exploitation + exploration;

        if (raveStats != null && raveStats.TryGetValue(move, out var rave) && rave.RaveVisits > 0)
        {
            double raveValue = rave.GetRaveValue();
            int raveVisits = rave.RaveVisits;
            // Simple beta schedule (Gelly & Silver 2007, basic form):
            // beta = m / (n + m + eps). RAVE dominates while m >> n (few real visits, many AMAF),
            // and fades to 0 as real visits accumulate. No "Silver bias" term — that variant tends
            // to over-suppress RAVE for moderate iteration budgets.
            double beta = raveVisits / (double)(node.Visits + raveVisits + 1e-6);
            return beta * raveValue + (1 - beta) * uctValue;
        }

        return uctValue;
    }

    /// <summary>Update RAVE/AMAF statistics. Only the slice trajectory[startIndex..] is considered —
    /// i.e. moves played AT or AFTER this node in the current iteration. For each such (move, player)
    /// where player == sideToMove and the move is one of our tracked children, record the reward
    /// as if that move had been the first move from this position.</summary>
    public void UpdateRaveFromTrajectory(
        List<(Move move, Player player)> trajectory,
        int startIndex,
        Player sideToMove,
        double reward)
    {
        if (raveStats == null) return;

        var seen = new HashSet<Move>();
        for (int i = startIndex; i < trajectory.Count; i++)
        {
            var (m, p) = trajectory[i];
            if (p != sideToMove) continue;
            if (!raveStats.TryGetValue(m, out var stat)) continue;
            // Each move counts at most once per simulation (avoid double-counting if the
            // same move appears more than once in the trajectory).
            if (!seen.Add(m)) continue;
            stat.UpdateRave(reward);
        }
    }

    public IEnumerable<RaveMctsNode> GetChildren() => children?.Values ?? Enumerable.Empty<RaveMctsNode>();
}
