using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using OutputWriting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TypeMapping;

namespace MongoDbReading
{
    public class EventStoreReader
    {
        protected static readonly BsonDocument Blank_Filter = new BsonDocument();

        MongoClient _mongoClient;
        
        IOutputWriter Out { get; }

        readonly IMongoCollection<BsonDocument> _collection;

        public EventStoreReader(string server, int port, string database, IOutputWriter outputWriter)
        {
            Out = outputWriter;
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
                Out.DisplayError(ex.Message);
            }
        }

        public async Task<IEnumerable<EventSource>> GetUniqueEventSources(DolittleTypeMap map)
        {
            Out.Write($"Searching the eventlog for unique aggregates of {map.Aggregate.Name}...");

            // Dirty, but works :)
            var wrappedId = $"UUID(\"{map.Aggregate.Id}\")";
            var query = $@"{{ 'Aggregate.WasAppliedByAggregate' : true, 'Aggregate.TypeId' : {wrappedId} }}";
            var filter = BsonSerializer.Deserialize<BsonDocument>(query);

            var allDocuments = await _collection.Find(filter).ToListAsync();
            var completeList = new List<EventSource>();
            foreach (var document in allDocuments)
            {
                var dotNetObject = BsonTypeMapper.MapToDotNetValue(document);
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
            return completeList;
        }

        public async Task<IEnumerable<EventEntry>> GetEventLog(DolittleTypeMap map, Guid id)
        {
            Out.Write("Reading the eventlog...");

            var aggregateId = $"UUID(\"{map.Aggregate.Id}\")";
            var eventSourceId = $"UUID(\"{id}\")";
            var query = $@"{{ 'Aggregate.WasAppliedByAggregate' : true, 'Aggregate.TypeId' : {aggregateId}, 'Metadata.EventSource' : {eventSourceId} }}";
            var filter = BsonSerializer.Deserialize<BsonDocument>(query);


            var allDocuments = await _collection.Find(filter).ToListAsync();
            var completeList = new List<EventEntry>();
            foreach (var document in allDocuments)
            {                
                var eventTypeGuid = document["Metadata"]["TypeId"].AsGuid;
                var eventTypeId = map.Events.FirstOrDefault(e => e.Id.Contains(eventTypeGuid.ToString(), StringComparison.InvariantCultureIgnoreCase));
                               
                completeList.Add(new EventEntry
                {
                    Aggregate = map.Aggregate.Name,
                    Event     = eventTypeId.Name,
                    IsPublic  = document["Metadata"]["Public"].AsBoolean,
                    Time      = document["Metadata"]["Occurred"].ToUniversalTime(),
                    PayLoad   = document["Content"].ToJson()
                });                               
            }
            return completeList;
        }

        public bool ConnectionWorks()
        {
            if(_collection is null)
            {
                Out.DisplayError("Unable to find any records using the provided MongoDB settings");
                return false;
            }
            return true;
        }
    }
}