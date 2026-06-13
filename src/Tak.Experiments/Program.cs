namespace Tak.Experiments;

using Tak.Core;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            // Parse command line arguments
            int boardSize = 5;
            string agentA = "random";
            string agentB = "heuristic";
            int games = 100;
            int iterations = 1000;
            int moveTimeMs = 1000;
            int seed = new Random().Next();
            double exploration = 1.414;
            string output = "results/tournament.csv";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--board-size":
                        boardSize = int.Parse(args[++i]);
                        break;
                    case "--agent-a":
                        agentA = args[++i];
                        break;
                    case "--agent-b":
                        agentB = args[++i];
                        break;
                    case "--games":
                        games = int.Parse(args[++i]);
                        break;
                    case "--iterations":
                        iterations = int.Parse(args[++i]);
                        break;
                    case "--move-time-ms":
                        moveTimeMs = int.Parse(args[++i]);
                        break;
                    case "--seed":
                        seed = int.Parse(args[++i]);
                        break;
                    case "--exploration":
                        exploration = double.Parse(args[++i]);
                        break;
                    case "--output":
                        output = args[++i];
                        break;
                }
            }

            // Create configuration and agents
            var config = new GameConfig(boardSize);
            var a = AgentFactory.CreateAgent(agentA, seed, exploration);
            var b = AgentFactory.CreateAgent(agentB, seed, exploration);

            // Run tournament
            var moveTimeLimit = moveTimeMs > 0 ? TimeSpan.FromMilliseconds(moveTimeMs) : (TimeSpan?)null;
            var tournament = new Tournament(config, a, b, games / 2, iterations, moveTimeLimit, seed, output);
            tournament.Run();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
