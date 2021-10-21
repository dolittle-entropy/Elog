using System.Collections.Generic;

namespace TypeMapping
{
    public class DolittleTypeMap
    {
        public DolittleAggregate Aggregate { get; set; }

        public List<DolittleEvent> Events { get; set; } = new List<DolittleEvent>();
    }
}