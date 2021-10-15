
using AssemblyReading;
using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Elog
{
    [Command(Name = "elog", Description = "Displays an eventlog for a specified aggregate")]
    public class Program
    {
        public static void Main(string[] args)
        {            
            CommandLineApplication.Execute<Program>(args);
        }

        public async Task OnExecute(CommandLineApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            var assemblyReader = new AssemblyReader(DolittleFolder);

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
            var reader = new EventStoreReader(MongoDb, Port, Database);

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

            var reader = new EventStoreReader(MongoDb, Port, Database);
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

        [Option(Description = "Mongo Database that contains the event log", ShortName = "db"), Required]
        public string Database { get; set; }

        [Option(Description = "Name of the aggregate to inspect, i.e. 'Product'")]
        public string AggregateName { get; set; } = String.Empty;

        [Option(Description = "Identity of the aggregate for which you want to see the event log", ShortName = "id")]
        public Guid Id { get; set; } = Guid.Empty;

        [Option(Description = "Display the payload of the event# ", ShortName = "evt")]
        public int EventNumber { get; set; } = -1;

        [Option(Description = "Path to the folder containing the dolittle application")]
        [DirectoryExists]
        public string DolittleFolder { get; set; }

        [Option(Description = "MongoDB server instance, name or IP", ShortName = "s")]
        public string MongoDb { get; set; } = "localhost";

        [Option(Description = "MongoDb Port", LongName = "port")]
        public int Port { get; set; } = 27017;
    }
}


