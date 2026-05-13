namespace Tak.Core;

/// <summary>Complete game state (immutable for MCTS simulations)</summary>
public class GameState
{
    /// <summary>Game configuration</summary>
    public GameConfig Config { get; }

    /// <summary>Current board state</summary>
    public Board Board { get; }

    /// <summary>Current player to move</summary>
    public Player CurrentPlayer { get; set; }

    /// <summary>Move history</summary>
    public List<Move> MoveHistory { get; }

    /// <summary>Remaining flat/wall stones for each player</summary>
    public Dictionary<Player, int> FlatStoneReserve { get; }

    /// <summary>Remaining capstones for each player</summary>
    public Dictionary<Player, int> CapstonReserve { get; }

    /// <summary>Game result (if game is over)</summary>
    public GameResult? Result { get; set; }

    /// <summary>Is opening rule active (first move by each player)?</summary>
    public Dictionary<Player, bool> IsOpening { get; }

    public GameState(GameConfig config)
    {
        Config = config;
        Board = new Board(config.BoardSize);
        CurrentPlayer = Player.White;
        MoveHistory = new List<Move>();
        FlatStoneReserve = new Dictionary<Player, int>
        {
            { Player.White, config.FlatStoneCount },
            { Player.Black, config.FlatStoneCount }
        };
        CapstonReserve = new Dictionary<Player, int>
        {
            { Player.White, config.CapstonCount },
            { Player.Black, config.CapstonCount }
        };
        IsOpening = new Dictionary<Player, bool>
        {
            { Player.White, true },
            { Player.Black, true }
        };
    }

    private GameState(GameState other)
    {
        Config = other.Config;
        Board = other.Board.Clone();
        CurrentPlayer = other.CurrentPlayer;
        MoveHistory = new List<Move>(other.MoveHistory);
        FlatStoneReserve = new Dictionary<Player, int>(other.FlatStoneReserve);
        CapstonReserve = new Dictionary<Player, int>(other.CapstonReserve);
        IsOpening = new Dictionary<Player, bool>(other.IsOpening);
        Result = other.Result;
    }

    /// <summary>Create a deep copy of the game state</summary>
    public GameState Clone() => new GameState(this);

    /// <summary>Make a move and return a new game state (this state is unchanged)</summary>
    public GameState MakeMove(Move move)
    {
        if (Result != null)
            throw new InvalidOperationException("Game is already over");

        var newState = Clone();
        newState.ApplyMoveInPlace(move);
        return newState;
    }

    /// <summary>Apply a move to this state (mutates state)</summary>
    private void ApplyMoveInPlace(Move move)
    {
        if (move is PlaceMove placeMove)
        {
            ApplyPlaceMove(placeMove);
        }
        else if (move is SlideMove slideMove)
        {
            ApplySlideMove(slideMove);
        }

        MoveHistory.Add(move);
        CheckGameEnd();
        CurrentPlayer = CurrentPlayer.Opponent();
    }

    private void ApplyPlaceMove(PlaceMove move)
    {
        var piece = new Piece(CurrentPlayer, move.PieceType);

        if (IsOpening[CurrentPlayer])
        {
            // On opening move, place opponent's piece
            piece = new Piece(CurrentPlayer.Opponent(), PieceType.Flat);
            IsOpening[CurrentPlayer] = false;
        }
        else
        {
            // Normal placement: deduct from reserve
            if (move.PieceType == PieceType.Capstone)
            {
                if (CapstonReserve[CurrentPlayer] <= 0)
                    throw new InvalidOperationException("No capstones left in reserve");
                CapstonReserve[CurrentPlayer]--;
            }
            else
            {
                if (FlatStoneReserve[CurrentPlayer] <= 0)
                    throw new InvalidOperationException("No flat stones left in reserve");
                FlatStoneReserve[CurrentPlayer]--;
            }
        }

        Board.PlacePiece(move.Position, piece);
    }

    private void ApplySlideMove(SlideMove move)
    {
        // Pick up pieces from source
        var stack = Board.GetStack(move.From);
        var piecesToMove = new List<Piece>();
        for (int i = 0; i < move.PiecesCarried; i++)
        {
            piecesToMove.Add(stack.Pop());
        }
        piecesToMove.Reverse(); // Now bottom to top

        // Slide and drop
        var currentPos = move.From;
        int dropIndex = 0;

        for (int step = 0; step < move.Distribution.Length; step++)
        {
            currentPos = currentPos.Offset(move.Direction);
            int dropCount = move.Distribution[step];

            for (int i = 0; i < dropCount; i++)
            {
                if (dropIndex >= piecesToMove.Count)
                    throw new InvalidOperationException("Drop distribution exceeds carried pieces");

                var piece = piecesToMove[dropIndex++];
                
                // Handle wall flattening on final drop
                if (dropIndex == piecesToMove.Count && i == dropCount - 1)
                {
                    var targetStack = Board.GetStack(currentPos);
                    if (!targetStack.IsEmpty && targetStack.TopPiece.IsWall && piece.IsCapstone)
                    {
                        // Flatten the wall
                        targetStack.Pop();
                        targetStack.Push(new Piece(piece.Owner, PieceType.Flat));
                        continue;
                    }
                }

                Board.GetStack(currentPos).Push(piece);
            }
        }
    }

    private void CheckGameEnd()
    {
        // Check for road win
        if (CheckRoadWin(CurrentPlayer))
        {
            Result = new GameResult(ResultType.Road, CurrentPlayer, MoveHistory.Count, MoveHistory);
            return;
        }

        // Check for road win by opponent (Tak-Tin rule: current player wins)
        if (CheckRoadWin(CurrentPlayer.Opponent()))
        {
            Result = new GameResult(ResultType.Road, CurrentPlayer, MoveHistory.Count, MoveHistory);
            return;
        }

        // Check for flat win
        if (IsBoardFull() || NoMovesAvailable())
        {
            var flatCounts = CountFlatStones();
            if (flatCounts[Player.White] > flatCounts[Player.Black])
            {
                Result = new GameResult(ResultType.Flat, Player.White, MoveHistory.Count, MoveHistory);
            }
            else if (flatCounts[Player.Black] > flatCounts[Player.White])
            {
                Result = new GameResult(ResultType.Flat, Player.Black, MoveHistory.Count, MoveHistory);
            }
            else
            {
                Result = new GameResult(ResultType.Draw, Player.None, MoveHistory.Count, MoveHistory);
            }
        }
    }

    /// <summary>Check if a player has formed a road (connected path)</summary>
    private bool CheckRoadWin(Player player)
    {
        if (player == Player.None) return false;

        // Get all flat pieces and capstones of this player
        var playerPieces = new HashSet<Position>();
        foreach (var (pos, stack) in Board.GetNonEmptySquares())
        {
            if (stack.Owner == player && (stack.TopPiece.IsFlat || stack.TopPiece.IsCapstone))
            {
                playerPieces.Add(pos);
            }
        }

        if (playerPieces.Count == 0)
            return false;

        // Check for horizontal road (left-right connection)
        for (int row = 0; row < Config.BoardSize; row++)
        {
            if (HasPathAcrossRow(row, playerPieces))
                return true;
        }

        // Check for vertical road (top-bottom connection)
        for (int col = 0; col < Config.BoardSize; col++)
        {
            if (HasPathDownColumn(col, playerPieces))
                return true;
        }

        return false;
    }

    private bool HasPathAcrossRow(int row, HashSet<Position> pieces)
    {
        // BFS from left edge to right edge
        var queue = new Queue<Position>();
        var visited = new HashSet<Position>();

        // Start only from left edge pieces (col == 0)
        var leftEdgePos = new Position(row, 0);
        if (pieces.Contains(leftEdgePos))
        {
            queue.Enqueue(leftEdgePos);
            visited.Add(leftEdgePos);
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();

            // Check if we reached right edge
            if (pos.Col == Config.BoardSize - 1)
                return true;

            // Explore neighbors
            foreach (var dir in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                var next = pos.Offset(dir);
                if (next.IsValid(Config.BoardSize) && pieces.Contains(next) && !visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    private bool HasPathDownColumn(int col, HashSet<Position> pieces)
    {
        // BFS from top edge to bottom edge
        var queue = new Queue<Position>();
        var visited = new HashSet<Position>();

        // Start only from top edge pieces (row == 0)
        var topEdgePos = new Position(0, col);
        if (pieces.Contains(topEdgePos))
        {
            queue.Enqueue(topEdgePos);
            visited.Add(topEdgePos);
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();

            // Check if we reached bottom edge
            if (pos.Row == Config.BoardSize - 1)
                return true;

            // Explore neighbors
            foreach (var dir in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                var next = pos.Offset(dir);
                if (next.IsValid(Config.BoardSize) && pieces.Contains(next) && !visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    private bool IsBoardFull()
    {
        for (int r = 0; r < Config.BoardSize; r++)
        {
            for (int c = 0; c < Config.BoardSize; c++)
            {
                if (Board.IsEmpty(new Position(r, c)))
                    return false;
            }
        }
        return true;
    }

    private bool NoMovesAvailable()
    {
        return !GameRules.GetLegalMoves(this).Any();
    }

    private Dictionary<Player, int> CountFlatStones()
    {
        var counts = new Dictionary<Player, int> { { Player.White, 0 }, { Player.Black, 0 } };

        foreach (var (_, stack) in Board.GetNonEmptySquares())
        {
            if (stack.IsEmpty) continue;
            if (stack.TopPiece.IsFlat)
            {
                counts[stack.Owner]++;
            }
        }

        return counts;
    }

    public override string ToString()
    {
        return $"GameState: {CurrentPlayer} to move, moves: {MoveHistory.Count}";
    }
}
