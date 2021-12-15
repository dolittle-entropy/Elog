
using AssemblyReading;
using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Elog
{
    [Command(Name = "elog", Description = "Lets you explore aggregates and their eventlogs")]
    [Subcommand(typeof(Configure))]
    public class Program
    {
        static readonly IOutputWriter output = new ConsoleOutputWriter();

        ElogConfiguration _config;

        public static Task Main(string[] args)
        {
            return CommandLineApplication.ExecuteAsync<Program>(args);
        }

        // warnings that the parameter is not used and can be removed
#pragma warning disable RCS1163, IDE0060
        public async Task OnExecute(CommandLineApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            _config = LoadConfiguration();
            if (_config == null)
            {
                return;
            }
            var assemblyReader = new AssemblyReader(_config.BinariesPath, output);

            if (AggregateName.Length > 0) // We are looking for a specific Aggregate
            {
                var map = assemblyReader.GenerateMapForAggregate(AggregateName);

                if (Id != Guid.Empty.ToString()) // We didn't provide an EventSource Id, so we list all unique aggregates
                {
                    await ListUniqueIdentifiers(map).ConfigureAwait(false);
                }
                else // We have an event source ID, so we want to display the event log
                {
                    await ListEventsForAggregate(map).ConfigureAwait(false);
                }
            }
            else // We didn't supply an aggregate name, so we just list all of them
            {
                var aggregates = assemblyReader.GetAllAggregates();
                DisplayAggregateList(aggregates);
            }
            output.Write($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
        }
#pragma warning restore RCS1163, IDE0060

        [Option(Description = "Name of the aggregate to inspect, i.e. 'Product'")]
        public string AggregateName { get; set; } = string.Empty;

        [Option(Description = "Identity of the aggregate for which you want to see the event log", ShortName = "id")]
        public string Id { get; set; } = Guid.Empty.ToString();

        [Option(Description = "Display the payload of the event# ", ShortName = "evt")]
        public int EventNumber { get; set; } = -1;

        [Option(Description = "Name of configuration to load. Will load the first configuration if left blank")]
        public string Configuration { get; set; } = string.Empty;

        ElogConfiguration LoadConfiguration()
        {
            var configurationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configurationFile = Path.Combine(configurationFolder, ElogConfiguration.ConfigurationFileName);
            if (!File.Exists(configurationFile))
            {
                output.DisplayError("No configuration found! Run 'elog.exe configure' to create a configuration.\nA wizard will guide you through the values required");
                return null;
            }
            var fileContents = File.ReadAllText(configurationFile);
            var configurations = JsonConvert.DeserializeObject<List<ElogConfiguration>>(fileContents);
            ElogConfiguration configuration = null;
            if (Configuration.Length == 0)
            {
                configuration = configurations[0];
            }
            else
            {
                configuration = configurations.Find(c => c.Name.Equals(Configuration, StringComparison.InvariantCultureIgnoreCase));
                if (configuration is null)
                {
                    output.DisplayError($"The configuration '{Configuration}' was not found. Aborting.");
                    return null;
                }
            }
            output.Write($@"
Configuration loaded: {configuration.Name}
Mongo Server        : {configuration.MongoConfig.MongoServer}:{configuration.MongoConfig.Port}
Mongo Database      : {configuration.MongoConfig.MongoDB}
Binaries Path       : {configuration.BinariesPath}
");
            return configuration;
        }

        static void DisplayAggregateList(IEnumerable<TypeMapping.DolittleAggregate> aggregates)
        {
            var table = new ConsoleTable("Aggregate", "Id");
            foreach (var entry in aggregates)
            {
                table.AddRow(entry.Name, entry.Id);
            }
            table.Write(Format.Minimal);
            output.Divider();
            output.Write($"{aggregates.Count()} identified Aggregates. Add '-a <aggregatename>' to see business entities\n");
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

            var table = new ConsoleTable("Aggregate", "Id", "Events");
            output.Divider();
            foreach (var uniqueEventSource in uniqueEventSources)
            {
                table.AddRow(
                    uniqueEventSource.Aggregate,
                    uniqueEventSource.Id,
                    uniqueEventSource.EventCount
                );
            }
            table.Write(Format.Minimal);
            output.Divider();
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

            guidId= matches.First().Id;
            output.Write($"Found single match for \"{Id}\": {guidId}{Environment.NewLine}");
            

            var eventLog = (await reader.GetEventLog(map, guidId).ConfigureAwait(false)).ToList();

            if (EventNumber <= -1)
            {
                output.Write($"\nEvent history for '{map.Aggregate.Name}' Id: {guidId}");
                output.Divider();
                var table = new ConsoleTable("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    var eventName = entry.Event + (entry.IsPublic ? "*" : "");

                    table.AddRow(counter++, entry.Aggregate, eventName, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                }
                table.Write(Format.Minimal);
                output.Divider();
                output.Write("* = Public Event");
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


