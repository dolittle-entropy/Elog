using OutputWriting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TypeMapping;

namespace AssemblyReading
{
    public class AssemblyReader
    {
        static readonly string[] skipDllsWith = new[]
        {
            "Microsoft.",
            "DnsClient.",
            "Serilog.",
            "Newtonsoft.",
            "SwashBuckle.",
            "System.",
            "HotChocolate.",
            "GraphQL.",
            "Grpc.",
            "Dolittle.",
            "Google.",
            "AutoFac.",
            "MongoDB.",
            "Polly.",
            "AutofacSerilogIntegration.",
            "SharpCompress.",
            "libwkhtmltox."
        };
        readonly string _assemblyFolder;

        IOutputWriter Out { get; }

        public AssemblyReader(string dolittleAssemblyFolder, IOutputWriter outputWriter)
        {
            _assemblyFolder = dolittleAssemblyFolder;
            Out = outputWriter;
        }

        public DolittleTypeMap GenerateMapForAggregate(string aggregateName)
        {
            var aggregateRootType = FindAndIdentifyAggregateRootType();
            if (aggregateRootType is null)
            {
                Out.DisplayError("Unable to find aggregate root type, must abort");
                return null;
            }
            var dllFiles = LoadDllFiles();
            var typeMap = new DolittleTypeMap();

            foreach (var dllFile in dllFiles)
            {
                Type[] types;
                try
                {
                    types = Assembly.LoadFrom(dllFile).GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type.AsDolittleAggregate(aggregateRootType) is { } dolittleAggregate)
                    {
                        if (dolittleAggregate.Name.Equals(aggregateName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            typeMap.Aggregate = dolittleAggregate;
                        }
                    }
                    else if (type.AsDolittleEvent() is { } dolittleEvent)
                    {
                        typeMap.Events.Add(dolittleEvent);
                    }
                }
            }
            if (typeMap.Aggregate is { })
            {
                Out.Write($"Aggregate '{typeMap.Aggregate.Name}', {typeMap.Aggregate.Id}.\nEventTypes found in binaries folder: {typeMap.Events.Count}");
            }
            return typeMap;
        }

        public IEnumerable<DolittleAggregate> GetAllAggregates()
        {
            var aggregateRootType = FindAndIdentifyAggregateRootType();
            if (aggregateRootType is null)
            {
                Out.DisplayError("Unable to find aggregate root type, must abort");
                return null;
            }

            var finalList = new List<DolittleAggregate>();
            var dllFiles = LoadDllFiles();

            foreach (var dllFile in dllFiles)
            {
                Type[] types;
                try
                {
                    types = Assembly.LoadFrom(dllFile).GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type.AsDolittleAggregate(aggregateRootType) is { } dolittleAggregate)
                    {
                        finalList.Add(dolittleAggregate);
                    }
                }
            }
            return finalList;
        }

        List<string> LoadDllFiles()
        {
            var dllFiles = Directory.GetFiles(_assemblyFolder, "*.dll");
            var finalList = new List<string>();

            foreach (var dllFile in dllFiles)
            {
                if (!skipDllsWith.Any(name => dllFile.Contains(name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    finalList.Add(dllFile);
                }
            }
            return finalList;
        }

        private Type FindAndIdentifyAggregateRootType()
        {
            const string ExpectedAssemblyName = "Dolittle.SDK.Aggregates.dll";

            var assembly = Assembly.LoadFrom(Path.Combine(_assemblyFolder, ExpectedAssemblyName));
            if (assembly is { })
            {
                return Array.Find(assembly.GetTypes(), t => t.Name.Equals("AggregateRoot"));
            }
            return null;
        }
    }
}