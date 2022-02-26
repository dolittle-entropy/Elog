using Newtonsoft.Json;
using OutputWriting;
using Spectre.Console;

namespace Common
{
    public static class ElogConfigurationFactory
    {
        static string _configurationFile;

        public static ElogConfiguration? Load()
        {
            var configurationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configurationFile = Path.Combine(configurationFolder, ElogConfiguration.ConfigurationFileName);

            if (!File.Exists(_configurationFile))
            {
                Out.Error($"No configuration found! Run '{ColorAs.Value("elog config")}' to create your first configuration.{Environment.NewLine}");
                return default;
            }
            var fileContents = File.ReadAllText(_configurationFile);
            var configurations = JsonConvert.DeserializeObject<List<ElogConfiguration>>(fileContents);
            var configuration = configurations.FirstOrDefault(c => c.IsDefault);
            if (configuration == null)
            {
                Out.Error($"No configurations exist. Run '{ColorAs.Value("Elog configure")}' to create your first configuration");
                return default;
            }            
            return configuration;
        }

        public static ElogConfiguration Display(this ElogConfiguration configuration)
        {
            Out.Info($"Loaded Configuration: {ColorAs.Value(_configurationFile)}");

            var table = new Table()
                .AddColumn("Setting")
                .AddColumn("Value");

            table.Border = TableBorder.Horizontal;

            table.AddRow("Configuration name", ColorAs.Value(configuration.Name));
            table.AddRow("MongoDB Server", ColorAs.Value(configuration.MongoConfig.MongoServer));
            table.AddRow("EventStore Database Name", ColorAs.Value(configuration.MongoConfig.MongoDB));
            table.AddRow("MongoDb Port", ColorAs.Value(configuration.MongoConfig.Port.ToString()));
            AnsiConsole.Write(table);
            Console.WriteLine();
            return configuration;
        }
    }
}
