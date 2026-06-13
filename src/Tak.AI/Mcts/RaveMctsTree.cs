namespace Tak.AI.Mcts;

using Tak.Core;

/// <summary>MCTS tree with RAVE (Rapid Action Value Estimation) support</summary>
public class RaveMctsTree
{
    private readonly RaveMctsNode root;
    private readonly Random random;
    private readonly double explorationConstant;

    /// <summary>Creates a RAVE MCTS tree rooted at the supplied initial state.</summary>
    /// <param name="initialState">The state to search from.</param>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <param name="seed">The optional random seed.</param>
    public RaveMctsTree(GameState initialState, double explorationConstant, int? seed = null)
    {
        root = new RaveMctsNode(initialState.Clone());
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.explorationConstant = explorationConstant;
    }

    /// <summary>Runs one RAVE MCTS iteration.</summary>
    /// <param name="maxRolloutMoves">The maximum number of moves to simulate in the rollout.</param>
    public void RunIteration(int maxRolloutMoves = 512)
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
        var (terminalState, rolloutMoves) = Simulate(node, maxRolloutMoves);
        trajectory.AddRange(rolloutMoves);

        // Backpropagation with RAVE: walk the path, updating each node's Wins/Visits
        // from the perspective of the mover INTO that node, and updating raveStats for
        // every trajectory move that matches the side-to-move at each node.
        BackpropagateRave(path, trajectory, terminalState);
    }

    /// <summary>Random playout returning the terminal state and the list of moves played with the player who made each move.</summary>
    private (GameState terminalState, List<(Move move, Player player)> moves) Simulate(RaveMctsNode node, int maxRolloutMoves)
    {
        var state = node.State.Clone();
        var moves = new List<(Move, Player)>();
        int depth = 0;

        while (state.Result == null && depth < maxRolloutMoves)
        {
            var legal = GameRules.GetLegalMoves(state).ToList();
            if (legal.Count == 0) break;

            var mover = state.CurrentPlayer;
            var move = legal[random.Next(legal.Count)];
            moves.Add((move, mover));
            state = state.MakeMove(move);
            depth++;
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

    /// <summary>Chooses the best root move by visit count.</summary>
    /// <returns>The move belonging to the most visited root child.</returns>
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

    /// <summary>Gets root search statistics.</summary>
    /// <returns>The root visit count and root win rate.</returns>
    public (int visits, double winRate) GetRootStats()
    {
        return (root.Visits, root.Wins / Math.Max(1, root.Visits));
    }
}

/// <summary>MCTS node with RAVE support</summary>
public class RaveMctsNode
{
    /// <summary>Gets or sets the parent node, or <see langword="null" /> for the root.</summary>
    public RaveMctsNode? Parent { get; set; }

    /// <summary>Gets or sets the move that led from the parent to this node.</summary>
    public Move? Move { get; set; }

    /// <summary>Gets the game state represented by this node.</summary>
    public GameState State { get; }

    /// <summary>Gets or sets the number of visits to this node.</summary>
    public int Visits { get; set; } = 0;

    /// <summary>Gets or sets the accumulated reward for this node.</summary>
    public double Wins { get; set; } = 0;

    private Dictionary<Move, RaveMctsNode>? children;
    private List<Move>? unvisitedChildren;
    private Dictionary<Move, RaveData>? raveStats;

    /// <summary>Creates a RAVE node for the supplied game state.</summary>
    /// <param name="state">The game state represented by the node.</param>
    public RaveMctsNode(GameState state)
    {
        State = state;
    }

    /// <summary>Gets whether all legal child moves have been expanded.</summary>
    public bool IsFullyExpanded => unvisitedChildren != null && unvisitedChildren.Count == 0;

    /// <summary>Gets whether the node represents a terminal game state.</summary>
    public bool IsTerminal => State.Result != null;

    /// <summary>Initializes the node's unvisited child move list and RAVE statistics.</summary>
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

    /// <summary>Selects and expands one unvisited child.</summary>
    /// <param name="random">The random source used to choose the move.</param>
    /// <returns>The newly expanded child node, or <see langword="null" /> if none are available.</returns>
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

    /// <summary>Selects the expanded child with the best UCT/RAVE mixed value.</summary>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <returns>The best child node, or <see langword="null" /> if there are no children.</returns>
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

    /// <summary>Selects the expanded child with the most visits.</summary>
    /// <returns>The most visited child, or <see langword="null" /> if there are no children.</returns>
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
        double exploration = c * Math.Sqrt(Math.Log(Math.Max(1, Visits)) / node.Visits);
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
    /// <param name="trajectory">The moves played during the selection, expansion, and rollout phases.</param>
    /// <param name="startIndex">The first trajectory index that belongs to this node's AMAF set.</param>
    /// <param name="sideToMove">The player to match when updating RAVE statistics.</param>
    /// <param name="reward">The reward to record for matching trajectory moves.</param>
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

    /// <summary>Gets the expanded children of this node.</summary>
    /// <returns>The expanded child nodes.</returns>
    public IEnumerable<RaveMctsNode> GetChildren() => children?.Values ?? Enumerable.Empty<RaveMctsNode>();
}
