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
        using var _ = new TimedAction("Elog");

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
        return app.Run(args);
    }
}

