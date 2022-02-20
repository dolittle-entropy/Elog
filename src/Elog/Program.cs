using System.Diagnostics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OutputWriting;
using Spectre.Console;

namespace Elog
{
    [Command(Name = "elog", Description = "Explore aggregates and their eventlogs")]
    [Subcommand(typeof(ElogCore))]
    [Subcommand(typeof(Configure))]
    public class Program
    {
        public static Task Main(string[] args)
        => CommandLineApplication.ExecuteAsync<Program>(args);

        public void OnExecute(CommandLineApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("ELOG").LeftAligned().Color(Color.Orange1));
            app.ShowHelp();
            Ansi.Info($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
            AnsiConsole.Reset();
        }
    }
}

