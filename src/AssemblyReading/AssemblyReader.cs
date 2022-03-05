using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using OutputWriting;
using Spectre.Console;
using TypeMapping;

namespace AssemblyReading
{
    public class AssemblyReader : IDisposable
    {
        private const string AggregateRootName = "AggregateRoot";
        static readonly string[] skipDllsWith = new[]
        {
            "Microsoft.",
            "Azure",
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
            "libwkhtmltox.",
            "Lamar",
            "AutoMapper",
            "MediatR",
            "Confluent",
            "StackExchange.Redis",
            "BaselineTypeDiscovery",
            "GreenDonut",
            "Pipelines",
            "FluentValidation",
        };
        readonly string _assemblyFolder;
        readonly MetadataLoadContext _metadataContext;

        readonly List<DolittleAggregate> _aggregateList;
        readonly List<DolittleEvent> _eventList;
        readonly List<DolittleProjection> _projectionList;

        public AssemblyReader(string dolittleAssemblyFolder)
        {
            const string EventsAssemblyName = "Dolittle.SDK.Events.dll";
            const string ExpectedAssemblyName = "Dolittle.SDK.Aggregates.dll";
            const string ProjectionsAssemblyName = "Dolittle.SDK.Projections.dll";
            const string EventHandlingAssemblyName = "Dolittle.SDK.Events.Handling.dll";

            _assemblyFolder = dolittleAssemblyFolder;

            var aggregateRootPath = Path.Combine(_assemblyFolder, ExpectedAssemblyName);
            var eventTypePath = Path.Combine(_assemblyFolder, EventsAssemblyName);
            var eventHandlingTypePath = Path.Combine(_assemblyFolder, EventHandlingAssemblyName);
            var projectionsTypePath = Path.Combine(_assemblyFolder, ProjectionsAssemblyName);

            if (!File.Exists(aggregateRootPath))
                throw new FileNotFoundException(nameof(aggregateRootPath));

            var resolver = new PathAssemblyResolver(new List<string>(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
                    {
                        eventTypePath,
                        aggregateRootPath,
                        projectionsTypePath,
                        eventHandlingTypePath,
                    });

            _eventList = new List<DolittleEvent>();
            _aggregateList = new List<DolittleAggregate>();
            _projectionList = new List<DolittleProjection>();
            _metadataContext = new MetadataLoadContext(resolver);
        }

        public List<DolittleAggregate> DolittleAggregates => _aggregateList;

        public List<DolittleEvent> DolittleEvents => _eventList;

        public List<DolittleProjection> ProjectionList => _projectionList;

        public DolittleTypeMap? GenerateMapForAggregate(string aggregateName)
        {
            var typeMap = new DolittleTypeMap();
            
            if(_aggregateList is null || !_aggregateList.Any())
                DiscoverDolittleTypes();

            var aggregate = _aggregateList!.FirstOrDefault(a => a.Name.Equals(aggregateName, StringComparison.InvariantCultureIgnoreCase));

            if (aggregate == null)
                return null;

            typeMap.Aggregate = aggregate;
            typeMap.Events = _eventList;

            if (typeMap.Aggregate is { })
            {
                Out.Info($"Aggregate '{ColorAs.Value(typeMap.Aggregate.Name)}', {ColorAs.Value(typeMap.Aggregate.Id.ToString())}.{Environment.NewLine}EventTypes found in binaries folder: {ColorAs.Value(typeMap.Events.Count.ToString())}");
            }
            return typeMap;
        }

        /// <summary>
        /// Perform a discovery on all aggregates, events, and projections
        /// </summary>
        public void DiscoverDolittleTypes()
        {

            var dllFiles = LoadDllFiles();
            double fileStep = 100.0 / dllFiles.Count;
            AnsiConsole.Progress()
                .AutoRefresh(true) // Turn off auto refresh
                .AutoClear(true)   // Do not remove the task list when done
                .HideCompleted(true)   // Hide tasks as they are completed
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                })
                .Start(ctx =>
            {
                var fileTask = ctx.AddTask($"Examining {dllFiles.Count} DLL files:");
                foreach (var dllFilePath in dllFiles)
                {
                    fileTask.Increment(fileStep);
                    Type[] types;
                    var assembly = _metadataContext.LoadFromAssemblyPath(dllFilePath);
                    types = assembly.GetTypes();
                    double typeStep = 100.0 / types.Length;

                    var fileInfo = new FileInfo(dllFilePath);
                    var typesTask = ctx.AddTask($"{fileInfo.Name}:");
                    foreach (var type in types)
                    {
                        typesTask.Increment(typeStep);
                        try
                        {
                            if (MapTypeToAggregateRoot(type))
                                continue;
                        }
                        catch
                        { }
                        try
                        {
                            if (MapTypeToDolittleEvent(type))
                                continue;
                        }
                        catch
                        { }
                        try
                        {
                            if (MapTypeToDolittleProjection(type))
                                continue;
                        }
                        catch
                        { }
                    }
                    typesTask.Value = 100.0;
                }
            });
        }

        private bool MapTypeToDolittleEvent(Type type)
        {
            var eventTypeAttribute = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name.Equals(DolittleEvent.AttributeName));
            if (eventTypeAttribute is null)
                return false;

            var eventTypeId = eventTypeAttribute.ConstructorArguments[0].Value.ToString();
            if (string.IsNullOrEmpty(eventTypeId))
                return false;

            _eventList.Add(new DolittleEvent { Name = type.Name, Id = eventTypeId });
            return true;
        }

        private bool MapTypeToDolittleProjection(Type type)
        {
            var eventTypeAttribute = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name.Equals(DolittleProjection.AttributeName));
            if (eventTypeAttribute is null)
                return false;

            var eventTypeId = eventTypeAttribute.ConstructorArguments[0].Value.ToString();
            if (string.IsNullOrEmpty(eventTypeId))
                return false;

            _eventList.Add(new DolittleEvent { Name = type.Name, Id = eventTypeId });
            return true;
        }

        private bool MapTypeToAggregateRoot(Type type)
        {
            if (type.BaseType?.Name.Equals(AggregateRootName) ?? false)
            {
                var aggregateRootAttribute = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name.Equals(DolittleAggregate.AttributeName));
                if (aggregateRootAttribute is null)
                    return false;

                var aggregateRootId = aggregateRootAttribute.ConstructorArguments[0].Value.ToString();

                if (string.IsNullOrEmpty(aggregateRootId))
                    return false;

                _aggregateList.Add(new DolittleAggregate { Id = Guid.Parse(aggregateRootId), Name = type.Name });
                return true;
            }
            return false;
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

        public void Dispose()
        {
            _metadataContext.Dispose();
        }
    }
}
