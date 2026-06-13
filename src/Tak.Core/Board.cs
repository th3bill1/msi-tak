namespace Tak.Core;

/// <summary>Represents a single square on the board (a stack of pieces)</summary>
public class Stack
{
    /// <summary>Pieces in this stack (bottom to top)</summary>
    private List<Piece> pieces;

    /// <summary>Creates an empty stack.</summary>
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
    /// <param name="piece">The piece to add.</param>
    public void Push(Piece piece) => pieces.Add(piece);

    /// <summary>Remove and return the top piece</summary>
    /// <returns>The piece that was on top of the stack.</returns>
    public Piece Pop() => pieces.Count > 0 ? pieces.RemoveAtEnd() : throw new InvalidOperationException("Cannot pop from empty stack");

    /// <summary>Get a copy of all pieces</summary>
    public IReadOnlyList<Piece> GetPieces() => pieces.AsReadOnly();

    /// <summary>Creates a deep copy of this stack.</summary>
    /// <returns>A new stack with the same pieces in the same order.</returns>
    public Stack Clone()
    {
        var clone = new Stack();
        foreach (var piece in pieces)
        {
            clone.pieces.Add(piece);
        }
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsEmpty) return "empty";
        return $"{TopPiece} x{Height}";
    }
}

/// <summary>Represents the game board.</summary>
public class Board
{
    private Stack[] squares;
    /// <summary>Gets the board size in squares per side.</summary>
    public int Size { get; }

    /// <summary>Creates a board with the specified size.</summary>
    /// <param name="size">The number of rows and columns on the board.</param>
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
    /// <param name="pos">The position to read.</param>
    /// <returns>The stack at the specified position.</returns>
    public Stack GetStack(Position pos)
    {
        ValidatePosition(pos);
        return squares[pos.Row * Size + pos.Col];
    }

    /// <summary>Check if a position is empty</summary>
    /// <param name="pos">The position to check.</param>
    /// <returns><see langword="true" /> if the position has no pieces.</returns>
    public bool IsEmpty(Position pos) => GetStack(pos).IsEmpty;

    /// <summary>Place a piece on a square (must be empty)</summary>
    /// <param name="pos">The position to place on.</param>
    /// <param name="piece">The piece to place.</param>
    public void PlacePiece(Position pos, Piece piece)
    {
        if (!IsEmpty(pos))
            throw new InvalidOperationException($"Cannot place on occupied square {pos}");
        GetStack(pos).Push(piece);
    }

    /// <summary>Get all non-empty squares</summary>
    /// <returns>The positions and stacks for every occupied square.</returns>
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

    /// <summary>Creates a deep copy of this board.</summary>
    /// <returns>A new board with cloned stacks.</returns>
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
    /// <summary>Removes and returns the last element from a list.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list to remove from.</param>
    /// <returns>The removed element.</returns>
    public static T RemoveAtEnd<T>(this List<T> list)
    {
        var index = list.Count - 1;
        var value = list[index];
        list.RemoveAt(index);
        return value;
    }
}
