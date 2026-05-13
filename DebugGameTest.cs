using Tak.Core;

class Program
{
    static void Main()
    {
        // Simple test to debug game ending after first move
        var game = Utils.CreateNewGame(4);
        Console.WriteLine($"Initial state: Result = {game.Result}, CurrentPlayer = {game.CurrentPlayer}");
        Console.WriteLine($"Legal moves at start: {GameRules.GetLegalMoves(game).Count()}");
        
        // Check reserves
        Console.WriteLine($"White flat stones: {game.FlatStoneReserve[Player.White]}");
        Console.WriteLine($"Black flat stones: {game.FlatStoneReserve[Player.Black]}");
        
        // Make first move
        var firstMoves = GameRules.GetLegalMoves(game).ToList();
        Console.WriteLine($"First player ({game.CurrentPlayer}) can make {firstMoves.Count} moves");
        
        var firstMove = firstMoves.First();
        Console.WriteLine($"Making first move: {Utils.FormatMove(firstMove)}");
        game = game.MakeMove(firstMove);
        
        Console.WriteLine($"After first move: Result = {game.Result}, CurrentPlayer = {game.CurrentPlayer}");
        Console.WriteLine($"Board has {game.Board.GetNonEmptySquares().Count()} pieces");
        
        // Check reserves again
        Console.WriteLine($"White flat stones after move: {game.FlatStoneReserve[Player.White]}");
        Console.WriteLine($"Black flat stones after move: {game.FlatStoneReserve[Player.Black]}");
        
        var secondMoves = GameRules.GetLegalMoves(game).ToList();
        Console.WriteLine($"Second player ({game.CurrentPlayer}) can make {secondMoves.Count} moves");
        
        if (game.Result != null)
        {
            Console.WriteLine($"GAME ENDED! Winner: {game.Result.Winner}, Type: {game.Result.Type}");
        }
        else
        {
            Console.WriteLine("Game continues normally");
        }
    }
}
