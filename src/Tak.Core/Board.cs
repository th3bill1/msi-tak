namespace Tak.Core;

/// <summary>Represents a single square on the board (a stack of pieces)</summary>
public class Stack
{
    /// <summary>Pieces in this stack (bottom to top)</summary>
    private List<Piece> pieces;

    public Stack()
    {
        pieces = new List<Piece>();
    }

    /// <summary>Get the top piece, or empty if stack is empty</summary>
    public Piece TopPiece => pieces.Count > 0 ? pieces[^1] : new Piece(Player.None, PieceType.Flat);

    /// <summary>Get the owner of this stack (owner of top piece)</summary>
    public Player Owner => TopPiece.Owner;

    /// <summary>Get the height of this stack</summary>
    public int Height => pieces.Count;

    /// <summary>Get the number of flat stones in this stack</summary>
    public int FlatCount => pieces.Count(p => p.IsFlat);

    /// <summary>Check if stack is empty</summary>
    public bool IsEmpty => pieces.Count == 0;

    /// <summary>Add a piece to the top of the stack</summary>
    public void Push(Piece piece) => pieces.Add(piece);

    /// <summary>Remove and return the top piece</summary>
    public Piece Pop() => pieces.Count > 0 ? pieces.RemoveAtEnd() : throw new InvalidOperationException("Cannot pop from empty stack");

    /// <summary>Get a copy of all pieces</summary>
    public IReadOnlyList<Piece> GetPieces() => pieces.AsReadOnly();

    /// <summary>Create a deep copy of this stack</summary>
    public Stack Clone()
    {
        var clone = new Stack();
        foreach (var piece in pieces)
        {
            clone.pieces.Add(piece);
        }
        return clone;
    }

    public override string ToString()
    {
        if (IsEmpty) return "empty";
        return $"{TopPiece} x{Height}";
    }
}

/// <summary>Represents the game board</summary>
public class Board
{
    private Stack[] squares;
    public int Size { get; }

    public Board(int size)
    {
        Size = size;
        squares = new Stack[Size * Size];
        for (int i = 0; i < squares.Length; i++)
        {
            squares[i] = new Stack();
        }
    }

    /// <summary>Get the stack at a position</summary>
    public Stack GetStack(Position pos)
    {
        ValidatePosition(pos);
        return squares[pos.Row * Size + pos.Col];
    }

    /// <summary>Check if a position is empty</summary>
    public bool IsEmpty(Position pos) => GetStack(pos).IsEmpty;

    /// <summary>Place a piece on a square (must be empty)</summary>
    public void PlacePiece(Position pos, Piece piece)
    {
        if (!IsEmpty(pos))
            throw new InvalidOperationException($"Cannot place on occupied square {pos}");
        GetStack(pos).Push(piece);
    }

    /// <summary>Get all non-empty squares</summary>
    public IEnumerable<(Position pos, Stack stack)> GetNonEmptySquares()
    {
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var pos = new Position(r, c);
                var stack = GetStack(pos);
                if (!stack.IsEmpty)
                    yield return (pos, stack);
            }
        }
    }

    /// <summary>Create a deep copy of the board</summary>
    public Board Clone()
    {
        var clone = new Board(Size);
        for (int i = 0; i < squares.Length; i++)
        {
            clone.squares[i] = squares[i].Clone();
        }
        return clone;
    }

    private void ValidatePosition(Position pos)
    {
        if (!pos.IsValid(Size))
            throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of bounds for {Size}x{Size} board");
    }
}

/// <summary>Extension helper for removing the last element from a list</summary>
internal static class ListExtensions
{
    public static T RemoveAtEnd<T>(this List<T> list)
    {
        var index = list.Count - 1;
        var value = list[index];
        list.RemoveAt(index);
        return value;
    }
}
