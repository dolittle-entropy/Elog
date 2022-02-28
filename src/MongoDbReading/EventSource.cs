using System;

namespace MongoDbReading
{
    public class EventSource
    {
        public string Aggregate { get; set; }

        public string Id { get; set; }

        public int EventCount { get; set; }

        public DateTime LastOccurred { get; set; }
        public long LastOffset { get; set; }
    }
}
