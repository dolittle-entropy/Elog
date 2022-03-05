using System;

namespace MongoDbReading
{
    public class EventEntry
    {
        public int Counter { get; set; }
        public long Offset { get; set; }
        public string Aggregate { get; set; }
        public string Event { get; set; }
        public DateTime Time { get; set; }
        public string PayLoad { get; set; }
        public bool IsPublic { get; set; }
    }
}
