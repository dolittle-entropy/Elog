using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AssemblyReading;
using Common;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using OutputWriting;
using Spectre.Console;
using Spectre.Console.Cli;
using TypeMapping;

namespace Elog.Commands
{

    public class EventSettings : CommandSettings
    {
        [CommandArgument(0, "[EventType name]")]
        public string Filter { get; set; }
    }

    public class Events : Command<EventSettings>
    {
        readonly ElogConfiguration? _config;
        readonly EventStoreReader _eventStoreReader;
        readonly AssemblyReader _assemblyReader;

        public Events()
        {
            AnsiConsole.Clear();
            _config = ElogConfigurationFactory.Load()?.Display()
                ?? throw new UnableToLoadElogConfiguration();
            _eventStoreReader = new EventStoreReader(_config.MongoConfig.MongoServer, _config.MongoConfig.Port, _config.MongoConfig.MongoDB);
            _assemblyReader = new AssemblyReader(_config.BinariesPath);
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] EventSettings settings)
        {
            _assemblyReader.DiscoverDolittleTypes();

            if (!string.IsNullOrEmpty(settings.Filter))
            {
                DisplayFiltered(settings.Filter);
                return 0;
            }

            DisplayEventList(_assemblyReader.DolittleEvents);
            return 0;
        }

        private void DisplayFiltered(string filter)
        {
            if (_assemblyReader.DolittleEvents.FirstOrDefault(e => e.Name.Equals(filter, System.StringComparison.InvariantCultureIgnoreCase)) is { } eventType)
            {
                var eventUsage = _eventStoreReader.GetEventTypeUsage(eventType, _assemblyReader.DolittleAggregates).Result;
                DisplayEventUsage(eventUsage);
            }

        }

        private void DisplayEventUsage(DolittleEventUsage? eventUsage)
        {

            var headerTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumns("Sample", "Value");

            headerTable.AddRow("Event Type",            ColorAs.Value(eventUsage.DolittleEvent.Name));
            headerTable.AddRow("Event Type Id ",        ColorAs.Value(eventUsage.DolittleEvent.Id.ToString()));
            headerTable.AddRow("Number of invocations", ColorAs.Value(eventUsage.InvocationCount.ToString()));
            headerTable.AddRow("First offset",          ColorAs.Value(eventUsage.FirstOffset.ToString()));
            headerTable.AddRow("Last offset",           ColorAs.Value(eventUsage.LastOffset.ToString()));

            AnsiConsole.Write(headerTable);

            if (eventUsage.InvocationCount > 0)
            {
                var table = new Table()
                    .AddColumn("Invoked By Aggregate")
                    .AddColumn("Aggregate Id")
                    .AddColumn("Invocations", config => config.RightAligned())
                    .RoundedBorder();

                foreach (var info in eventUsage.AggregateUsages.Values)
                {
                    table.AddRow(info.Aggregate.Name, info.Aggregate.Id.ToString(), info.InvocationCount.ToString("### ### ###"));
                }
                AnsiConsole.Write(table);
            }
            else
                Out.Warning($"No invocations of {ColorAs.Value(eventUsage.DolittleEvent.Name)} were found in the event log");
        }

        private void DisplayEventList(List<DolittleEvent> dolittleEvents)
        {
            var cancelChoice = ColorAs.Warning("*** CANCEL ***");

            var table = new Table().AddColumns("Event Type", "Event Type Id");

            var orderedEvents = dolittleEvents.OrderBy(x => x.Name).ToList();
            for (int i = 0; i < orderedEvents.Count; i++)
            {
                table.AddRow(orderedEvents[i].Name, orderedEvents[i].Id);
            }
            Out.Info($"Found {orderedEvents.Count} Event types:");
            AnsiConsole.Write(table);

            var choices = new List<string>();
            choices.Add(cancelChoice);
            choices.AddRange(dolittleEvents.Select(x => x.Name));

            Out.Info($"Select event to inspect or {cancelChoice} to finish:");

            var response = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .PageSize(3)
                .MoreChoicesText(ColorAs.Info("Use Up/down to scroll"))
                .AddChoices(choices));

            if (response == cancelChoice)
                return;

            DisplayFiltered(response);
        }
    }
}
