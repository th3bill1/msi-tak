namespace Tak.Core;

/// <summary>Player enumeration</summary>
public enum Player
{
    /// <summary>No player / empty square</summary>
    None = 0,
    
    /// <summary>White player</summary>
    White = 1,
    
    /// <summary>Black player</summary>
    Black = 2
}

/// <summary>Piece type enumeration</summary>
public enum PieceType
{
    /// <summary>Flat stone (counts toward road)</summary>
    Flat = 0,
    
    /// <summary>Wall / standing stone (blocks movement, doesn't count toward road)</summary>
    Wall = 1,
    
    /// <summary>Capstone (counts toward road, can flatten walls)</summary>
    Capstone = 2
}

/// <summary>Movement direction</summary>
public enum Direction
{
    /// <summary>Up</summary>
    Up = 0,
    
    /// <summary>Down</summary>
    Down = 1,
    
    /// <summary>Left</summary>
    Left = 2,
    
    /// <summary>Right</summary>
    Right = 3
}

/// <summary>Game result type</summary>
public enum ResultType
{
    /// <summary>No result yet</summary>
    Ongoing,
    
    /// <summary>Victory by forming a road</summary>
    Road,
    
    /// <summary>Victory by having more controlled flat stones</summary>
    Flat,
    
    /// <summary>Draw (equal flat count)</summary>
    Draw
}

/// <summary>Extension methods for Player</summary>
public static class PlayerExtensions
{
    /// <summary>Get the opponent of a player</summary>
    public static Player Opponent(this Player player) => player switch
    {
        Player.White => Player.Black,
        Player.Black => Player.White,
        _ => Player.None
    };
}

/// <summary>Extension methods for Direction</summary>
public static class DirectionExtensions
{
    /// <summary>Convert direction to row offset</summary>
    public static int RowOffset(this Direction direction) => direction switch
    {
        Direction.Up => -1,
        Direction.Down => 1,
        _ => 0
    };

    /// <summary>Convert direction to column offset</summary>
    public static int ColOffset(this Direction direction) => direction switch
    {
        Direction.Left => -1,
        Direction.Right => 1,
        _ => 0
    };
}
