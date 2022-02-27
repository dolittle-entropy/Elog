using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OutputWriting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Elog.Commands
{
    public class SplashSettings : CommandSettings
    {

    }

    public class Splash : Command<SplashSettings>
    {
        public override int Execute([NotNull] CommandContext context, [NotNull] SplashSettings settings)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("ELOG").LeftAligned().Color(Color.Orange1));

            ShowHelp(context);
            ShowVersion(context);

            AnsiConsole.Reset();
            return 0;
        }

        private void ShowHelp(CommandContext context)
        {
            Out.Info($"[bold]COMMANDS:[/]");
            var table = new Table()
                .AddColumns("Command", "Alias", "Purpose")
                .Border(TableBorder.Simple);

            table.AddRow("configure", "c", "Manage Elog configurations ");
            table.AddRow("aggregates", "a", "Drill down into aggregates using the active configuration");
            table.AddRow("events", "e", "Drill down into EventTypes using the active configuration");

            AnsiConsole.Write(table);
        }

        private static int ShowVersion(CommandContext context)
        {
            var versionInformation = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            Out.Success($"Dolittle.Elog version {ColorAs.Value(versionInformation.ProductVersion)}. MIT Licensed software 2020-2022 Dolittle AS");
            return 0;
        }
    }
}
