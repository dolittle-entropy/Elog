using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using TypeMapping;

namespace MongoDbReading
{
    public class EventStoreWatcher
    {
        private readonly ElogConfiguration _config;

        public event EventHandler<DolittleEventFired> OnDolittleEventFired;

        public EventStoreWatcher(ElogConfiguration config)
        {
            _config = config;
        }

        public Task StartAsync(DolittleTypeMap typeMap)
        {
            return Task.Run(async () => {
                var client = CreateMongoClient();
                var database = client.GetDatabase(_config.MongoConfig.MongoDB);
                var collectionToWatch = database.GetCollection<BsonDocument>("event-log");

                using var cursor = collectionToWatch.Watch();

                // Sit tight until something happens
                while(cursor.MoveNext() && cursor.Current.Count() == 0)
                {
                    await Task.Yield();
                }

                do
                {
                    foreach(var change in cursor.Current.AsEnumerable())
                    {
                        try
                        {
                            var document = BsonSerializer.Deserialize<BsonDocument>(change.FullDocument);
                            if(document["Aggregate"]["WasAppliedByAggregate"].AsBoolean)
                            {
                                var eventOffset = document["_id"].AsInt64;
                                var aggregateId = document["Aggregate"]["TypeId"].AsGuid;
                                var eventTypeId = document["Metadata"]["TypeId"].AsGuid;
                                if (aggregateId != typeMap.Aggregate.Id)
                                    continue;

                                var mathcingEvent = typeMap.Events.FirstOrDefault(evt => evt.Id.Equals(eventTypeId.ToString()));
                                if (mathcingEvent is null)
                                    continue;

                                var dolittleEventFired = new DolittleEventFired
                                {
                                    EventLogOffset = eventOffset,
                                    EventStore = _config.MongoConfig.MongoDB,
                                    Aggregate = typeMap.Aggregate.Name,
                                    EventName = mathcingEvent.Name,
                                    Occurred = document["Metadata"]["Occurred"].ToLocalTime(),
                                    Detected = DateTime.Now,
                                };
                                OnDolittleEventFired?.Invoke(this, dolittleEventFired);
                            }
                        }
                        catch
                        {
                            /* See if Putin cares */
                        }
                    }
                } while(cursor.MoveNext());
            });
        }

        private IMongoClient CreateMongoClient()
        {
            var clientSettings = new MongoClientSettings
            {
                Server = new MongoServerAddress(_config.MongoConfig.MongoServer, _config.MongoConfig.Port),
                ConnectTimeout = TimeSpan.FromSeconds(3),
                ServerSelectionTimeout = TimeSpan.FromSeconds(3),
            };
            return new MongoClient(clientSettings);
        }
    }
}
