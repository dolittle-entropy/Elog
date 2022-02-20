using System;

namespace MongoDbReading
{
    public class DolittleEventFired
    {
        public long EventLogOffset { get; set; }
        public string Aggregate { get; set; }
        public string EventName { get; set; }
        public DateTime Detected { get; set; }
        public string EventStore { get; set; }
        public DateTime Occurred { get; set; }
    }
}
