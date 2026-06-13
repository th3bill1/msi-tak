namespace Tak.Core;

/// <summary>Game configuration (board size, piece counts)</summary>
public class GameConfig
{
    /// <summary>Board size (4, 5, or 6)</summary>
    public int BoardSize { get; }

    /// <summary>Initial flat/wall stone count per player</summary>
    public int FlatStoneCount { get; }

    /// <summary>Initial capstone count per player (0 or 1)</summary>
    public int CapstonCount { get; }

    /// <summary>Creates a game configuration for the specified board size.</summary>
    /// <param name="boardSize">The board size to configure.</param>
    public GameConfig(int boardSize)
    {
        BoardSize = boardSize switch
        {
            4 => 4,
            5 => 5,
            6 => 6,
            _ => throw new ArgumentException($"Unsupported board size: {boardSize}")
        };

        (FlatStoneCount, CapstonCount) = boardSize switch
        {
            4 => (15, 0),
            5 => (21, 1),
            6 => (30, 1),
            _ => throw new ArgumentException($"Unsupported board size: {boardSize}")
        };
    }

    /// <summary>Returns a compact description of the configuration.</summary>
    public override string ToString() => $"{BoardSize}x{BoardSize} (flat: {FlatStoneCount}, cap: {CapstonCount})";
}
