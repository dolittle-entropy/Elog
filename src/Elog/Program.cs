
using AssemblyReading;
using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Elog
{
    [Command(Name = "elog", Description = "Displays an eventlog for a specified aggregate")]
    [Subcommand(typeof(Configure))]
    public class Program
    {
        private ElogConfiguration _config;

        public static Task Main(string[] args)
        {            
            return CommandLineApplication.ExecuteAsync<Program>(args);
        }

        public async Task OnExecute(CommandLineApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            _config = LoadConfiguration();
            if(_config == null)
            {
                return;
            }
            var assemblyReader = new AssemblyReader(_config.BinariesPath);

            if(AggregateName.Length > 0)
            {

                var map = assemblyReader.GenerateMapForAggregate(AggregateName);

                if(Id != Guid.Empty)
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
            if(!File.Exists(configurationFile))
            {
                Console.WriteLine("No configuration found. Run 'elog.exe configure' to create a configuration");
                return null;
            }
            var fileContents = File.ReadAllText(configurationFile);
            var configurations = JsonConvert.DeserializeObject<List<ElogConfiguration>>(fileContents);
            ElogConfiguration configuration = null;
            if(Configuration.Length == 0)
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
            Console.WriteLine($"Aggregate Name not provided. Listing all {aggregates.Count()} identified Aggregates");
            var table = new ConsoleTable("Aggregate", "Id");            
            foreach (var entry in aggregates)
            {
                table.AddRow(entry.Name, entry.Id);
            };
            table.Write(Format.Minimal);
        }

        private async Task ListEventsForAggregate(TypeMapping.DolittleTypeMap map)
        {
            Console.WriteLine($"Aggregate Id not provided. Listing all unique Identities for {map.Aggregate.Name}");
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB);

            var uniqueEventSources = await reader.GetUniqueEventSources(map);
            var table = new ConsoleTable("Aggregate", "Id", "Events");
            foreach(var uniqueEventSource in uniqueEventSources)
            {
                table.AddRow(uniqueEventSource.Aggregate, uniqueEventSource.Id, uniqueEventSource.EventCount);
            };
            table.Write(Format.Minimal);
        }

        private async Task ListUniqueIdentifiers(TypeMapping.DolittleTypeMap map)
        {
            var reader = new EventStoreReader(
                _config.MongoConfig.MongoServer,
                _config.MongoConfig.Port,
                _config.MongoConfig.MongoDB);

            var eventLog = await reader.GetEventLog(map, Id);

            if(EventNumber == -1)
            {
                Console.WriteLine($"Displaying event history for {map.Aggregate.Name} with Id:{Id}");
                Console.WriteLine(new String('-', 80));
                var table = new ConsoleTable("No.", "Aggregate", "Event", "Time");
                var counter = 0;
                foreach (var entry in eventLog)
                {
                    table.AddRow(counter++, entry.Aggregate, entry.Event, entry.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff"));
                };
                table.Write(Format.Minimal);
            }
            else
            {
                var rightEvent = eventLog.ToList()[EventNumber];
                var json = JsonConvert.DeserializeObject(rightEvent.PayLoad);
                Console.WriteLine($"Displaying payload for event #{EventNumber} for {map.Aggregate.Name}:{Id}");
                Console.WriteLine(new String('-', 80));
                Console.WriteLine($"Event {EventNumber} is {rightEvent.Event} performed on {rightEvent.Time.ToString("dddd dd.MMMyyyy HH:mm:ss.ffff")}");
                Console.WriteLine(json);
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


