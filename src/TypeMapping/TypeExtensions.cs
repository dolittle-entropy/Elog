using Dolittle.SDK.Aggregates;
using Dolittle.SDK.Events;
using System.Reflection;


namespace TypeMapping
{
    public static class TypeExtensions
    {
        public static DolittleAggregate AsDolittleAggregate(this Type type, Type aggregateRootType)
        {

            if (!type.IsClass )
            {
                return null;
            }

            var typeName = type.FullName;
            if(!aggregateRootType.IsAssignableFrom(type))
            {
                return null;
            }

            var attribute = type.GetCustomAttribute<AggregateRootAttribute>();
            
            if(attribute is { })
            {
                return new DolittleAggregate
                {
                    Id   = attribute.Id,
                    Name = type.Name
                };
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
                        Id = attribute.EventType.ToString(),
                        Name = type.Name
                    };
                }
            }
            catch { }
            return null;
        }
    }
}
