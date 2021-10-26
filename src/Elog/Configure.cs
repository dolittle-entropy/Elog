using ConsoleTables;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Elog
{
    [Command("configure", Description = "Runs a configuration wizard to get you started")]
    public class Configure
    {
        [Option(Description = "Lists your configurations")]
        public bool List { get; set; }

        [Option(Description = "Deletes the first configuration that matches the name provided")]
        public string Delete { get; set; }

        const string ConfigurationFileName = "elog.config";

        readonly string _configurationFile;

        readonly IOutputWriter _out = new ConsoleOutputWriter();

        public Configure()
        {
            var applicationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configurationFile = Path.Combine(applicationFolder, ConfigurationFileName);
        }

        // warnings that the parameter is not used and can be removed
#pragma warning disable RCS1163, IDE0060
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

            _out.Write("Running the configuration wizard for ELog");
            _out.Divider();

            var config = PromptForConfiguration();
            var savedConfigs = LoadConfiguration();

            if (savedConfigs is null)
            {
                savedConfigs = new List<ElogConfiguration>
                {
                    config
                };
                _out.Write($"Configuration '{config.Name}' created.");
            }
            else if (savedConfigs.Any(c => c.Name.Equals(config.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (!_out.Confirm($"A configuration named {config.Name} already exists. Overwrite?"))
                {
                    _out.Write("Operation aborted");
                    return;
                }
                var oldConfig = savedConfigs.First(c => c.Name.Equals(config.Name, StringComparison.InvariantCultureIgnoreCase));
                savedConfigs.Remove(oldConfig);
                savedConfigs.Add(config);
                _out.Write($"Configuration '{config.Name}' updated");
            }
            else
            {
                savedConfigs.Add(config);
                _out.Write($"Configuration '{config.Name}' added");
            }
            WriteConfiguration(savedConfigs);
        }
#pragma warning restore RCS1163, IDE0060

        private void DeleteConfiguration()
        {
            var configurations = LoadConfiguration();
            var configurationToDelete = configurations.Find(c => c.Name.Equals(Delete, StringComparison.InvariantCultureIgnoreCase));
            if (configurationToDelete is { })
            {
                configurations.Remove(configurationToDelete);
                WriteConfiguration(configurations);
            }
            else
            {
                _out.DisplayError($"Could not find a configuration named '{Delete}'");
            }
        }

        private void DisplayConfigurations()
        {
            _out.Write("Elog - List Configurations\n");
            _out.Write($"Configuration loaded from: {_configurationFile}\n");

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
            _out.Divider();
            _out.Write($"{savedConfigs.Count} Configurations found\n");
        }

        static string FindSolutionNameInBinariesPath(string binariesPath)
        {
            var bits = binariesPath.Split('\\');
            if (bits.Length == 1)
            {
                // Support mac/linux paths
                bits = binariesPath.Split('/');
            }

            for (var i = bits.Length - 1; i > 0; i--)
            {
                if (bits[i].Equals("bin"))
                {
                    return bits[i - 2];
                }
            }
            return string.Empty;
        }

        bool TestConfiguration(ElogConfiguration config)
        {
            const string KeyDolittleSDKFile = "Dolittle.SDK.Aggregates.dll";
            var filePath = Path.Combine(config.BinariesPath, KeyDolittleSDKFile);
            if (!File.Exists(filePath))
            {
                _out.DisplayError($"The folder '{config.BinariesPath}' does not contain the key file '{KeyDolittleSDKFile}'. Configuration fails!");
                return false;
            }

            var eventStoreReader = new EventStoreReader(
                config.MongoConfig.MongoServer,
                config.MongoConfig.Port,
                config.MongoConfig.MongoDB,
                _out);

            return eventStoreReader.ConnectionWorks();
        }

        private ElogConfiguration PromptForConfiguration()
        {
            _out.Write("Hit [ENTER] to accept default values or edit where necessary:");

            var configName = _out.AskForValue("Give your config a name         : ", "Default");
            var mongoServer = _out.AskForValue("MongoDB ServerInstance          : ", "localhost");
            var mongoDB = _out.AskForValue("MongoDB Database name           : ", "event_store");
            var mongoPort = _out.AskForValue("Port to use                     : ", "27017");
            var pathToBinaries = _out.AskForValue("Complete path to binaries folder: ", "C:\\dev");
            _out.Divider();

            if (int.TryParse(mongoPort, out var port))
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
                    _out.DisplayError("Configuration is invalid. Aborting");
                    return null;
                }
                return config;
            }
            else
            {
                _out.DisplayError("The Port for MongoDB is not a valid number. Configuration was not saved");
            }
            return null;
        }

        private List<ElogConfiguration> LoadConfiguration()
        {
            List<ElogConfiguration> existingConfiguration = null;

            if (File.Exists(_configurationFile))
            {
                var fileContent = File.ReadAllText(_configurationFile);
                if (string.IsNullOrEmpty(fileContent))
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
                _out.Write($"Configuration saved to: {_configurationFile}");
            }
        }
    }
}
