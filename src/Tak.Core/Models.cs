namespace Tak.Core;

/// <summary>Represents a zero-based board position.</summary>
public readonly struct Position : IEquatable<Position>
{
    /// <summary>Row (0-based)</summary>
    public int Row { get; }
    
    /// <summary>Column (0-based)</summary>
    public int Col { get; }

    /// <summary>Creates a position from zero-based row and column coordinates.</summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="col">The zero-based column.</param>
    public Position(int row, int col)
    {
        Row = row;
        Col = col;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Position pos && Equals(pos);

    /// <inheritdoc />
    public bool Equals(Position other) => Row == other.Row && Col == other.Col;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Row, Col);

    /// <inheritdoc />
    public override string ToString() => $"({Row}, {Col})";

    /// <summary>Determines whether two positions are equal.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <returns><see langword="true" /> if both positions have the same row and column.</returns>
    public static bool operator ==(Position a, Position b) => a.Equals(b);

    /// <summary>Determines whether two positions are different.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <returns><see langword="true" /> if the positions have different coordinates.</returns>
    public static bool operator !=(Position a, Position b) => !a.Equals(b);

    /// <summary>Get position offset by direction</summary>
    /// <param name="direction">The direction to move from this position.</param>
    /// <returns>The adjacent position in the requested direction.</returns>
    public Position Offset(Direction direction) =>
        new(Row + direction.RowOffset(), Col + direction.ColOffset());

    /// <summary>Check if position is valid for given board size</summary>
    /// <param name="boardSize">The number of rows and columns on the board.</param>
    /// <returns><see langword="true" /> if the position lies inside the board.</returns>
    public bool IsValid(int boardSize) =>
        Row >= 0 && Row < boardSize && Col >= 0 && Col < boardSize;
}

/// <summary>A piece on the board</summary>
public readonly struct Piece : IEquatable<Piece>
{
    /// <summary>Owner of the piece</summary>
    public Player Owner { get; }
    
    /// <summary>Type of piece</summary>
    public PieceType Type { get; }

    /// <summary>Creates a piece with the specified owner and type.</summary>
    /// <param name="owner">The player that owns the piece.</param>
    /// <param name="type">The type of piece.</param>
    public Piece(Player owner, PieceType type)
    {
        Owner = owner;
        Type = type;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Piece p && Equals(p);

    /// <inheritdoc />
    public bool Equals(Piece other) => Owner == other.Owner && Type == other.Type;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Owner, Type);

    /// <inheritdoc />
    public override string ToString() => $"{Owner}-{Type}";

    /// <summary>Determines whether two pieces are equal.</summary>
    /// <param name="a">The first piece.</param>
    /// <param name="b">The second piece.</param>
    /// <returns><see langword="true" /> if both pieces have the same owner and type.</returns>
    public static bool operator ==(Piece a, Piece b) => a.Equals(b);

    /// <summary>Determines whether two pieces are different.</summary>
    /// <param name="a">The first piece.</param>
    /// <param name="b">The second piece.</param>
    /// <returns><see langword="true" /> if the pieces have different owners or types.</returns>
    public static bool operator !=(Piece a, Piece b) => !a.Equals(b);

    /// <summary>Check if this piece is a flat stone (counts toward road and flat victory)</summary>
    public bool IsFlat => Type == PieceType.Flat;

    /// <summary>Check if this piece is a wall</summary>
    public bool IsWall => Type == PieceType.Wall;

    /// <summary>Check if this piece is a capstone</summary>
    public bool IsCapstone => Type == PieceType.Capstone;
}

/// <summary>Base class for moves</summary>
public abstract record Move;

/// <summary>A placement move that places a new piece on an empty board square.</summary>
/// <param name="Position">The target board position.</param>
/// <param name="PieceType">The type of piece to place.</param>
public record PlaceMove(Position Position, PieceType PieceType) : Move;

/// <summary>A slide move that moves pieces from one square through adjacent squares.</summary>
/// <param name="From">The source position.</param>
/// <param name="To">The final target position.</param>
/// <param name="Direction">The direction of travel.</param>
/// <param name="Distribution">The number of pieces dropped on each traversed square.</param>
public record SlideMove(Position From, Position To, Direction Direction, int[] Distribution) : Move
{
    /// <summary>The position this move starts from</summary>
    public Position Position => From;

    /// <summary>Get the number of pieces moved</summary>
    public int PiecesCarried => Distribution.Sum();

    /// <inheritdoc />
    public virtual bool Equals(SlideMove? other)
    {
        return other is not null
            && From == other.From
            && To == other.To
            && Direction == other.Direction
            && Distribution.SequenceEqual(other.Distribution);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(From);
        hash.Add(To);
        hash.Add(Direction);
        foreach (var drop in Distribution)
        {
            hash.Add(drop);
        }
        return hash.ToHashCode();
    }

    /// <summary>Validate that the drop distribution is valid for a given board size</summary>
    /// <param name="boardSize">The size of the board being played.</param>
    /// <returns><see langword="true" /> if the distribution can be used on the board.</returns>
    public bool IsValidDistribution(int boardSize)
    {
        // Must have at least one piece per dropped square
        if (Distribution.Length < 1)
            return false;

        // Must not exceed board size or carried pieces
        if (Distribution.Length > boardSize || Distribution.Length > PiecesCarried)
            return false;

        // Each distribution must be at least 1
        if (Distribution.Any(d => d < 1))
            return false;

        return true;
    }
}

/// <summary>Describes the final result of a game.</summary>
/// <param name="Type">The type of result.</param>
/// <param name="Winner">The winning player, or <see cref="Player.None" /> for a draw or ongoing game.</param>
/// <param name="MoveCount">The number of moves played.</param>
/// <param name="Moves">The moves played in the game.</param>
public record GameResult(
    ResultType Type,
    Player Winner,
    int MoveCount,
    List<Move> Moves
)
{
    /// <inheritdoc />
    public override string ToString() => Type switch
    {
        ResultType.Road => $"Road win: {Winner}",
        ResultType.Flat => $"Flat win: {Winner}",
        ResultType.Draw => "Draw",
        _ => "Game ongoing"
    };
}
