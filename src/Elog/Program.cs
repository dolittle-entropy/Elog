
using AssemblyReading;
using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Elog
{
    [Command(Name = "elog", Description = "Lets you explore aggregates and their eventlogs")]
    [Subcommand(typeof(Configure))]
    public class Program
    {
        private static string DIVIDER_STRING = new string('-', 80);

        private ElogConfiguration _config;

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
            var assemblyReader = new AssemblyReader(_config.BinariesPath);

            if (AggregateName.Length > 0)
            {

                var map = assemblyReader.GenerateMapForAggregate(AggregateName);

                if (Id != Guid.Empty)
                {
                    await ListUniqueIdentifiers(map);
                }
                else
                {
                    await ListEventsForAggregate(map);
                }
            }
            else
            {
                var aggregates = assemblyReader.GetAllAggregates();
                DisplayAggregateList(aggregates);
            }
            Console.WriteLine($"Program finished in {stopwatch.ElapsedMilliseconds:### ###.0}ms");
        }

        private ElogConfiguration LoadConfiguration()
        {
            var configurationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configurationFile = Path.Combine(configurationFolder, ElogConfiguration.ConfigurationFileName);
            if (!File.Exists(configurationFile))
            {
                Console.WriteLine("ERROR: No configuration found! Run 'elog.exe configure' to create a configuration.\nA wizard will guide you through the values required");
                return null;
            }
            var fileContents = File.ReadAllText(configurationFile);
            var configurations = JsonConvert.DeserializeObject<List<ElogConfiguration>>(fileContents);
            ElogConfiguration configuration = null;
            if (Configuration.Length == 0)
            {
                configuration = configurations.First();
            }
            else
            {
                configuration = configurations.First(c => c.Name.Equals(Configuration, StringComparison.InvariantCultureIgnoreCase));
            }
            Console.WriteLine($@"
Configuration loaded: {configuration.Name}
Mongo Server        : {configuration.MongoConfig.MongoServer}:{configuration.MongoConfig.Port}
Mongo Database      : {configuration.MongoConfig.MongoDB}
Binaries Path       : {configuration.BinariesPath}
");
            return configuration;
        }

        private void DisplayAggregateList(IEnumerable<TypeMapping.DolittleAggregate> aggregates)
        {
            var table = new ConsoleTable("Aggregate", "Id");
            foreach (var entry in aggregates)
            {
                table.AddRow(entry.Name, entry.Id);
            };
            table.Write(Format.Minimal);
            Divider();
            Console.WriteLine($"{aggregates.Count()} identified Aggregates. Add '-a <aggregatename>' to see business entities\n");
        }

        private void Divider()
        {
            Console.WriteLine(DIVIDER_STRING);
        }

        private async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB);

            var uniqueEventSources = await reader.GetUniqueEventSources(map);
            var table = new ConsoleTable("Aggregate", "Id", "Events");
            Divider();
            foreach (var uniqueEventSource in uniqueEventSources)
            {
                table.AddRow(uniqueEventSource.Aggregate, uniqueEventSource.Id, uniqueEventSource.EventCount);
            };
            table.Write(Format.Minimal);
            Divider();
            Console.WriteLine($"{uniqueEventSources.Count()} unique Identities found for {map.Aggregate.Name}. \nAdd '-id <id>' to see their event log.\n");
        }

        private async Task ListUniqueIdentifiers(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB);

            var eventLog = (await reader.GetEventLog(map, Id)).ToList();

            if (EventNumber <= -1)
            {
                Console.WriteLine($"\nEvent history for '{map.Aggregate.Name}' Id:{Id}");
                Divider();
                var table = new ConsoleTable("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    table.AddRow(counter++, entry.Aggregate, entry.Event, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                };
                table.Write(Format.Minimal);
                Divider();
                Console.WriteLine("Add '-evt <#>' to see the payload details of that event\n");
            }
            else
            {
                if(EventNumber >= eventLog.Count)
                {
                    Console.WriteLine($"ERROR: The Event number values for this Event Source range from 0 t0 {eventLog.Count - 1} only.\n");
                    return;
                }
                var rightEvent = eventLog.ToList()[EventNumber];
                var json = JsonConvert.DeserializeObject(rightEvent.PayLoad);
                Console.WriteLine($"Displaying Payload #{EventNumber} for aggregate {map.Aggregate.Name}:{Id}");
                Console.WriteLine($"Event {EventNumber}: '{rightEvent.Event}' on {rightEvent.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff")}");
                Divider();
                Console.WriteLine(json);
                Divider();
            }
        }

        [Option(Description = "Name of the aggregate to inspect, i.e. 'Product'")]
        public string AggregateName { get; set; } = String.Empty;

        [Option(Description = "Identity of the aggregate for which you want to see the event log", ShortName = "id")]
        public Guid Id { get; set; } = Guid.Empty;

        [Option(Description = "Display the payload of the event# ", ShortName = "evt")]
        public int EventNumber { get; set; } = -1;

        [Option(Description = "Name of configuration to load. Will load the first configuration if left blank")]
        public string Configuration { get; set; } = string.Empty;
    }
}


