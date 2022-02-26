using System.Collections.Generic;
using TypeMapping;

namespace MongoDbReading
{
    public record DolittleAggregateUsage
    {
        public DolittleAggregate Aggregate { get; set; }

        public long InvocationCount { get; set; }
    }

    public record DolittleEventUsage
    {
        public DolittleEvent? DolittleEvent { get; set; }

        public long FirstOffset { get; set; }

        public long LastOffset { get; set; }

        public long InvocationCount { get; set; }

        public Dictionary<string, DolittleAggregateUsage> AggregateUsages { get; set; }
    }
}
