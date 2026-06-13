namespace Tak.AI;

using System.Diagnostics;
using Tak.Core;
using Tak.AI.Mcts;

/// <summary>Diagnostics captured from the most recent RAVE search.</summary>
/// <param name="SelectedMove">The move selected by the search.</param>
/// <param name="LegalMoveCount">The number of legal moves available at the root.</param>
/// <param name="IterationsRun">The number of search iterations completed.</param>
/// <param name="Duration">The elapsed search duration.</param>
/// <param name="RootVisits">The number of visits recorded at the root node.</param>
/// <param name="RootWinRate">The root node win rate.</param>
/// <param name="SelectionReason">Additional context for fallback or tactical selections.</param>
public record RaveSearchDiagnostics(
    Move? SelectedMove,
    int LegalMoveCount,
    int IterationsRun,
    TimeSpan Duration,
    int RootVisits,
    double RootWinRate,
    string? SelectionReason
);

/// <summary>UCT agent with RAVE (Rapid Action Value Estimation)</summary>
public class RaveAgent : Agent
{
    private readonly double explorationConstant;
    private readonly Random random;

    /// <inheritdoc />
    public override string Name => "RAVE";

    /// <summary>Gets diagnostics from the most recent call to <see cref="ChooseMove" />.</summary>
    public RaveSearchDiagnostics? LastDiagnostics { get; private set; }

    /// <summary>Gets or sets whether the agent throws when search returns an illegal move.</summary>
    public bool ThrowOnInvalidMove { get; set; }

    /// <summary>Create a RAVE-enhanced MCTS agent with an optional deterministic seed.</summary>
    /// <param name="explorationConstant">The UCT exploration constant.</param>
    /// <param name="seed">The optional random seed.</param>
    public RaveAgent(double explorationConstant = 1.414, int? seed = null)
    {
        this.explorationConstant = explorationConstant;
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Choose a move using RAVE-augmented search within the provided limits.</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="timeLimit">The optional time limit.</param>
    /// <param name="iterationLimit">The optional iteration limit.</param>
    /// <returns>The selected move.</returns>
    public override Move ChooseMove(GameState state, TimeSpan? timeLimit = null, int? iterationLimit = null)
    {
        iterationLimit ??= 1000;
        var legalMoves = GameRules.GetLegalMoves(state).ToList();
        if (legalMoves.Count == 0)
            throw new InvalidOperationException("No legal moves available");

        var stopwatch = Stopwatch.StartNew();
        var deadline = timeLimit.HasValue ? stopwatch.Elapsed + timeLimit.Value : (TimeSpan?)null;

        var immediateWin = TacticalMoveFinder.FindImmediateWinningMove(state, legalMoves);
        if (immediateWin != null)
            return CompleteWithDiagnostics(immediateWin, legalMoves.Count, 0, stopwatch, 0, 0, "immediate-win");

        var immediateBlock = TacticalMoveFinder.FindImmediateOpponentWinBlock(state, legalMoves);
        if (immediateBlock != null)
            return CompleteWithDiagnostics(immediateBlock, legalMoves.Count, 0, stopwatch, 0, 0, "immediate-block");

        var tree = new RaveMctsTree(state, explorationConstant, random.Next());
        int iterationsRun = 0;
        int maxRolloutMoves = Math.Max(32, state.Config.BoardSize * state.Config.BoardSize * 2);

        while (iterationsRun < iterationLimit.Value && (deadline == null || stopwatch.Elapsed < deadline.Value))
        {
            tree.RunIteration(maxRolloutMoves);
            iterationsRun++;
        }

        var (rootVisits, rootWinRate) = tree.GetRootStats();
        Move selected;
        string? selectionReason = null;

        try
        {
            selected = tree.GetBestMove();
        }
        catch (InvalidOperationException)
        {
            selected = legalMoves[0];
            selectionReason = "search-returned-no-move";
        }

        if (!legalMoves.Contains(selected))
        {
            var message = $"RAVE selected an illegal move: {Utils.FormatMove(selected)}";
            if (ThrowOnInvalidMove)
                throw new InvalidOperationException(message);

            selected = legalMoves[0];
            selectionReason = message;
        }

        return CompleteWithDiagnostics(selected, legalMoves.Count, iterationsRun, stopwatch, rootVisits, rootWinRate, selectionReason);
    }

    private Move CompleteWithDiagnostics(
        Move selected,
        int legalMoveCount,
        int iterationsRun,
        Stopwatch stopwatch,
        int rootVisits,
        double rootWinRate,
        string? selectionReason)
    {
        stopwatch.Stop();
        LastDiagnostics = new RaveSearchDiagnostics(
            selected,
            legalMoveCount,
            iterationsRun,
            stopwatch.Elapsed,
            rootVisits,
            rootWinRate,
            selectionReason);
        return selected;
    }
}
