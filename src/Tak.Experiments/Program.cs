namespace Tak.Experiments;

using Tak.Core;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = ExperimentCli.Parse(args);

            if (options.HelpRequested)
            {
                Console.WriteLine(ExperimentCli.GetUsage());
                return 0;
            }

            var config = new GameConfig(options.BoardSize);

            var whiteSpec = new AgentSpec(
                options.WhiteAgent,
                seed => AgentFactory.CreateAgent(options.WhiteAgent, seed, options.Exploration));

            var blackSpec = new AgentSpec(
                options.BlackAgent,
                seed => AgentFactory.CreateAgent(options.BlackAgent, seed, options.Exploration));

            var tournament = new Tournament(
                config,
                whiteSpec,
                blackSpec,
                options.Games,
                options.IterationLimit,
                ExperimentCli.ToMoveTimeLimit(options.MoveTimeLimitMs),
                options.Seed,
                options.OutputPath);

            tournament.Run();

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(ExperimentCli.GetUsage());
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
