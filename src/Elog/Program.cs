﻿
using AssemblyReading;
using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Elog
{
    [Command(Name = "elog", Description = "Lets you explore aggregates and their eventlogs")]
    [Subcommand(typeof(Configure))]
    public class Program
    {
        static readonly IOutputWriter Out = new ConsoleOutputWriter();

        ElogConfiguration _config;

        public static Task Main(string[] args)
        {
            return CommandLineApplication.ExecuteAsync<Program>(args);
        }

        public async Task OnExecute(CommandLineApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            _config = LoadConfiguration();
            if (_config == null)
            {
                return;
            }
            var assemblyReader = new AssemblyReader(_config.BinariesPath, Out);

            if (AggregateName.Length > 0) // We are looking for a specific Aggregate
            {
                var map = assemblyReader.GenerateMapForAggregate(AggregateName);

                // code-review: add .ConfigureAwait(false) to these awaits?
                if (Id != Guid.Empty) // We didn't provide an EventSource Id, so we list all unique aggregates
                {
                    await ListUniqueIdentifiers(map);
                }
                else // We have an event source ID, so we want to display the event log
                {
                    await ListEventsForAggregate(map);
                }
            }
            else // We didn't supply an aggregate name, so we just list all of them
            {
                var aggregates = assemblyReader.GetAllAggregates();
                DisplayAggregateList(aggregates);
            }
            Out.Write($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
        }

        [Option(Description = "Name of the aggregate to inspect, i.e. 'Product'")]
        public string AggregateName { get; set; } = string.Empty;

        [Option(Description = "Identity of the aggregate for which you want to see the event log", ShortName = "id")]
        public Guid Id { get; set; } = Guid.Empty;

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
                Out.DisplayError("No configuration found! Run 'elog.exe configure' to create a configuration.\nA wizard will guide you through the values required");
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
                    Out.DisplayError($"The configuration '{Configuration}' was not found. Aborting.");
                    return null;
                }
            }
            Out.Write($@"
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
            Out.Divider();
            Out.Write($"{aggregates.Count()} identified Aggregates. Add '-a <aggregatename>' to see business entities\n");
        }

        async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB,
                Out);

            // code-review: add .ConfigureAwait(false) here?
            var uniqueEventSources = await reader.GetUniqueEventSources(map);

            var table = new ConsoleTable("Aggregate", "Id", "Events");
            Out.Divider();
            foreach (var uniqueEventSource in uniqueEventSources)
            {
                table.AddRow(uniqueEventSource.Aggregate, uniqueEventSource.Id, uniqueEventSource.EventCount);
            }
            table.Write(Format.Minimal);
            Out.Divider();
            Out.Write($"{uniqueEventSources.Count()} unique Identities found for {map.Aggregate.Name}. \nAdd '-id <id>' to see their event log.\n");
        }

        async Task ListUniqueIdentifiers(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB,
                Out);

            // code-review: add .ConfigureAwait(false) here?
            var eventLog = (await reader.GetEventLog(map, Id)).ToList();

            if (EventNumber <= -1)
            {
                Out.Write($"\nEvent history for '{map.Aggregate.Name}' Id:{Id}");
                Out.Divider();
                var table = new ConsoleTable("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    var eventName = entry.Event + (entry.IsPublic ? "*" : "");

                    table.AddRow(counter++, entry.Aggregate, eventName, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                }
                table.Write(Format.Minimal);
                Out.Divider();
                Out.Write("* = Public Event");
                Out.Write("Add '-evt <#>' to see the payload details of that event\n");
            }
            else
            {
                if (EventNumber >= eventLog.Count)
                {
                    Out.DisplayError($"The Event number values for this Event Source range from 0 t0 {eventLog.Count - 1} only.\n");
                    return;
                }
                var rightEvent = eventLog.ToList()[EventNumber];
                var json = JsonConvert.DeserializeObject(rightEvent.PayLoad);
                Out.Write($"Displaying Payload #{EventNumber} for aggregate {map.Aggregate.Name}:{Id}");
                Out.Write($"Event {EventNumber}: '{rightEvent.Event}' on {rightEvent.Time:dddd dd.MMMyyyy HH:mm:ss.ffff}");
                Out.Divider();
                Out.Write(json?.ToString() ?? "");
                Out.Divider();
            }
        }
    }
}


