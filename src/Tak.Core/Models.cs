namespace Tak.Core;

/// <summary>Board position</summary>
public readonly struct Position : IEquatable<Position>
{
    /// <summary>Row (0-based)</summary>
    public int Row { get; }
    
    /// <summary>Column (0-based)</summary>
    public int Col { get; }

    /// <summary>Create a new position</summary>
    /// <summary>Creates a position from zero-based row and column coordinates.</summary>
    public Position(int row, int col)
    {
        Row = row;
        Col = col;
    }

    public override bool Equals(object? obj) => obj is Position pos && Equals(pos);
    public bool Equals(Position other) => Row == other.Row && Col == other.Col;
    public override int GetHashCode() => HashCode.Combine(Row, Col);
    public override string ToString() => $"({Row}, {Col})";
    
    public static bool operator ==(Position a, Position b) => a.Equals(b);
    public static bool operator !=(Position a, Position b) => !a.Equals(b);

    /// <summary>Get position offset by direction</summary>
    public Position Offset(Direction direction) =>
        new(Row + direction.RowOffset(), Col + direction.ColOffset());

    /// <summary>Check if position is valid for given board size</summary>
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

    /// <summary>Create a new piece</summary>
    /// <summary>Creates a piece with the specified owner and type.</summary>
    public Piece(Player owner, PieceType type)
    {
        Owner = owner;
        Type = type;
    }

    public override bool Equals(object? obj) => obj is Piece p && Equals(p);
    public bool Equals(Piece other) => Owner == other.Owner && Type == other.Type;
    public override int GetHashCode() => HashCode.Combine(Owner, Type);
    public override string ToString() => $"{Owner}-{Type}";
    
    public static bool operator ==(Piece a, Piece b) => a.Equals(b);
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

/// <summary>A placement move (place a new piece on the board)</summary>
public record PlaceMove(Position Position, PieceType PieceType) : Move;

/// <summary>A slide move (move existing pieces from one square to another)</summary>
public record SlideMove(Position From, Position To, Direction Direction, int[] Distribution) : Move
{
    /// <summary>The position this move starts from</summary>
    public Position Position => From;

    /// <summary>Get the number of pieces moved</summary>
    public int PiecesCarried => Distribution.Sum();

    public virtual bool Equals(SlideMove? other)
    {
        return other is not null
            && From == other.From
            && To == other.To
            && Direction == other.Direction
            && Distribution.SequenceEqual(other.Distribution);
    }

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

/// <summary>Game result</summary>
public record GameResult(
    ResultType Type,
    Player Winner,
    int MoveCount,
    List<Move> Moves
)
{
    /// <summary>Get result as string</summary>
    /// <summary>Returns a compact description of the game result.</summary>
    public override string ToString() => Type switch
    {
        ResultType.Road => $"Road win: {Winner}",
        ResultType.Flat => $"Flat win: {Winner}",
        ResultType.Draw => "Draw",
        _ => "Game ongoing"
    };
}
