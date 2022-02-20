using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OutputWriting;
using TypeMapping;

namespace MongoDbReading
{
    public class EventStoreReader
    {
        protected static readonly BsonDocument Blank_Filter = new();

        readonly MongoClient _mongoClient;       
        readonly IMongoCollection<BsonDocument> _collection;

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
                Ansi.Error(ex.Message);
            }
        }

        public async Task<IEnumerable<EventSource>> GetUniqueEventSources(DolittleTypeMap map)
        {
            Ansi.Info($"Scanning eventlog for events produced by {ColorAs.Value(map.Aggregate.Name)}...");

            // Dirty, but works :)
            var wrappedId = $"UUID(\"{map.Aggregate.Id}\")";
            var query = $"{{ 'Aggregate.WasAppliedByAggregate' : true, 'Aggregate.TypeId' : {wrappedId} }}";
            var filter = BsonSerializer.Deserialize<BsonDocument>(query);

            var allDocuments = await _collection.Find(filter).ToListAsync().ConfigureAwait(false);

            var completeList = new List<EventSource>();
            foreach (var document in allDocuments)
            {
                var dotNetObject = BsonTypeMapper.MapToDotNetValue(document);
                var eventSourceId = document["Metadata"]["EventSource"].AsString;
                var existing = completeList.Find(es => es.Id == eventSourceId.ToString());
                if (existing is { })
                {
                    existing.EventCount++;
                }
                else
                {
                    completeList.Add(new EventSource
                    {
                        Aggregate = map.Aggregate.Name,
                        Id = eventSourceId.ToString(),
                        EventCount = 1
                    });
                }
            }
            return completeList;
        }

        public async Task<IEnumerable<EventEntry>> GetEventLog(DolittleTypeMap map, string id)
        {
            Ansi.Info("Reading the eventlog...");

            var aggregateId = $"UUID(\"{map.Aggregate.Id}\")";
            var eventSourceId = $"\"{id}\"";
            var query = $"{{ 'Aggregate.WasAppliedByAggregate' : true, 'Aggregate.TypeId' : {aggregateId}, 'Metadata.EventSource' : {eventSourceId} }}";
            var filter = BsonSerializer.Deserialize<BsonDocument>(query);

            var allDocuments = await _collection.Find(filter).ToListAsync().ConfigureAwait(false);

            var completeList = new List<EventEntry>();
            foreach (var document in allDocuments)
            {
                var eventTypeGuid = document["Metadata"]["TypeId"].AsGuid;
                var eventTypeId = map.Events.Find(e => e.Id.Contains(eventTypeGuid.ToString(), StringComparison.InvariantCultureIgnoreCase));

                completeList.Add(new EventEntry
                {
                    Aggregate = map.Aggregate.Name,
                    Event = eventTypeId?.Name ?? "Unknown type",
                    IsPublic = document["Metadata"]["Public"].AsBoolean,
                    Time = document["Metadata"]["Occurred"].ToUniversalTime(),
                    PayLoad = document["Content"].ToJson()
                });
            }
            return completeList;
        }

        public bool ConnectionWorks()
        {
            if (_collection is null)
            {
                Ansi.Error("Unable to find any records using the provided MongoDB settings");
                return false;
            }
            return true;
        }
    }
}
