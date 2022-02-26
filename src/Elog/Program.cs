using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Elog.Commands;
using McMaster.Extensions.CommandLineUtils;
using OutputWriting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Elog;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp<Splash>();

        app.Configure(config =>
        {
            config.AddCommand<Events>("events")
                .WithAlias("e")
                .WithDescription("Display events found in current configuration")
                .WithExample(new[]
                    { "events", "[Event Type]"});

            config.AddCommand<Elog.Commands.Run>("aggregates")
                .WithAlias("a")
                .WithDescription("Run a scan of all aggregates")
                .WithExample(new[]
                    {"run", "[Aggregate name]", "[Aggregate Id]", "[Event number to show]"});

            config.AddCommand<Elog.Commands.Configure>("configure")
                .WithAlias("c")
                .WithDescription("Manage your Elog Configuration");

            config.Settings.ApplicationName = "Elog";
                
        });

        var stopwatch = Stopwatch.StartNew();

        var result = app.Run(args);
        if(result == 0)
            Out.Info($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
        else
            Out.Error($"Program finished with errors in {stopwatch.ElapsedMilliseconds:### ###.0}ms");

        AnsiConsole.Reset();
        return result;
    }
}

//[Command(Name = "elog", Description = "Explore aggregates and their eventlogs")]
//[Subcommand(typeof(Run))]
//[Subcommand(typeof(Configure))]
//[Subcommand(typeof(Events))]
//public class Program
//{
//    public static Task Main(string[] args)
//    => CommandLineApplication.ExecuteAsync<Program>(args);

//    public void OnExecute(CommandLineApplication app)
//    {
//        if(Version)
//        {
//            Out.Success($"Elog version {ColorAs.Value(Assembly.GetExecutingAssembly().GetName().Version.ToString())}. All rights reversed");
//            return;
//        }
//        var stopwatch = Stopwatch.StartNew();
//        AnsiConsole.Clear();
//        AnsiConsole.Write(new FigletText("ELOG").LeftAligned().Color(Color.Orange1));
//        app.ShowHelp();
//        Out.Info($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
//        AnsiConsole.Reset();
//    }

//    [Option(Description = "Display version information")]
//    public bool Version { get; set; }
// }

