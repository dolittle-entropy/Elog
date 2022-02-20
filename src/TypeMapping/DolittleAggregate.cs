using System;

namespace TypeMapping
{
    public record DolittleAggregate
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public const string AttributeName = "AggregateRootAttribute";
    }
}
