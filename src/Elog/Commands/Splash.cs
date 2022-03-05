using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OutputWriting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Elog.Commands
{

    public class Splash : Command<SplashSettings>
    {
        class HackyCommand
        {
            public string Name { get; set; }
            public string Alias { get; set; }
            public string Description { get; set; }

            public HackyCommand()
            {
            }

            public HackyCommand(string name, string alias, string description)
            {
                Name = name;
                Alias = alias;
                Description = description;
            }
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] SplashSettings settings)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("ELOG").LeftAligned().Color(Color.Orange1));
            ShowVersion(context);
            Out.Info($"A handy tool for working with Dolittle powered applications.{Environment.NewLine}");
            ShowHelp(context);            

            AnsiConsole.Reset();
            return 0;
        }

        private void ShowHelp(CommandContext context)
        {
            var hackyCommands = new List<HackyCommand>
            {
                new HackyCommand("Aggregates", "a", "Drill down into the Aggregates"),
                new HackyCommand("Events", "e", "Drill into event types and their usages"),
                new HackyCommand("Configure", "c", "Manage your Elog configuration"),
                new HackyCommand("Cancel", "", "Exit this selection. [[ESC]] also cancels table selections")
            };

            new LiveDataTable<HackyCommand>()
                .WithoutBorders()
                .WithHeader("[bold]ACTIONS:[/]")
                .WithColumns("Command", "Description")
                .WithDataSource(hackyCommands)
                .WithDataPicker(p => new(){ p.Name, p.Description })
                .WithEnterInstruction("invoke '{0}'", p => p.Name)
                .WithSelectionAction(selectedOption =>
                {
                    if (selectedOption.Name != "Cancel")
                        SelectCommand(context, selectedOption);
                })
                .Start();            
        }

        private void SelectCommand(CommandContext context, [NotNull] HackyCommand command)
        {
            switch (command.Name)
            {
                case "Configure":
                    {
                        var commandSettings = new ConfigureSettings();
                        var configCommand = new Configure();
                        configCommand.Execute(context, commandSettings);
                        break;
                    }

                case "Aggregates":
                    {
                        var aggregateSettings = new RunSettings();
                        var aggregateCommand = new Run();
                        aggregateCommand.Execute(context, aggregateSettings);
                        break;
                    }

                case "Events":
                    {
                        var eventSettings = new EventSettings();
                        var eventCommand = new Events();
                        eventCommand.Execute(context, eventSettings);
                        break;
                    }
            };
        }

        private static int ShowVersion(CommandContext context)
        {
            var versionInformation = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            Out.Success($"Dolittle.Elog version {ColorAs.Value(versionInformation.ProductVersion)}. MIT Licensed software 2020-2022 Dolittle AS");
            return 0;
        }
    }
}
