using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDbReading
{
    public class EventSource
    {
        public string Aggregate { get; set; }

        public Guid Id { get; set; }

        public int EventCount { get; set; }
    }
}
