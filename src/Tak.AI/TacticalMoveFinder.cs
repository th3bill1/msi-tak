namespace Tak.AI;

using Tak.Core;

internal static class TacticalMoveFinder
{
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
