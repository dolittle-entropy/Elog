using System;

namespace MongoDbReading
{
    public class EventSource
    {
        public string Aggregate { get; set; }

        public Guid Id { get; set; }

        public int EventCount { get; set; }
    }
}
