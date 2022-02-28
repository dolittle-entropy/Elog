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
using TypeMapping;

namespace Elog.Commands
{

    public class Run : Command<RunSettings>
    {
        readonly ElogConfiguration? _config;
        AssemblyReader _assemblyReader;

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
            _assemblyReader = new AssemblyReader(_config.BinariesPath);
            _assemblyReader.DiscoverDolittleTypes();

            if (settings.AggregateName.Length > 0) // We are looking for a specific Aggregate
            {
                var map = _assemblyReader.GenerateMapForAggregate(settings.AggregateName);
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
                    ListEventsForAggregate(map, settings).Wait();
                }
            }
            else // We didn't supply an aggregate name, so we just list all of them
            {
                DisplayAggregateList(_assemblyReader.DolittleAggregates, settings);
            }
            return 0;
        }

        void DisplayAggregateList(IEnumerable<TypeMapping.DolittleAggregate> aggregates, [NotNull] RunSettings settings)
        {
            if (aggregates?.Any() ?? false)
            {
                new LiveDataTable<DolittleAggregate>()
                    .WithHeader($"Found {aggregates.Count()} Aggregate roots:")
                    .WithDataSource(aggregates)
                    .WithColumns("Aggregate root", "Aggregate root identifier")
                    .WithDataPicker(a => new List<string> { a.Name, a.Id.ToString(), })
                    .WithEnterInstruction("drill into {0}", p => p.Name)
                    .WithSelectionAction(selectedAggregate =>
                    {
                        settings.AggregateName = selectedAggregate.Name;
                        var map = _assemblyReader.GenerateMapForAggregate(settings.AggregateName);
                        ListEventsForAggregate(map, settings).Wait();
                    }).Start();
            }
            else
            {
                Out.Warning("No aggregates found");
            }
        }

        async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map, [NotNull] RunSettings settings)
        {
            var reader = new EventStoreReader(_config.MongoConfig);

            var uniqueEventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

            if (!uniqueEventSources?.Any() ?? true)
            {
                Out.Warning($"No aggregates were found for type '{ColorAs.Value(map.Aggregate.Name)}'");
                return;
            }
            Out.Info($"{ColorAs.Value(uniqueEventSources.Count().ToString())} unique Identities found for {ColorAs.Value(map.Aggregate.Name)}. {Environment.NewLine}Add the {ColorAs.Value("<identity>")}' to see its event log.\n");

            var dataTable = new Table()
                .Border(TableBorder.Rounded);

            var liveTable = new LiveDataTable<EventSource>()
                .WithHeader("Use right/left to flip pages. Commands: [[ (i)nspect ]]")
                .WithDataSource(uniqueEventSources)
                .WithColumns("Aggregate root", "Aggregate Id", "Event count", "Last offset", "Last updated")
                .WithEnterInstruction("Inspect Aggregate with ID: {0}", p => p.Id)
                .WithDataPicker(item =>
                {
                    return new List<string>
                    {
                        item.Aggregate,
                        item.Id,
                        item.EventCount.ToString(),
                        item.LastOffset.ToString(),
                        item.LastOccurred.ToString(Out.DetailedTimeFormat)
                    };
                })
                .WithSelectionAction(source =>
                {
                    var selectedMap = _assemblyReader.GenerateMapForAggregate(source.Aggregate);
                    if (selectedMap != null)
                    {
                        settings.AggregateName = selectedMap.Aggregate.Name;
                        settings.Id = source.Id;
                        ListUniqueIdentifiers(selectedMap, settings).Wait();
                    }
                });

            liveTable.Start();
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

            var liveTable = new LiveDataTable<EventEntry>()
                .WithHeader($"{Environment.NewLine}Event history for '{ColorAs.Value(map.Aggregate.Name)}' Id: {guidId}")
                .WithEnterInstruction("see the payload of the selected '{0}' event", e => e.Event)
                .WithDataSource(eventLog)
                .WithColumns("Aggregate", "Event", "Time")
                .WithDataPicker(e => new List<string>
                    {
                        e.Aggregate,
                        e.Event,
                        e.Time.ToString(Out.DetailedTimeFormat)
                    })
                .WithSelectionAction(e => DisplayEventPayload(map, e, settings));

            liveTable.Start();
        }

        void DisplayEventPayload(DolittleTypeMap map, EventEntry eventEntry, [NotNull] RunSettings settings)
        {
            var json = JsonConvert.DeserializeObject(eventEntry.PayLoad);
            Out.Info($"Displaying {eventEntry.Event} from aggregate {ColorAs.Value(map.Aggregate.Name)} with id {ColorAs.Value(settings.Id)}:");
            Out.Info($"This event is of type {ColorAs.Value(eventEntry.Event)} and was applied on {ColorAs.Value(eventEntry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"))}");
            Out.Content("JSON Content", json?.ToString() ?? ColorAs.Error("--NO CONTENT--"));
        }
    }
}
