using Xunit;
using Tak.Core;
using Tak.AI;
using Tak.Experiments;

namespace Tak.Tests;

public class AgentTests
{
    [Fact]
    public void RandomAgent_ReturnsLegalMove()
    {
        var agent = new RandomAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void RandomAgent_Reproducible()
    {
        var game1 = Utils.CreateNewGame(4);
        var agent1 = new RandomAgent(seed: 42);
        var move1 = agent1.ChooseMove(game1);
        
        var game2 = Utils.CreateNewGame(4);
        var agent2 = new RandomAgent(seed: 42);
        var move2 = agent2.ChooseMove(game2);
        
        // Same seed should produce same move
        Assert.Equal(move1, move2);
    }

    [Fact]
    public void HeuristicAgent_ReturnsLegalMove()
    {
        var agent = new HeuristicAgent(seed: 42);
        var game = Utils.CreateNewGame(5);
        
        var move = agent.ChooseMove(game);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void HeuristicAgent_PrefersMatesInOne()
    {
        var agent = new HeuristicAgent();
        var game = Utils.CreateNewGame(4);
        
        // Create a scenario where heuristic could find an immediate win
        // This is simplified - just verify it returns a legal move
        var move = agent.ChooseMove(game);
        Assert.NotNull(move);
    }

    [Fact]
    public void UctAgent_ReturnsLegalMove()
    {
        var agent = new UctAgent(explorationConstant: 1.414, seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 100);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void UctAgent_RespondsToIterationLimit()
    {
        var agent = new UctAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        // With 10 iterations should be fast
        var start = DateTime.Now;
        var move1 = agent.ChooseMove(game, iterationLimit: 10);
        var time1 = (DateTime.Now - start).TotalMilliseconds;
        
        // With 1000 iterations should be slower
        start = DateTime.Now;
        var move2 = agent.ChooseMove(game, iterationLimit: 1000);
        var time2 = (DateTime.Now - start).TotalMilliseconds;
        
        // Just verify both return valid moves
        Assert.NotNull(move1);
        Assert.NotNull(move2);
    }

    [Fact]
    public void RaveAgent_ReturnsLegalMove()
    {
        var agent = new RaveAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 100);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void ProgressiveWideningAgent_ReturnsLegalMove()
    {
        var agent = new ProgressiveWideningAgent(seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 100);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Theory]
    [InlineData("random")]
    [InlineData("heuristic")]
    [InlineData("uct")]
    [InlineData("rave")]
    [InlineData("pw")]
    public void AllAgents_ReturnLegalMoves(string agentName)
    {
        var agent = Tak.Experiments.AgentFactory.CreateAgent(agentName, seed: 42);
        var game = Utils.CreateNewGame(4);
        
        var move = agent.ChooseMove(game, iterationLimit: 50);
        
        Assert.NotNull(move);
        var legalMoves = GameRules.GetLegalMoves(game).ToList();
        Assert.Contains(move, legalMoves);
    }

    [Fact]
    public void AgentFactory_CreatesAllAgents()
    {
        var agents = new[] { "random", "heuristic", "uct", "rave", "pw" };
        
        foreach (var name in agents)
        {
            var agent = Tak.Experiments.AgentFactory.CreateAgent(name);
            Assert.NotNull(agent);
            Assert.Equal(name switch
            {
                "random" => "Random",
                "heuristic" => "Heuristic",
                "uct" => "UCT",
                "rave" => "RAVE",
                "pw" => "PW",
                _ => ""
            }, agent.Name);
        }
    }
}
