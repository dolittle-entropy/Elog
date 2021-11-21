using Dolittle.SDK.Aggregates;
using Dolittle.SDK.Events;
using System;
using System.Reflection;

namespace TypeMapping
{
    public static class TypeExtensions
    {
        public static DolittleAggregate AsDolittleAggregate(this Type type, Type aggregateRootType)
        {
            if (!type.IsClass)
            {
                return null;
            }

            if (!aggregateRootType.IsAssignableFrom(type))
            {
                return null;
            }

            var attribute = (AggregateRootAttribute) type.GetCustomAttribute<AggregateRootAttribute>();

            if (attribute is AggregateRootAttribute attr)
            {
                try
                {
                    return new DolittleAggregate
                    {                    
                        Id = attr.Type.Id,
                        Name = type.Name
                    };
                }
                catch
                {

                }

            }
            return null;
        }

        public static DolittleEvent AsDolittleEvent(this Type type)
        {
            if (!type.IsClass)
            {
                return null;
            }

            try
            {
                var attribute = type.GetCustomAttribute<EventTypeAttribute>();
                if (attribute is { })
                {
                    return new DolittleEvent
                    {
                        Id = attribute.Identifier.ToString(),
                        Name = type.Name
                    };
                }
            }
            catch { }
            return null;
        }
    }
}
