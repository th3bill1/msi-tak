namespace Tak.Core;

/// <summary>Game rules and validation</summary>
public static class GameRules
{
    private static readonly Direction[] OrthogonalDirections =
        [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    /// <summary>Get all legal moves for the current state</summary>
    /// <param name="state">The state to generate moves for.</param>
    /// <returns>All legal moves for the current player.</returns>
    public static IEnumerable<Move> GetLegalMoves(GameState state)
    {
        if (state.Result != null)
            yield break;

        var player = state.CurrentPlayer;

        // First, add all legal placement moves
        foreach (var move in GetLegalPlacementMoves(state))
        {
            yield return move;
        }

        // Then add all legal slide moves
        foreach (var move in GetLegalSlideMoves(state))
        {
            yield return move;
        }
    }

    private static IEnumerable<Move> GetLegalPlacementMoves(GameState state)
    {
        var player = state.CurrentPlayer;
        var config = state.Config;

        // Collect all empty squares
        var emptySquares = new List<Position>();
        for (int r = 0; r < config.BoardSize; r++)
        {
            for (int c = 0; c < config.BoardSize; c++)
            {
                var pos = new Position(r, c);
                if (state.Board.IsEmpty(pos))
                {
                    emptySquares.Add(pos);
                }
            }
        }

        // On opening move, player can only place opponent's flat stones
        if (state.IsOpening[player])
        {
            foreach (var pos in emptySquares)
            {
                yield return new PlaceMove(pos, PieceType.Flat);
            }
            yield break;
        }

        // Normal placement: can place flat, wall, or capstone (if available)
        foreach (var pos in emptySquares)
        {
            // Flat stone
            if (state.FlatStoneReserve[player] > 0)
            {
                yield return new PlaceMove(pos, PieceType.Flat);
            }

            // Wall (also counts against flat stone reserve)
            if (state.FlatStoneReserve[player] > 0)
            {
                yield return new PlaceMove(pos, PieceType.Wall);
            }

            // Capstone
            if (state.CapstonReserve[player] > 0)
            {
                yield return new PlaceMove(pos, PieceType.Capstone);
            }
        }
    }

    private static IEnumerable<Move> GetLegalSlideMoves(GameState state)
    {
        var player = state.CurrentPlayer;
        var config = state.Config;

        // For each non-empty square controlled by current player
        foreach (var (pos, stack) in state.Board.GetNonEmptySquares())
        {
            if (stack.Owner != player)
                continue;

            // Try moving 1 to N pieces (up to board size)
            int maxPieces = Math.Min(stack.Height, config.BoardSize);
            for (int numToMove = 1; numToMove <= maxPieces; numToMove++)
            {
                // Try each direction
                foreach (var dir in OrthogonalDirections)
                {
                    // Generate all valid drop distributions for this direction
                    foreach (var move in GenerateSlideMoves(state, pos, dir, numToMove))
                    {
                        yield return move;
                    }
                }
            }
        }
    }

    private static IEnumerable<SlideMove> GenerateSlideMoves(GameState state, Position from, Direction dir, int piecesToMove)
    {
        var config = state.Config;
        var board = state.Board;

        var currentPos = from;
        var validSquares = new List<Position>();

        // Find all valid squares we can move to in this direction
        for (int step = 0; step < config.BoardSize; step++)
        {
            var nextPos = currentPos.Offset(dir);
            if (!nextPos.IsValid(config.BoardSize))
                break;

            var targetStack = board.GetStack(nextPos);

            // Cannot move onto capstone
            if (!targetStack.IsEmpty && targetStack.TopPiece.IsCapstone)
                break;

            // Can move onto wall only as capstone on final drop
            if (!targetStack.IsEmpty && targetStack.TopPiece.IsWall)
            {
                if (board.GetStack(from).Height > 0 && board.GetStack(from).TopPiece.IsCapstone)
                {
                    validSquares.Add(nextPos);
                }
                break;
            }

            validSquares.Add(nextPos);
            currentPos = nextPos;
        }

        if (validSquares.Count == 0)
            yield break;

        // Generate all valid distributions up to the farthest reachable square.
        foreach (var distribution in GenerateDistributions(piecesToMove, validSquares.Count))
        {
            var targetPos = validSquares[distribution.Length - 1];
            yield return new SlideMove(from, targetPos, dir, distribution);
        }
    }

    /// <summary>Generate all valid drop distributions for N pieces across up to K squares.</summary>
    private static IEnumerable<int[]> GenerateDistributions(int pieces, int maxSquares)
    {
        if (maxSquares < 1 || pieces < 1)
            yield break;

        var maxDrops = Math.Min(pieces, maxSquares);
        for (int dropSquares = 1; dropSquares <= maxDrops; dropSquares++)
        {
            foreach (var dist in GenerateAllDistributions(pieces, dropSquares))
            {
                yield return dist;
            }
        }
    }

    private static IEnumerable<int[]> GenerateAllDistributions(int pieces, int squares)
    {
        if (squares == 1)
        {
            yield return new[] { pieces };
            yield break;
        }

        for (int first = 1; first <= pieces - (squares - 1); first++)
        {
            foreach (var rest in GenerateAllDistributions(pieces - first, squares - 1))
            {
                var result = new int[squares];
                result[0] = first;
                Array.Copy(rest, 0, result, 1, rest.Length);
                yield return result;
            }
        }
    }
}
