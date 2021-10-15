using System.Reflection;
using TypeMapping;

namespace AssemblyReading
{
    public class AssemblyReader
    {
        readonly string _assemblyFolder;

        public AssemblyReader(string dolittleAssemblyFolder)
        {
            _assemblyFolder = dolittleAssemblyFolder;
        }

        public DolittleTypeMap GenerateMapForAggregate(string aggregateName)
        {
            Console.WriteLine("Reading assemblies...");

            var aggregateRootType = FindAndIdentifyAggregateRootType();
            if(aggregateRootType is null)
            {
                Console.WriteLine("Unable to find aggregate root type, must abort");
                return null;
            }
            var dllFiles = Directory.GetFiles(_assemblyFolder, "*.dll");
            var typeMap = new DolittleTypeMap();

            var skipDllsWith = new[] { "Microsoft.", "DnsClient.", "Serilog.", "Newtonsoft.", "SwashBuckle.", "System.", "HotChocolate.", "GraphQL.", "Grpc.", "Dolittle.", "Google.", "AutoFac.", "MongoDB.", "Polly.", "AutofacSerilogIntegration.", "SharpCompress.",  };

            foreach(var dllFile in dllFiles)
            {
                if(skipDllsWith.Any(name => dllFile.Contains(name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    continue;
                }
                var assembly = Assembly.LoadFrom(dllFile);
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach(var type in types)
                {
                    var typeName = type.FullName;
                        
                    if(type.AsDolittleAggregate(aggregateRootType) is { } dolittleAggregate)
                    {
                        if(dolittleAggregate.Name.Equals(aggregateName, StringComparison.InvariantCultureIgnoreCase))
                        {                            
                            typeMap.Aggregate = dolittleAggregate;
                        }
                    }
                    else if(type.AsDolittleEvent() is { } dolittleEvent)
                    {                        
                        typeMap.Events.Add(dolittleEvent);
                    }
                }            
            }
            if(typeMap.Aggregate is { })
            {
                Console.WriteLine($"Aggregate '{typeMap.Aggregate.Name}' found. \nThe solution contains a total of {typeMap.Events.Count} distinct events");
            }
            return typeMap;
        }

        public IEnumerable<DolittleAggregate> GetAllAggregates()
        {
            var aggregateRootType = FindAndIdentifyAggregateRootType();
            var finalList = new List<DolittleAggregate>();
            if (aggregateRootType is null)
            {
                Console.WriteLine("Unable to find aggregate root type, must abort");
                return null;
            }
            var dllFiles = Directory.GetFiles(_assemblyFolder, "*.dll");
            var typeMap = new DolittleTypeMap();

            var skipDllsWith = new[] { "Microsoft.", "DnsClient.", "Serilog.", "Newtonsoft.", "SwashBuckle.", "System.", "HotChocolate.", "GraphQL.", "Grpc.", "Dolittle.", "Google.", "AutoFac.", "MongoDB.", "Polly.", "AutofacSerilogIntegration.", "SharpCompress.", };

            foreach (var dllFile in dllFiles)
            {
                if (skipDllsWith.Any(name => dllFile.Contains(name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    continue;
                }
                var assembly = Assembly.LoadFrom(dllFile);
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    var typeName = type.FullName;

                    if (type.AsDolittleAggregate(aggregateRootType) is { } dolittleAggregate)
                    {
                        finalList.Add(dolittleAggregate);
                    }
                }
            }
            return finalList;
        }

        private Type FindAndIdentifyAggregateRootType()
        {
            const string expectedAssemblyName = "Dolittle.SDK.Aggregates.dll";
            var assembly = Assembly.LoadFrom(Path.Combine(_assemblyFolder, expectedAssemblyName));
            if(assembly is { })
            {
                return assembly.GetTypes().FirstOrDefault(t => t.Name.Equals("AggregateRoot"));
            }
            return null;
        }
    }
}