using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public Configure()
        {
            var applicationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configurationFile = Path.Combine(applicationFolder, configurationFileName);
        }

        public void OnExecute(CommandLineApplication app)
        {
            if(List)
            {
                DisplayConfigurations();
                return;
            }

            if(!string.IsNullOrEmpty(Delete))
            {
                DeleteConfiguration();
                return;
            }

            Console.WriteLine("Create a configuration for ELog");
            Console.WriteLine(new String('-', 80));
            
            var config       = PromptForConfiguration();
            var savedConfigs = LoadConfiguration();

            if(savedConfigs is null)
            {
                savedConfigs = new List<ElogConfiguration>();
                savedConfigs.Add(config);
                WriteConfiguration(savedConfigs);
                Console.WriteLine($"Configuration '{config.Name}' added.");
            }
            else if(savedConfigs.Any(c => c.Name.Equals(config.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                Console.Write($"A configuration named {config.Name} already exists. Overwrite? Y/n");
                var readKey = Console.ReadKey();
                Console.WriteLine();
                if (!readKey.Key.Equals(ConsoleKey.Enter) && !readKey.Key.Equals(ConsoleKey.Y))
                {
                    Console.WriteLine("Operation aborted");
                    return;
                }
                var oldConfig = savedConfigs.First(c => c.Name.Equals(config.Name, StringComparison.InvariantCultureIgnoreCase));
                savedConfigs.Remove(oldConfig);
                savedConfigs.Add(config);
                Console.WriteLine($"Configuration '{config.Name}' updated");
            }
            else
            {
                savedConfigs.Add(config);
                Console.WriteLine($"Configuration '{config.Name}' added");
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
                Console.WriteLine($"ERROR: Could not find configuration named '{Delete}'");
            }
        }

        private void DisplayConfigurations()
        {
            var savedConfigs = LoadConfiguration();
            foreach(var config in savedConfigs)
            {
                Console.WriteLine($"- {config.Name}");
            }
            Console.WriteLine(new String('-', 80));
            Console.WriteLine($"{savedConfigs.Count} Configurations found");
        }

        private bool TestConfiguration(ElogConfiguration config)
        {
            const string keyDolittleSDKFile = "Dolittle.SDK.Aggregates.dll";
            if (!File.Exists(keyDolittleSDKFile))
            {
                Console.WriteLine($"The binaries folder '{config.BinariesPath}' does not appear to contain a key file '{keyDolittleSDKFile}' ");
                return false;
            }

            var eventStoreReader = new EventStoreReader(
                config.MongoConfig.MongoServer,
                config.MongoConfig.Port,
                config.MongoConfig.MongoDB);

            if (!eventStoreReader.ConnectionWorks())
            {
                return false;
            }
            return true;
        }

        private ElogConfiguration PromptForConfiguration()
        {
            Console.WriteLine("Hit [ENTER] to accept default values");

            var configName = ReadLine("Give your config a name          : ", "Default");
            var mongoServer = ReadLine("MongoDB ServerInstance           : ", "localhost");
            var mongoDB = ReadLine("MongoDB Database name            : ", "event_store");
            var mongoPort = ReadLine("Port to use                      : ", "27017");
            var pathToBinaries = ReadLine("Complete path to binaries folder: ", "C:\\dev");
            Console.WriteLine();

            if (int.TryParse(mongoPort, out int port))
            {
                var config = new ElogConfiguration
                {
                    Name = configName,
                    IsDefault = true,
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
                    Console.WriteLine("Configuration is invalid. Aborting");
                    return null;
                }
                return config;
            }
            else
            {
                Console.WriteLine("The Port for MongoDB is not valid. Configuration was not saved");
            }
            return null;
        }

        static string ReadLine(string prompt, string defaultValue)
        {
            Console.Write(prompt);
            var currColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(defaultValue);
            Console.ForegroundColor = currColor;

            var (_, top) = Console.GetCursorPosition();
            Console.SetCursorPosition(prompt.Length, top - 1);
            var key = Console.ReadKey();

            while (key.Key == ConsoleKey.Backspace)
            {
                Console.SetCursorPosition(prompt.Length, top - 1);
                key = Console.ReadKey();

            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.SetCursorPosition(0, top);
                return defaultValue;
            }
            else
            {
                Console.Write(new string(' ', defaultValue.Length)); // clean this line
                Console.SetCursorPosition(prompt.Length, top - 1);
                Console.Write(key.KeyChar);
                var res = key.KeyChar + Console.ReadLine();
                Console.SetCursorPosition(0, top);
                return res;
            }

        }

        private List<ElogConfiguration> LoadConfiguration()
        {
            List<ElogConfiguration> existingConfiguration = null;

            if (File.Exists(_configurationFile))
            {
                var fileContent = File.ReadAllText(_configurationFile);
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
                Console.Write($"Configuration saved: {_configurationFile}");
            }
        }
    }

    public class MongoConfig
    {
        public string MongoServer { get; set; }

        public string MongoDB { get; set; }

        public int Port { get; set; }

    }

    public class ElogConfiguration
    {
        public bool IsDefault { get; set; }

        public string Name { get; set; }

        public string BinariesPath { get; set; }

        public MongoConfig MongoConfig { get; set; }

        public static string ConfigurationFileName = "elog.config";
    }
}
