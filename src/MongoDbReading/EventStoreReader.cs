using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OutputWriting;
using Spectre.Console;
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
                Out.Error(ex.Message);
            }
        }

        public async Task<IEnumerable<EventSource>> GetUniqueEventSources(DolittleTypeMap map)
        {
            Out.Info($"Scanning eventlog for events produced by {ColorAs.Value(map.Aggregate.Name)}...");

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

        public async Task<DolittleEventUsage?> GetEventTypeUsage([NotNull] DolittleEvent dolittleEvent, List<DolittleAggregate> map)
        {

            var wrappedId = $"UUID(\"{dolittleEvent.Id}\")";
            var query = $"{{ 'Aggregate.WasAppliedByAggregate' : true, 'Metadata.TypeId' : {wrappedId} }}";
            var filter = BsonSerializer.Deserialize<BsonDocument>(query);
            var result = new DolittleEventUsage
            {
                DolittleEvent = dolittleEvent,
                AggregateUsages = new Dictionary<string, DolittleAggregateUsage>()
            };

            await AnsiConsole.Progress()
                .HideCompleted(true)
                .AutoClear(true)
                .AutoRefresh(true)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(Spinner.Known.Dots5)
                })
                .StartAsync(async ctx => 
                {
                    var task1 = ctx.AddTask("Reading through the eventstore...");
                    var task2 = ctx.AddTask("Counting and sorting Events......");

                    var allDocuments = await _collection.Find(filter).ToListAsync().ConfigureAwait(false);

                    task1.Increment(100.0);
                    var stepSize = 100.0 / allDocuments.Count;

                    for (int i = 0; i < allDocuments.Count; i++)
                    {
                        task2.Increment(stepSize);                        

                        // Count it
                        result.InvocationCount += 1;

                        // Set first time found
                        if (result.FirstOffset == 0)
                            result.FirstOffset = (long) allDocuments[i]["_id"].AsDecimal128;

                        // Set the last seen offset
                        result.LastOffset = (long) allDocuments[i]["_id"].AsDecimal128;

                        var aggregateId = allDocuments[i]["Aggregate"]["TypeId"].AsGuid.ToString();

                        // Ensure we have this aggregate once
                        if (!result.AggregateUsages.ContainsKey(aggregateId))
                        {
                            result.AggregateUsages.Add(aggregateId, new DolittleAggregateUsage
                            {
                                Aggregate = map.First(m => m.Id.ToString() == aggregateId),
                            });
                        }
                        result.AggregateUsages[aggregateId].InvocationCount += 1;
                    }
                    
                });

            return result;

        }

        public async Task<IEnumerable<EventEntry>> GetEventLog(DolittleTypeMap map, string id)
        {
            Out.Info("Reading the eventlog...");

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
                Out.Error("Unable to find any records using the provided MongoDB settings");
                return false;
            }
            return true;
        }
    }
}
