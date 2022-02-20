using System;
using System.Collections.Generic;
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
        static readonly IOutputWriter output = new ConsoleOutputWriter();

        readonly ElogConfiguration _config;

        public ElogCore()
        {
            AnsiConsole.Clear();
            _config = LoadConfiguration();
        }

        public void OnExecute(CommandLineApplication app)
        {            
            var assemblyReader = new AssemblyReader(_config.BinariesPath, output);
            assemblyReader.DiscoverDolittleTypes();

            if (AggregateName.Length > 0) // We are looking for a specific Aggregate
            {
                var map = assemblyReader.GenerateMapForAggregate(AggregateName);

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

        }

        [Option(Description = "Name of the aggregate to inspect, i.e. 'Product'")]
        public string AggregateName { get; set; } = string.Empty;

        [Option(Description = "Identity of the aggregate for which you want to see the event log", ShortName = "id")]
        public string Id { get; set; } = Guid.Empty.ToString();

        [Option(Description = "Display the payload of the event# ", ShortName = "evt")]
        public int EventNumber { get; set; } = -1;

        [Option(Description = "Name of configuration to load. Will load the first configuration if left blank")]
        public string Configuration { get; set; } = string.Empty;

        ElogConfiguration? LoadConfiguration()
        {
            var configurationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configurationFile = Path.Combine(configurationFolder, ElogConfiguration.ConfigurationFileName);
            if (!File.Exists(configurationFile))
            {
                Ansi.Error("No configuration found! Run 'elog.exe configure' to create a configuration.\nA wizard will guide you through the values required");
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
            var table = new Table()
                .AddColumns("Aggregate", "Id");

            foreach (var entry in aggregates)
            {
                table.AddRow(entry.Name, entry.Id.ToString());
            }
            Ansi.Info($"Found {aggregates.Count()} Aggregate Types:");
            AnsiConsole.Write(table);
            Ansi.Info($"Add '{ColorAs.Value("-a <aggregatename>")}' to see a list of individual aggregates\n");
        }

        async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB,
                output);

            var uniqueEventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

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

            output.Write($"{uniqueEventSources.Count()} unique Identities found for {map.Aggregate.Name}. \nAdd '-id <id>' to see their event log.\n");
        }

        async Task ListUniqueIdentifiers(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB,
                output);

            string guidId;

            var eventSources = await reader
                .GetUniqueEventSources(map)
                .ConfigureAwait(false);

            var matches = eventSources.Where(source => source.Id.ToString().Equals(Id, StringComparison.InvariantCultureIgnoreCase));

            if (!matches.Any())
            {
                output.Write(
                    $"No event-sources-ids for the aggregate {map.Aggregate.Name} starts with {Id}."
                );
                return;
            }
            if (matches.Count() > 1)
            {
                output.Write(
                    $"Two or more event-sources for the aggregate {map.Aggregate.Name} starts with {Id}"
                );
                return;
            }

            guidId = matches.First().Id;
            output.Write($"Found single match for \"{Id}\": {guidId}{Environment.NewLine}");

            var eventLog = (await reader.GetEventLog(map, guidId).ConfigureAwait(false)).ToList();

            if (EventNumber <= -1)
            {
                output.Write($"\nEvent history for '{map.Aggregate.Name}' Id: {guidId}");
                output.Divider();
                var table = new Table()
                    .AddColumns("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    var eventName = entry.Event + (entry.IsPublic ? "*" : "");

                    table.AddRow(counter++.ToString(), entry.Aggregate, eventName, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                }
                AnsiConsole.Write(table);

                output.Write("Add '-evt <#>' to see the payload details of that event\n");
            }
            else
            {
                if (EventNumber >= eventLog.Count)
                {
                    output.DisplayError($"The Event number values for this Event Source range from 0 t0 {eventLog.Count - 1} only.\n");
                    return;
                }
                var rightEvent = eventLog.ToList()[EventNumber];
                var json = JsonConvert.DeserializeObject(rightEvent.PayLoad);
                output.Write($"Displaying Payload #{EventNumber} for aggregate {map.Aggregate.Name}:{Id}");
                output.Write($"Event {EventNumber}: '{rightEvent.Event}' on {rightEvent.Time:dddd dd.MMMyyyy HH:mm:ss.ffff}");
                output.Divider();
                output.Write(json?.ToString() ?? "");
                output.Divider();
            }
        }

    }
}
