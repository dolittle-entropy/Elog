using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;

namespace Elog
{
    [Command("configure", Description = "Runs a configuration wizard to get you started")]
    public class Configure
    {
        [Option(Description = "Lists your configurations")]
        public bool List { get; set; }

        [Option(Description = "Deletes the first configuration that matches the name provided")]
        public string Delete { get; set; }

        const string configurationFileName = "elog.config";

        readonly string _configurationFile;

        readonly IOutputWriter Out = new ConsoleOutputWriter();

        public Configure()
        {
            var applicationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configurationFile = Path.Combine(applicationFolder, configurationFileName);
        }

        public void OnExecute(CommandLineApplication app)
        {
            if (List)
            {
                DisplayConfigurations();
                return;
            }

            if (!string.IsNullOrEmpty(Delete))
            {
                DeleteConfiguration();
                return;
            }


            Out.Write("Running the configuration wizard for ELog");
            Out.Divider();

            var config = PromptForConfiguration();
            var savedConfigs = LoadConfiguration();

            if (savedConfigs is null)
            {
                savedConfigs = new List<ElogConfiguration>();
                savedConfigs.Add(config);                
                Out.Write($"Configuration '{config.Name}' created.");
            }
            else if (savedConfigs.Any(c => c.Name.Equals(config.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (!Out.Confirm($"A configuration named {config.Name} already exists. Overwrite?"))
                {
                    Out.Write("Operation aborted");
                    return;
                }
                var oldConfig = savedConfigs.First(c => c.Name.Equals(config.Name, StringComparison.InvariantCultureIgnoreCase));
                savedConfigs.Remove(oldConfig);
                savedConfigs.Add(config);
                Out.Write($"Configuration '{config.Name}' updated");
            }
            else
            {
                savedConfigs.Add(config);
                Out.Write($"Configuration '{config.Name}' added");
            }
            WriteConfiguration(savedConfigs);        
        }

        private void DeleteConfiguration()
        {
            var configurations = LoadConfiguration();
            var configurationToDelete = configurations.FirstOrDefault(c => c.Name.Equals(Delete, StringComparison.InvariantCultureIgnoreCase));
            if (configurationToDelete is { })
            {
                configurations.Remove(configurationToDelete);
                WriteConfiguration(configurations);
            }
            else
            {
                Out.DisplayError($"Could not find a configuration named '{Delete}'");
            }
        }

        private void DisplayConfigurations()
        {
            Out.Write("Elog - List Configurations\n");            
            Out.Write($"Configuration loaded from: {_configurationFile}\n");
            
            var savedConfigs = LoadConfiguration();
            var consoleTable = new ConsoleTable("Name", "Server", "Port", "Database", "Solution");
            foreach (var config in savedConfigs)
            {
                consoleTable.AddRow(
                    config.Name, 
                    config.MongoConfig.MongoServer, 
                    config.MongoConfig.Port,
                    config.MongoConfig.MongoDB,
                    FindSolutionNameInBinariesPath(config.BinariesPath));                
            }
            consoleTable.Write(Format.Minimal);
            Out.Divider();
            Out.Write($"{savedConfigs.Count} Configurations found\n");
        }

        private string FindSolutionNameInBinariesPath(string binariesPath)
        {
            var bits = binariesPath.Split('\\');
            for(int i = bits.Length - 1; i > 0; i--)
            {
                if(bits[i].Equals("bin"))
                {
                    return bits[i - 2];
                }
            }
            return string.Empty;
        }

        private bool TestConfiguration(ElogConfiguration config)
        {
            const string keyDolittleSDKFile = "Dolittle.SDK.Aggregates.dll";
            if (!File.Exists(keyDolittleSDKFile))
            {
                Out.DisplayError($"The binaries folder '{config.BinariesPath}' does not appear to contain a key file '{keyDolittleSDKFile}' ");
                return false;
            }

            var eventStoreReader = new EventStoreReader(
                config.MongoConfig.MongoServer,
                config.MongoConfig.Port,
                config.MongoConfig.MongoDB,
                Out);

            if (!eventStoreReader.ConnectionWorks())
            {
                return false;
            }
            return true;
        }

        private ElogConfiguration PromptForConfiguration()
        {
            Out.Write("Hit [ENTER] to accept default values or edit where necessary:");

            var configName     = Out.AskForValue("Give your config a name         : ", "Default");
            var mongoServer    = Out.AskForValue("MongoDB ServerInstance          : ", "localhost");
            var mongoDB        = Out.AskForValue("MongoDB Database name           : ", "event_store");
            var mongoPort      = Out.AskForValue("Port to use                     : ", "27017");
            var pathToBinaries = Out.AskForValue("Complete path to binaries folder: ", "C:\\dev");
            Out.Divider();

            if (int.TryParse(mongoPort, out int port))
            {
                var config = new ElogConfiguration
                {
                    Name = configName,                    
                    BinariesPath = pathToBinaries,
                    MongoConfig = new MongoConfig
                    {
                        MongoServer = mongoServer,
                        Port = port,
                        MongoDB = mongoDB,
                    }
                };

                if (!TestConfiguration(config))
                {
                    Out.DisplayError("Configuration is invalid. Aborting");
                    return null;
                }
                return config;
            }
            else
            {
                Out.DisplayError("The Port for MongoDB is not a valid number. Configuration was not saved");
            }
            return null;
        }
       
        private List<ElogConfiguration> LoadConfiguration()
        {
            List<ElogConfiguration> existingConfiguration = null;

            if (File.Exists(_configurationFile))
            {
                var fileContent = File.ReadAllText(_configurationFile);
                if(string.IsNullOrEmpty(fileContent))
                {
                    return null;
                }
                existingConfiguration = JsonConvert.DeserializeObject<List<ElogConfiguration>>(fileContent);
            }
            return existingConfiguration;
        }

        private void WriteConfiguration(List<ElogConfiguration> config)
        {
            var serialized = JsonConvert.SerializeObject(config);
            if (serialized.Length > 0)
            {
                File.WriteAllText(_configurationFile, serialized);
                Out.Write($"Configuration saved to: {_configurationFile}");
            }
        }
    }
}
