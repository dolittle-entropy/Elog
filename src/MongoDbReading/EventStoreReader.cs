using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using TypeMapping;

namespace MongoDbReading
{
    public class EventStoreReader
    {
        protected static readonly BsonDocument Blank_Filter = new BsonDocument();

        readonly IMongoCollection<BsonDocument> _collection;
        MongoClient _mongoClient;

        public EventStoreReader(string server, int port, string database)
        {
            var mongoServerAddress = new MongoServerAddress(server, port);
            var settings = new MongoClientSettings
            {
                Server = mongoServerAddress,
                ServerSelectionTimeout = TimeSpan.FromSeconds(5),                
                ConnectTimeout = TimeSpan.FromSeconds(5),                   
                GuidRepresentation = GuidRepresentation.Standard
            };
            try
            {
                _mongoClient = new MongoClient(settings);
                var mongoDdatabase = _mongoClient.GetDatabase(database);
                _collection = mongoDdatabase.GetCollection<BsonDocument>("event-log");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task<IEnumerable<EventSource>> GetUniqueEventSources(DolittleTypeMap map)
        {
            Console.WriteLine("Reading eventlog...");
            var allDocuments = await _collection.Find(Blank_Filter).ToListAsync();
            var completeList = new List<EventSource>();
            foreach (var document in allDocuments)
            {
                var dotNetObject = BsonTypeMapper.MapToDotNetValue(document);
                var wasFromAggregate = document["Aggregate"]["WasAppliedByAggregate"].AsBoolean;
                var aggregateId = document["Aggregate"]["TypeId"].AsGuid;

                if (wasFromAggregate && aggregateId == map.Aggregate.Id)
                {
                    var eventSourceId = document["Metadata"]["EventSource"].AsGuid;
                    var existing = completeList.FirstOrDefault(es => es.Id == eventSourceId);
                    if (existing is { })
                    {
                        existing.EventCount += 1;
                    }
                    else
                    {
                        completeList.Add(new EventSource
                        {
                            Aggregate = map.Aggregate.Name,
                            Id = eventSourceId,
                            EventCount = 1
                        });
                    }
                }
            }
            return completeList;
        }

        public async Task<IEnumerable<EventEntry>> GetEventLog(DolittleTypeMap map, Guid id)
        {
            Console.WriteLine("Reading eventlog...");
            var allDocuments = await _collection.Find(Blank_Filter).ToListAsync();
            var completeList = new List<EventEntry>();
            foreach (var document in allDocuments)
            {
                var dotNetObject = BsonTypeMapper.MapToDotNetValue(document);
                var wasFromAggregate = document["Aggregate"]["WasAppliedByAggregate"].AsBoolean;
                var aggregateId = document["Aggregate"]["TypeId"].AsGuid;

                if (wasFromAggregate && aggregateId == map.Aggregate.Id)
                {
                    var eventSourceId = document["Metadata"]["EventSource"].AsGuid;                    
                    var eventTypeGuid = document["Metadata"]["TypeId"].AsGuid;
                    var eventTypeId = map.Events.FirstOrDefault(e => e.Id.Contains(eventTypeGuid.ToString(), StringComparison.InvariantCultureIgnoreCase));

                    if(eventSourceId == id)
                    {
                        completeList.Add(new EventEntry
                        {
                            Aggregate = map.Aggregate.Name,
                            Event     = eventTypeId.Name,
                            Time      = document["Metadata"]["Occurred"].ToUniversalTime(),
                            PayLoad   = document["Content"].ToJson()
                        });
                    }
                }
            }
            return completeList;
        }

        public bool ConnectionWorks()
        {
            if(_collection is null)
            {
                Console.WriteLine("Unable to find any records using the provided MongoDB settings");
                return false;
            }
            return true;
        }
    }
}