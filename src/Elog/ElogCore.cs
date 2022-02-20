using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssemblyReading;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using Spectre.Console;

namespace Elog
{
    [Command("run", Description = "Run Elog")]
    public class ElogCore
    {
        [Option(Description = "Name of the aggregate to inspect, i.e. 'Product'")]
        public string AggregateName { get; set; } = string.Empty;

        [Option(Description = "Identity of the aggregate for which you want to see the event log", ShortName = "id")]
        public string Id { get; set; } = Guid.Empty.ToString();

        [Option(Description = "Display the payload of the event# ", ShortName = "n")]
        public int EventNumber { get; set; } = -1;

        readonly ElogConfiguration? _config;

        public ElogCore()
        {
            AnsiConsole.Clear();
            _config = LoadConfiguration();
            if (_config is null)
            {                
                return;
            }
        }

        public void OnExecute(CommandLineApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            if (_config is null)
                return;

            var assemblyReader = new AssemblyReader(_config.BinariesPath);
            assemblyReader.DiscoverDolittleTypes();

            if (AggregateName.Length > 0) // We are looking for a specific Aggregate
            {
                var map = assemblyReader.GenerateMapForAggregate(AggregateName);
                if(map is null)
                {
                    Ansi.Error($"Unable to map aggregate '{ColorAs.Value(AggregateName)}'. Operation cancelled");
                    return;
                }

                if (Id != Guid.Empty.ToString()) // We didn't provide an EventSource Id, so we list all unique aggregates
                {
                    ListUniqueIdentifiers(map).Wait();
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

            Ansi.Info($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
            AnsiConsole.Reset();
        }

        ElogConfiguration? LoadConfiguration()
        {
            var configurationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configurationFile = Path.Combine(configurationFolder, ElogConfiguration.ConfigurationFileName);
            if (!File.Exists(configurationFile))
            {
                Ansi.Error($"No configuration found! Run '{ColorAs.Value("elog config")}' to create your first configuration.{Environment.NewLine}");
                return default;
            }
            var fileContents = File.ReadAllText(configurationFile);
            var configurations = JsonConvert.DeserializeObject<List<ElogConfiguration>>(fileContents);
            var configuration = configurations.FirstOrDefault(c => c.IsDefault);
            if (configuration == null)
            {
                Ansi.Error($"No configurations exist. Run '{ColorAs.Value("Elog configure")}' to create your first configuration");
                return default;
            }
            DisplayConfiguration(configuration, configurationFile);
            return configuration;
        }

        void DisplayConfiguration(ElogConfiguration configuration, string configPath)
        {
            Ansi.Info($"Loaded Configuration: {ColorAs.Value(configPath)}");

            var table = new Table()
                .AddColumn("Setting")
                .AddColumn("Value");

            table.Border = TableBorder.Horizontal;

            table.AddRow("Configuration name", ColorAs.Value(configuration.Name));
            table.AddRow("MongoDB Server", ColorAs.Value(configuration.MongoConfig.MongoServer));
            table.AddRow("EventStore Database Name", ColorAs.Value(configuration.MongoConfig.MongoDB));
            table.AddRow("MongoDb Port", ColorAs.Value(configuration.MongoConfig.Port.ToString()));
            AnsiConsole.Write(table);
            Console.WriteLine();
        }

        static void DisplayAggregateList(IEnumerable<TypeMapping.DolittleAggregate> aggregates)
        {
            if(aggregates?.Any() ?? false)
            {
                var table = new Table()
                    .AddColumns("Aggregate name", "Mapped to Id");

                foreach (var entry in aggregates)
                {
                    table.AddRow(entry.Name, entry.Id.ToString());
                }
                Ansi.Info($"Found {aggregates.Count()} Aggregate Types:");
                AnsiConsole.Write(table);
                Ansi.Info($"Add '{ColorAs.Value("-a <aggregatename>")}' to see a list of individual aggregates{Environment.NewLine}");
            }
            else
            {
                Ansi.Warning("No aggregates found");
            }
        }

        async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB);

            var uniqueEventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

            if(uniqueEventSources?.Any() ?? false)
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
                Ansi.Info($"{ColorAs.Value(uniqueEventSources.Count().ToString())} unique Identities found for {ColorAs.Value(map.Aggregate.Name)}. {Environment.NewLine}Add '-id <id>' to see their event log.\n");
            }
            else
            {
                Ansi.Warning($"No aggregates were found for type '{ColorAs.Value(map.Aggregate.Name)}'");
            }

        }

        async Task ListUniqueIdentifiers(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB);

            string guidId;

            var eventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

            var matches = eventSources.Where(source => source.Id.ToString().Equals(Id, StringComparison.InvariantCultureIgnoreCase));

            if (!matches.Any())
            {
                Ansi.Warning($"No event-sources-ids for the aggregate {map.Aggregate.Name} starts with {Id}.");
                return;
            }
            if (matches.Count() > 1)
            {
                Ansi.Warning($"Two or more event-sources for the aggregate {map.Aggregate.Name} starts with {Id}");
                return;
            }

            guidId = matches.First().Id;
            Ansi.Success($"Found a match for {map.Aggregate.Name} #{Id}{Environment.NewLine}");

            var eventLog = (await reader.GetEventLog(map, guidId).ConfigureAwait(false)).ToList();

            if (EventNumber <= -1)
            {
                Ansi.Info($"{Environment.NewLine}Event history for '{ColorAs.Value(map.Aggregate.Name)}' Id: {guidId}");
                
                var table = new Table()
                    .AddColumns("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    var eventName = entry.Event + (entry.IsPublic ? "*" : "");

                    table.AddRow(counter++.ToString(), entry.Aggregate, eventName, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                }
                AnsiConsole.Write(table);

                Ansi.Info($"Add '-n <#>' to see the payload of the event{Environment.NewLine}");
            }
            else
            {
                if (EventNumber >= eventLog.Count)
                {
                    Ansi.Error($"The Event number values for this Event Source range from 0 t0 {eventLog.Count - 1} only.{Environment.NewLine}");
                    return;
                }
                var rightEvent = eventLog.ToList()[EventNumber];
                var json = JsonConvert.DeserializeObject(rightEvent.PayLoad);
                Ansi.Info($"Displaying event number {ColorAs.Value(EventNumber.ToString())} from aggregate {ColorAs.Value(map.Aggregate.Name)} with id {ColorAs.Value(Id)}:");
                Ansi.Info($"This event is of type {ColorAs.Value(rightEvent.Event)} and was applied on {ColorAs.Value(rightEvent.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"))}");
                Ansi.Content("JSON Content", json?.ToString() ?? ColorAs.Error("--NO CONTENT--"));                
            }
        }

    }
}
