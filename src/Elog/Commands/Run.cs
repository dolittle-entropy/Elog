using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using AssemblyReading;
using Common;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Elog.Commands
{

    public class Run : Command<RunSettings>
    {
        readonly ElogConfiguration? _config;

        public Run()
        {
            AnsiConsole.Clear();
            _config = ElogConfigurationFactory
                .Load()
                ?.Display()
                ?? throw new UnableToLoadElogConfiguration();
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] RunSettings settings)
        {
            var assemblyReader = new AssemblyReader(_config.BinariesPath);
            assemblyReader.DiscoverDolittleTypes();

            if (settings.AggregateName.Length > 0) // We are looking for a specific Aggregate
            {
                var map = assemblyReader.GenerateMapForAggregate(settings.AggregateName);
                if (map is null)
                {
                    Out.Error($"Unable to map aggregate '{ColorAs.Value(settings.AggregateName)}'. Operation cancelled");
                    return 1;
                }

                if (settings.Id != Guid.Empty.ToString()) // We didn't provide an EventSource Id, so we list all unique aggregates
                {
                    ListUniqueIdentifiers(map, settings).Wait();
                }
                else // We have an event source ID, so we want to display the event log
                {
                    ListEventsForAggregate(map).Wait();
                }
            }
            else // We didn't supply an aggregate name, so we just list all of them
            {
                DisplayAggregateList(assemblyReader.DolittleAggregates);
            }
            return 0;
        }

        static void DisplayAggregateList(IEnumerable<TypeMapping.DolittleAggregate> aggregates)
        {
            if (aggregates?.Any() ?? false)
            {

                var table = new Table()
                    .AddColumns("Aggregate name", "Mapped to Id");

                const int take = 20;
                int skip = 0;
                foreach (var entry in aggregates.Take(take).Skip(skip))
                {
                    table.AddRow(entry.Name, entry.Id.ToString());
                }
                Out.Info($"Found {aggregates.Count()} Aggregate Types:");
                AnsiConsole.Write(table);

                Out.Info($"Add the '{ColorAs.Value("<aggregatename>")}' to drill down into a list of individual aggregates{Environment.NewLine}");
            }
            else
            {
                Out.Warning("No aggregates found");
            }
        }

        async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(_config.MongoConfig);

            var uniqueEventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

            if (uniqueEventSources?.Any() ?? false)
            {
                var table = new Table()
                    .AddColumns("Aggregate", "Id", "Events");

                table.Columns[2].RightAligned();

                foreach (var uniqueEventSource in uniqueEventSources)
                {
                    table.AddRow(
                        uniqueEventSource.Aggregate,
                        uniqueEventSource.Id.ToString(),
                        uniqueEventSource.EventCount.ToString()
                    );
                }
                AnsiConsole.Write(table);
                Out.Info($"{ColorAs.Value(uniqueEventSources.Count().ToString())} unique Identities found for {ColorAs.Value(map.Aggregate.Name)}. {Environment.NewLine}Add the {ColorAs.Value("<identity>")}' to see its event log.\n");
            }
            else
            {
                Out.Warning($"No aggregates were found for type '{ColorAs.Value(map.Aggregate.Name)}'");
            }

        }

        async Task ListUniqueIdentifiers(TypeMapping.DolittleTypeMap map, [NotNull] RunSettings settings)
        {
            var reader = new EventStoreReader(_config.MongoConfig);

            string guidId;

            var eventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

            var matches = eventSources.Where(source => source.Id.ToString().Equals(settings.Id, StringComparison.InvariantCultureIgnoreCase));

            if (!matches.Any())
            {
                Out.Warning($"No event-sources-ids for the aggregate {map.Aggregate.Name} starts with {settings.Id}.");
                return;
            }
            if (matches.Count() > 1)
            {
                Out.Warning($"Two or more event-sources for the aggregate {map.Aggregate.Name} starts with {settings.Id}");
                return;
            }

            guidId = matches.First().Id;
            Out.Success($"Found a match for {map.Aggregate.Name} #{settings.Id}{Environment.NewLine}");

            var eventLog = (await reader.GetEventLog(map, guidId).ConfigureAwait(false)).ToList();

            if (settings.EventNumber <= -1)
            {
                Out.Info($"{Environment.NewLine}Event history for '{ColorAs.Value(map.Aggregate.Name)}' Id: {guidId}");

                var table = new Table()
                    .AddColumns("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    var eventName = entry.Event + (entry.IsPublic ? "*" : "");

                    table.AddRow(counter++.ToString(), entry.Aggregate, eventName, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                }
                AnsiConsole.Write(table);

                Out.Info($"Add the {ColorAs.Value("<event number>")} to see the payload of the individual event{Environment.NewLine}");
            }
            else
            {
                if (settings.EventNumber >= eventLog.Count)
                {
                    Out.Error($"The Event number values for this Event Source range from 0 t0 {eventLog.Count - 1} only.{Environment.NewLine}");
                    return;
                }
                var rightEvent = eventLog.ToList()[settings.EventNumber];
                var json = JsonConvert.DeserializeObject(rightEvent.PayLoad);
                Out.Info($"Displaying event number {ColorAs.Value(settings.EventNumber.ToString())} from aggregate {ColorAs.Value(map.Aggregate.Name)} with id {ColorAs.Value(settings.Id)}:");
                Out.Info($"This event is of type {ColorAs.Value(rightEvent.Event)} and was applied on {ColorAs.Value(rightEvent.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"))}");
                Out.Content("JSON Content", json?.ToString() ?? ColorAs.Error("--NO CONTENT--"));
            }
        }
    }
}
