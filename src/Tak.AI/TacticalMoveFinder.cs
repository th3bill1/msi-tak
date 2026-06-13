namespace Tak.AI;

using Tak.Core;

internal static class TacticalMoveFinder
{
    /// <summary>Finds a legal move that wins immediately for the current player.</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="legalMoves">The legal moves to inspect.</param>
    /// <returns>An immediate winning move, or <see langword="null" /> if none exists.</returns>
    public static Move? FindImmediateWinningMove(GameState state, IReadOnlyList<Move> legalMoves)
    {
        foreach (var move in legalMoves)
        {
            var next = state.MakeMove(move);
            if (next.Result?.Winner == state.CurrentPlayer)
                return move;
        }

        return null;
    }

    /// <summary>Finds a safe legal move when the opponent has an immediate winning reply.</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="legalMoves">The legal moves to inspect.</param>
    /// <returns>A blocking or safe move, or <see langword="null" /> if no immediate opponent win was found.</returns>
    public static Move? FindImmediateOpponentWinBlock(GameState state, IReadOnlyList<Move> legalMoves)
    {
        Move? safeMove = null;
        bool foundUnsafeMove = false;

        foreach (var move in legalMoves)
        {
            var next = state.MakeMove(move);
            var opponentHasImmediateWin = GameRules.GetLegalMoves(next)
                .Any(opponentMove => next.MakeMove(opponentMove).Result?.Winner == state.CurrentPlayer.Opponent());

            if (opponentHasImmediateWin)
                foundUnsafeMove = true;
            else
                safeMove ??= move;
        }

        return foundUnsafeMove ? safeMove : null;
    }
}
