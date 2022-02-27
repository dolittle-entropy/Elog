using System.Diagnostics;
using Elog.Commands;
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
        if (result == 0)
            Out.Info($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
        else
            Out.Error($"Program finished with errors in {stopwatch.ElapsedMilliseconds:### ###.0}ms");

        AnsiConsole.Reset();
        return result;
    }
}

