using Xunit;
using Tak.Core;
using Tak.AI;

namespace Tak.Tests;

public class IntegrationTests
{
    [Fact]
    public void FullGame_RandomVsRandom()
    {
        var game = Utils.CreateNewGame(4);
        var agentA = new RandomAgent(seed: 42);
        var agentB = new RandomAgent(seed: 43);
        
        int moveCount = 0;
        while (game.Result == null && moveCount < 100)
        {
            var agent = (IAgent)(game.CurrentPlayer == Player.White ? agentA : agentB);
            var move = agent.ChooseMove(game);
            Assert.NotNull(move);
            game = game.MakeMove(move);
            moveCount++;
        }
        
        // Game should end
        Assert.NotNull(game.Result);
        Assert.True(moveCount < 100, "Game should end within 100 moves");
    }

    [Fact]
    public void FullGame_RandomVsHeuristic()
    {
        var game = Utils.CreateNewGame(4);
        var agentA = new RandomAgent(seed: 42);
        var agentB = new HeuristicAgent(seed: 43);
        
        int moveCount = 0;
        while (game.Result == null && moveCount < 100)
        {
            var agent = (IAgent)(game.CurrentPlayer == Player.White ? agentA : agentB);
            var move = agent.ChooseMove(game);
            Assert.NotNull(move);
            game = game.MakeMove(move);
            moveCount++;
        }
        
        // Game should end
        Assert.NotNull(game.Result);
    }

    [Fact]
    public void FullGame_UctVsHeuristic()
    {
        var game = Utils.CreateNewGame(4);
        var agentA = new UctAgent(seed: 42);
        var agentB = new HeuristicAgent(seed: 43);
        
        int moveCount = 0;
        while (game.Result == null && moveCount < 100)
        {
            var agent = (IAgent)(game.CurrentPlayer == Player.White ? agentA : agentB);
            var move = agent.ChooseMove(game, iterationLimit: 200);
            Assert.NotNull(move);
            game = game.MakeMove(move);
            moveCount++;
        }
        
        // Game should end
        Assert.NotNull(game.Result);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void FullGame_MultipleBoardSizes(int boardSize)
    {
        var game = Utils.CreateNewGame(boardSize);
        var agentA = new RandomAgent(seed: 42);
        var agentB = new RandomAgent(seed: 43);
        
        int moveCount = 0;
        while (game.Result == null && moveCount < 200)
        {
            var agent = game.CurrentPlayer == Player.White ? agentA : agentB;
            var move = agent.ChooseMove(game);
            game = game.MakeMove(move);
            moveCount++;
        }
        
        // Game should complete
        Assert.NotNull(game.Result);
    }

    [Fact]
    public void GameState_ImmutableAfterMakeMove()
    {
        var game1 = Utils.CreateNewGame(4);
        var moves = GameRules.GetLegalMoves(game1).ToList();
        var move = moves[0];
        
        var game2 = game1.MakeMove(move);
        
        // game1 should be unchanged
        Assert.Equal(0, game1.MoveHistory.Count);
        Assert.Equal(1, game2.MoveHistory.Count);
    }

    [Fact]
    public void GameCloning_CreatesSeparateStates()
    {
        var game1 = Utils.CreateNewGame(4);
        var game2 = game1.Clone();
        
        var moves = GameRules.GetLegalMoves(game1).ToList();
        game1 = game1.MakeMove(moves[0]);
        
        // game2 should be unchanged
        Assert.Equal(0, game2.MoveHistory.Count);
        Assert.Equal(1, game1.MoveHistory.Count);
    }

    [Fact]
    public void MoveHistory_TracksMoves()
    {
        var game = Utils.CreateNewGame(4);
        var moves = GameRules.GetLegalMoves(game).ToList();
        
        for (int i = 0; i < Math.Min(3, moves.Count); i++)
        {
            game = game.MakeMove(moves[i]);
            Assert.Equal(i + 1, game.MoveHistory.Count);
        }
    }

    [Fact]
    public void ReserveDepletion_CorrectlyTracked()
    {
        var game = Utils.CreateNewGame(4);
        var initialFlats = game.FlatStoneReserve[Player.White];
        
        // Make opening move
        var moves = GameRules.GetLegalMoves(game).ToList();
        game = game.MakeMove(moves[0]);
        
        // After one move by each player, flats should remain same (opening rule)
        Assert.Equal(initialFlats, game.FlatStoneReserve[Player.White]);
    }

    [Fact]
    public void GameResult_Populated()
    {
        var game = Utils.CreateNewGame(4);
        var agentA = new RandomAgent(seed: 42);
        var agentB = new RandomAgent(seed: 43);
        
        int moveCount = 0;
        while (game.Result == null && moveCount < 100)
        {
            var agent = game.CurrentPlayer == Player.White ? agentA : agentB;
            var move = agent.ChooseMove(game);
            game = game.MakeMove(move);
            moveCount++;
        }
        
        // Game should have a result
        Assert.NotNull(game.Result);
        Assert.NotEqual(Player.None, game.Result.Winner);
        Assert.NotEqual(ResultType.Ongoing, game.Result.Type);
    }

    [Fact]
    public void AllAgents_CompleteGame()
    {
        var agents = new IAgent[]
        {
            new RandomAgent(seed: 42),
            new HeuristicAgent(seed: 42),
            new UctAgent(seed: 42),
            new RaveAgent(seed: 42),
            new ProgressiveWideningAgent(seed: 42)
        };
        
        foreach (var agent in agents)
        {
            var game = Utils.CreateNewGame(4);
            int moveCount = 0;
            
            while (game.Result == null && moveCount < 100)
            {
                var move = game.CurrentPlayer == Player.White 
                    ? agent.ChooseMove(game, iterationLimit: 100)
                    : new RandomAgent(42).ChooseMove(game);
                game = game.MakeMove(move);
                moveCount++;
            }
            
            Assert.NotNull(game.Result);
            Assert.True(game.Result != null, $"Game with {agent.Name} should end");
        }
    }
}
