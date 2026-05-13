namespace Tak.Experiments;

using Tak.Core;
using Tak.AI;
using System.Diagnostics;
using CsvHelper;
using System.Globalization;

/// <summary>Tournament result record for CSV</summary>
public class TournamentResultRecord
{
    public int GameIndex { get; set; }
    public int BoardSize { get; set; }
    public string? AgentWhite { get; set; }
    public string? AgentBlack { get; set; }
    public string? Winner { get; set; }
    public string? ResultType { get; set; }
    public int Moves { get; set; }
    public long DurationMs { get; set; }
    public double AverageMoveTimeMs { get; set; }
    public double SimulationsPerSecond { get; set; }
    public int Seed { get; set; }
    public int IterationLimit { get; set; }
}

/// <summary>Tournament runner</summary>
public class Tournament
{
    private readonly GameConfig config;
    private readonly IAgent agentA;
    private readonly IAgent agentB;
    private readonly int gamesPerSide;
    private readonly int iterationLimit;
    private readonly int baseSeed;
    private readonly string outputPath;

    public Tournament(GameConfig config, IAgent agentA, IAgent agentB, int gamesPerSide, int iterationLimit, int baseSeed, string outputPath)
    {
        this.config = config;
        this.agentA = agentA;
        this.agentB = agentB;
        this.gamesPerSide = gamesPerSide;
        this.iterationLimit = iterationLimit;
        this.baseSeed = baseSeed;
        this.outputPath = outputPath;
    }

    public void Run()
    {
        var results = new List<TournamentResultRecord>();
        int winsA = 0, winsB = 0, draws = 0;
        long totalDuration = 0;

        Console.WriteLine($"Starting tournament: {agentA.Name} vs {agentB.Name}");
        Console.WriteLine($"Configuration: {config}");
        Console.WriteLine($"Games: {gamesPerSide * 2}, Iterations: {iterationLimit}");
        Console.WriteLine();

        for (int game = 0; game < gamesPerSide * 2; game++)
        {
            IAgent whiteAgent, blackAgent;
            string whiteRole, blackRole;

            if (game < gamesPerSide)
            {
                whiteAgent = agentA;
                blackAgent = agentB;
                whiteRole = agentA.Name;
                blackRole = agentB.Name;
            }
            else
            {
                whiteAgent = agentB;
                blackAgent = agentA;
                whiteRole = agentB.Name;
                blackRole = agentA.Name;
            }

            var gameSeed = baseSeed + game;
            var sw = Stopwatch.StartNew();

            var result = PlayGame(whiteAgent, blackAgent, gameSeed);

            sw.Stop();

            var record = new TournamentResultRecord
            {
                GameIndex = game + 1,
                BoardSize = config.BoardSize,
                AgentWhite = whiteRole,
                AgentBlack = blackRole,
                Winner = result.Winner == Player.White ? whiteRole : 
                         result.Winner == Player.Black ? blackRole : "Draw",
                ResultType = result.Type.ToString(),
                Moves = result.Moves.Count,
                DurationMs = sw.ElapsedMilliseconds,
                AverageMoveTimeMs = sw.ElapsedMilliseconds / (double)Math.Max(1, result.Moves.Count),
                IterationLimit = iterationLimit,
                Seed = gameSeed
            };

            results.Add(record);
            totalDuration += sw.ElapsedMilliseconds;

            // Track wins
            if (result.Winner == Player.White && whiteRole == agentA.Name)
                winsA++;
            else if (result.Winner == Player.Black && blackRole == agentA.Name)
                winsA++;
            else if (result.Type == ResultType.Draw)
                draws++;
            else
                winsB++;

            Console.WriteLine($"Game {game + 1:D2}/{gamesPerSide * 2}: {whiteRole} vs {blackRole} → {record.Winner} ({result.Type}) [{result.Moves.Count} moves, {sw.ElapsedMilliseconds}ms]");
        }

        // Write CSV
        WriteResults(results, outputPath);

        // Print summary
        Console.WriteLine();
        Console.WriteLine("=== TOURNAMENT SUMMARY ===");
        Console.WriteLine($"Agent A ({agentA.Name}): {winsA} wins");
        Console.WriteLine($"Agent B ({agentB.Name}): {winsB} wins");
        Console.WriteLine($"Draws: {draws}");
        Console.WriteLine($"Total games: {gamesPerSide * 2}");
        Console.WriteLine($"Win rate (A): {(winsA * 100.0) / (gamesPerSide * 2):F1}%");
        Console.WriteLine($"Total duration: {totalDuration}ms");
        Console.WriteLine($"Average game time: {totalDuration / (gamesPerSide * 2)}ms");
        Console.WriteLine();
        Console.WriteLine($"Results written to: {outputPath}");
    }

    private GameResult PlayGame(IAgent white, IAgent black, int seed)
    {
        var state = Utils.CreateNewGame(config.BoardSize);
        
        while (state.Result == null)
        {
            var agent = state.CurrentPlayer == Player.White ? white : black;
            var move = agent.ChooseMove(state, iterationLimit: iterationLimit);
            state = state.MakeMove(move);
        }

        return state.Result;
    }

    private void WriteResults(List<TournamentResultRecord> results, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        
        using (var writer = new StreamWriter(path))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(results);
        }
    }
}
