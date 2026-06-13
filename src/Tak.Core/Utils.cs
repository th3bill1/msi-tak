namespace Tak.Core;

/// <summary>Utility functions</summary>
public static class Utils
{
    /// <summary>Create a new game with given configuration</summary>
    /// <param name="boardSize">The board size to create.</param>
    /// <returns>A new game state.</returns>
    public static GameState CreateNewGame(int boardSize) => new(new GameConfig(boardSize));

    /// <summary>Get a random legal move</summary>
    /// <param name="state">The state to choose a move from.</param>
    /// <param name="random">The random source to use.</param>
    /// <returns>A random legal move, or <see langword="null" /> if none exists.</returns>
    public static Move? GetRandomMove(GameState state, Random random)
    {
        var moves = GameRules.GetLegalMoves(state).ToList();
        if (moves.Count == 0)
            return null;
        return moves[random.Next(moves.Count)];
    }

    /// <summary>Format move for display</summary>
    /// <param name="move">The move to format.</param>
    /// <returns>A display string for the move.</returns>
    public static string FormatMove(Move move) => move switch
    {
        PlaceMove pm => $"Place {pm.PieceType} at {pm.Position}",
        SlideMove sm => $"Slide {sm.From} -> {sm.To} carrying {sm.PiecesCarried} [{string.Join(",", sm.Distribution)}]",
        _ => "Unknown move"
    };

    /// <summary>Play a game to completion with given agents</summary>
    /// <param name="whiteAgent">The agent that controls White.</param>
    /// <param name="blackAgent">The agent that controls Black.</param>
    /// <param name="initialState">The state to start from.</param>
    /// <param name="timeLimit">The optional time limit per move.</param>
    /// <param name="seedRandom">An optional random source reserved for seeded callers.</param>
    /// <returns>The final game result.</returns>
    public static GameResult PlayGame(IGameAgent whiteAgent, IGameAgent blackAgent, GameState initialState, TimeSpan? timeLimit = null, Random? seedRandom = null)
    {
        var state = initialState.Clone();
        
        while (state.Result == null)
        {
            var agent = state.CurrentPlayer == Player.White ? whiteAgent : blackAgent;
            var random = seedRandom;
            var move = agent.ChooseMove(state, timeLimit);
            
            if (move == null)
            {
                throw new InvalidOperationException($"Agent {agent.Name} returned null move");
            }

            state = state.MakeMove(move);
        }

        return state.Result;
    }
}

/// <summary>Interface for AI agents</summary>
public interface IGameAgent
{
    /// <summary>Agent name for logging/display</summary>
    string Name { get; }

    /// <summary>Choose a move for the given game state</summary>
    /// <param name="state">The current game state.</param>
    /// <param name="timeLimit">The optional time limit for choosing a move.</param>
    /// <returns>The selected move.</returns>
    Move ChooseMove(GameState state, TimeSpan? timeLimit = null);
}
