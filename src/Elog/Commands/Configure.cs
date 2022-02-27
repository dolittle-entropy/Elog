using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Common;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using Spectre.Console;
using Spectre.Console.Cli;
using sp = Spectre.Console.Cli;

namespace Elog.Commands
{

    public class Configure : sp.Command<ConfigureSettings>
    {
        const string ConfigurationFileName = "elog-config.json";

        readonly string _configurationFile;

        public Configure()
        {
            var applicationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configurationFile = Path.Combine(applicationFolder, ConfigurationFileName);
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] ConfigureSettings settings)
        {
            if (settings.Create) { CreateConfiguration(false); return 0; }
            if (settings.Delete) { DeleteConfiguration(); return 0; }
            if (settings.ActiveConfiguration) { ChangeDefaultConfiguration(); return 0; }
            if (settings.Update) { EditSomeConfiguration(); return 0; }

            Console.WriteLine("");
            DisplayConfigurations();

            return 0;
        }

        private void CreateConfiguration(bool firstTime)
        {

            if (!PromptForConfiguration(firstTime))
            {
                Out.Warning("Configuration abandoned");
                return;
            }
        }

        private void EditSomeConfiguration()
        {
            const string cancelMessage = "[red]* Cancel[/]";
            const string marker = "[yellow] *active*[/]";

            var configurations = LoadConfigurations();
            var list = new List<string>();
            list.Add(cancelMessage);
            list.AddRange(configurations.Select(c => c.IsDefault ? $"{c.Name}{marker}" : c.Name));

            var editConfigName = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title($"Select configuration to edit:")
                    .MoreChoicesText("[green]* scroll with your arrow keys to see more[/]")
                    .AddChoices(list));

            if (editConfigName.Equals(cancelMessage))
            {
                Out.Warning("Edit configuration cancelled");
                return;
            }
            editConfigName = editConfigName.Replace(marker, "");
            var configToEdit = PopConfiguration(configurations, editConfigName);
            if (configToEdit is null)
                AnsiConsole.MarkupLine($"ERROR: Configuration '{editConfigName}' not found");

            AnsiConsole.MarkupLine($"Editing configuration '{ColorAs.Value(editConfigName)}'.{Environment.NewLine}Input new values, or hit [[Enter]] to accept current:{Environment.NewLine}");
            configToEdit.Name = AnsiConsole.Ask("Configuration name:", configToEdit.Name);
            configToEdit.IsDefault = AnsiConsole.Confirm("Make this the active configuration?", configToEdit.IsDefault);
            configToEdit.MongoConfig.MongoServer = AnsiConsole.Ask("MongoDb server:", configToEdit.MongoConfig.MongoServer);
            configToEdit.MongoConfig.MongoDB = AnsiConsole.Ask("EventStore Database name:", configToEdit.MongoConfig.MongoDB);
            configToEdit.MongoConfig.Port = AnsiConsole.Ask("EventStore Database port:", configToEdit.MongoConfig.Port);
            configToEdit.BinariesPath = AnsiConsole.Ask("Path to binaries:", configToEdit.BinariesPath);
            while (!Directory.Exists(configToEdit.BinariesPath))
            {
                Out.Error($"ERROR: Directory does not exist: {ColorAs.Value(configToEdit.BinariesPath)}");
                configToEdit.BinariesPath = AnsiConsole.Ask("Path to binaries:", configToEdit.BinariesPath);
            }

            if (TestConfiguration(configToEdit))
            {
                configurations.Add(configToEdit);
                WriteConfiguration(configurations);
            }
            return;
        }

        ElogConfiguration? PopConfiguration(List<ElogConfiguration> configurations, string configurationName)
        {
            var matchingConfiguration = configurations.FirstOrDefault(c => c.Name.Equals(configurationName, StringComparison.InvariantCultureIgnoreCase));
            if (matchingConfiguration is { })
            {
                configurations.Remove(matchingConfiguration);
                return matchingConfiguration;
            }
            return null;
        }

        private void ChangeDefaultConfiguration()
        {
            var cancelMessage = ColorAs.Warning("Cancel");
            var marker = ColorAs.Success(" **active**");

            var configurations = LoadConfigurations();
            var list = new List<string>();
            list.Add(cancelMessage);
            list.AddRange(configurations.Select(c => c.IsDefault ? $"{c.Name}{marker}" : c.Name));
            var newDefaultConfigName = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title($"Select the configuration to make active:")
                    .MoreChoicesText("[green]* scroll with your arrow keys to see more[/]")
                    .AddChoices(list));

            if (newDefaultConfigName.Equals(cancelMessage))
            {
                Out.Warning("Canceled active configuration selection");
                return;
            }

            if (newDefaultConfigName.EndsWith(marker))
                newDefaultConfigName = newDefaultConfigName.Replace(marker, "");

            var yesDoIt = AnsiConsole.Confirm($"Make '{ColorAs.Value(newDefaultConfigName)}' your active configuration?", true);
            if (!yesDoIt)
            {
                Out.Warning("Ok. Canceled selection of active configuration");
                return;
            }

            for (var i = 0; i < configurations.Count; i++)
            {
                configurations[i].IsDefault = false;
                if (configurations[i].Name.Equals(newDefaultConfigName))
                    configurations[i].IsDefault = true;
            }

            AnsiConsole.Status().Start("Writing new configuration", ctx =>
            {
                Thread.Sleep(500);
                WriteConfiguration(configurations);
            });
            Out.Info($"Done. '{ColorAs.Value(newDefaultConfigName)}' is now the active configuration");
        }

        private void DeleteConfiguration()
        {
            const string cancelMessage = "[red]* Cancel[/]";

            var configurations = LoadConfigurations();
            var list = new List<string>();
            list.Add(cancelMessage);
            list.AddRange(configurations.Select(c => c.Name));
            var configurationToDelete = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title($"Select configuration to delete:")
                    .MoreChoicesText("[green]* scroll with your arrow keys to see more[/]")
                    .AddChoices(list));

            if (configurationToDelete.Equals(cancelMessage))
            {
                AnsiConsole.MarkupLine("[yellow]Delete operation aborted[/]");
                return;
            }

            var selectedConfiguration = configurations.First(c => c.Name.Equals(configurationToDelete));
            if (selectedConfiguration is { })
            {
                configurations.Remove(selectedConfiguration);
            }

            if (selectedConfiguration.IsDefault && configurations.Count > 1)
            {
                AnsiConsole.MarkupLine($"'[yellow]{selectedConfiguration}[/]' was the active configuration!");
                var newDefault = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title("Select your new active configuration:")
                    .AddChoices(configurations.Select(c => c.Name))
                    ); ;

                var newDefaultConfig = configurations.First(c => c.Name.Equals(newDefault));
                newDefaultConfig.IsDefault = true;
                AnsiConsole.MarkupLine($"'[green]{newDefault}[/]' is now your active configuration");
            }

            var yesDelete = AnsiConsole.Confirm($"Ready to delete configuration '{configurationToDelete}'?", false);
            if (yesDelete)
            {
                WriteConfiguration(configurations);
                AnsiConsole.Status().Start("Writing new configuration", ctx =>
                {
                    Thread.Sleep(1000);
                    AnsiConsole.MarkupLine("Configuration updated");
                });
            }
            else
            {
                AnsiConsole.MarkupLine($"Ok, delete operation skipped.");
            }
            return;
        }

        private void DisplayConfigurations()
        {
            var savedConfigs = LoadConfigurations();
            if (!savedConfigs?.Any() ?? true)
            {
                Out.Warning("No configurations found. Starting the configuration wizard");
                CreateConfiguration(firstTime: true);
                return;
            }

            AnsiConsole.MarkupLine($"Configuration loaded from [yellow]{_configurationFile}[/]");

            var ansiTable = new Table()
                .Border(TableBorder.Square)
                .AddColumn("Configuration name")
                .AddColumn("Solution(s)")
                .AddColumn("Mongo Server")
                .AddColumn("EventStore DB")
                .AddColumn("Mongo Port")
                .AddColumn("Active?");

            ansiTable.Columns[4].RightAligned();
            ansiTable.Columns[5].Centered();

            foreach (var config in savedConfigs)
            {
                var solutionName = FindSolutionNameInBinariesPath(config.BinariesPath);
                if (solutionName.Contains("-Bad Path-"))
                {
                    ansiTable.AddRow(
                        ColorAs.Error(config.Name),
                        ColorAs.Error(solutionName),
                        ColorAs.Error(config.MongoConfig.MongoServer),
                        ColorAs.Error(config.MongoConfig.MongoDB),
                        ColorAs.Error(config.MongoConfig.Port.ToString()),
                        ColorAs.Error(""));
                }
                else
                {
                    ansiTable.AddRow(
                        config.IsDefault ? ColorAs.Success(config.Name) : config.Name,
                        config.IsDefault ? ColorAs.Success(solutionName) : solutionName,
                        config.IsDefault ? ColorAs.Success(config.MongoConfig.MongoServer) : config.MongoConfig.MongoServer,
                        config.IsDefault ? ColorAs.Success(config.MongoConfig.MongoDB) : config.MongoConfig.MongoDB,
                        config.IsDefault ? ColorAs.Success(config.MongoConfig.Port.ToString()) : config.MongoConfig.Port.ToString(),
                        config.IsDefault ? ColorAs.Success("yes") : "");
                }
            }
            AnsiConsole.Write(ansiTable);
            AnsiConsole.MarkupLine($"[green]{savedConfigs.Count}[/] configurations found{Environment.NewLine}");
        }

        static string FindSolutionNameInBinariesPath(string binariesPath)
        {
            var startFolder = new DirectoryInfo(binariesPath);
            if (!startFolder.Exists)
            {
                return ColorAs.Error("-Bad Path-");
            }
            while (startFolder.Parent is DirectoryInfo && startFolder.GetFiles("*.sln").Length == 0)
                startFolder = startFolder.Parent;

            if (startFolder is null)
            {
                Out.Error("No solution files found in path");
                return string.Empty;
            }

            var solutionFiles = startFolder.GetFiles("*.sln");
            if (solutionFiles.Length == 1)
                return solutionFiles[0].Name.Replace(".sln", "");

            return string.Join("", solutionFiles.Select(s => s.Name.Replace(".sln", $"{Environment.NewLine}")));
        }

        bool TestConfiguration(ElogConfiguration config)
        {
            const string KeyDolittleSDKFile = "Dolittle.SDK.*";

            var folder = new DirectoryInfo(config.BinariesPath);
            if (folder.GetFiles(KeyDolittleSDKFile).Length == 0)
            {
                Out.Error($"The folder '{ColorAs.Value(config.BinariesPath)}' does not contain any files mathcing the pattern '{ColorAs.Value(KeyDolittleSDKFile)}'");
                return false;
            }
            var eventStoreReader = new EventStoreReader(config.MongoConfig);
            if (!eventStoreReader.ConnectionWorks())
            {
                Out.Error($"Unable to connect to mongo using '{config.MongoConfig.MongoServer}.{config.MongoConfig.MongoDB}:{config.MongoConfig.Port}'");
                return false;
            }
            return true;
        }

        bool PromptForConfiguration(bool firstConfiguration)
        {
            var allConfigurations = LoadConfigurations();
            if (allConfigurations == null)
            {
                allConfigurations = new List<ElogConfiguration>();
                firstConfiguration = true;
            }

            // Get config name or fail
            var configName = GetConfigurationName(allConfigurations);
            if (string.IsNullOrEmpty(configName))
                return false;

            // Get mongo settings
            var mongoServer = AnsiConsole.Ask("MongoDB Server Instance: ", "localhost");
            var mongoPort = AnsiConsole.Ask("MongoDb Port to use: ", 27017);

            var mongoDB = ChooseEventStore(mongoServer, mongoPort);
            if (string.IsNullOrEmpty(mongoDB))
                return false;

            // Get binaries folder
            var pathToBinaries = AnsiConsole.Ask("Complete path to binaries folder: ", "C:\\dev");
            while (!Directory.Exists(pathToBinaries))
            {
                Out.Error($"Path does not exist: {ColorAs.Value(pathToBinaries)}");
                pathToBinaries = AnsiConsole.Ask("Complete path to binaries folder: ", "C:\\dev");
            }
            // Ask if this should be the new default
            var makeDefaultConfig = false;
            if (firstConfiguration)
                makeDefaultConfig = true;
            else
                makeDefaultConfig = AnsiConsole.Confirm("Make this the default configuration?", false);

            ElogConfiguration? resultingConfiguration = null;

            var checksPassed = false;
            AnsiConsole.Status().Start("Checking the MongoDb settings", ctx =>
            {
                if (!MongoConfigurationIsValid(mongoServer, mongoDB, mongoPort))
                {
                    return;
                }
                resultingConfiguration = new ElogConfiguration
                {
                    Name = configName,
                    BinariesPath = pathToBinaries,
                    IsDefault = makeDefaultConfig,
                    MongoConfig = new MongoConfig
                    {
                        MongoServer = mongoServer,
                        MongoDB = mongoDB,
                        Port = mongoPort
                    }
                };
                Thread.Sleep(1000);
                ctx.Status("Testing your configuration...");
                if (!TestConfiguration(resultingConfiguration))
                    return;

                ctx.Status("Applying the new configuration...");
                if (makeDefaultConfig)
                {
                    for (var i = 0; i < allConfigurations.Count; i++)
                        allConfigurations[i].IsDefault = false;

                    resultingConfiguration.IsDefault = true;
                }

                allConfigurations.Add(resultingConfiguration);
                Thread.Sleep(1000);

                ctx.Status("Writing configuration...");
                WriteConfiguration(allConfigurations);
                Thread.Sleep(1000);
                checksPassed = true;
            });

            return checksPassed;
        }

        private string? ChooseEventStore(string mongoServer, int mongoPort)
        {
            var eventStoreNames = GetEventStoreDatabaseNames(mongoServer, mongoPort);
            if (!eventStoreNames?.Any() ?? true)
            {
                return string.Empty;
            }

            var choiceDetail = ColorAs.Value($"Found {eventStoreNames.Count} Event Stores. Select with up/down, confirm with ENTER");
            if (eventStoreNames.Count == 1)
                choiceDetail = ColorAs.Value("Found one Event Store found. Confirm with ENTER");

            var chosenEventStore = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title(choiceDetail)
                .PageSize(10)
                .AddChoices(eventStoreNames)
                .MoreChoicesText("Use the up/down keys to see more")
                );

            Out.Info($"Selected EventStore database: {ColorAs.Success(chosenEventStore)}");
            return chosenEventStore;
        }

        private List<string> GetEventStoreDatabaseNames(string mongoServer, int mongoPort)
        {
            var eventStoreDatabaseNames = new List<string>();
            var mongoSettings = new MongoClientSettings
            {
                Server = new MongoServerAddress(mongoServer, mongoPort),
                ConnectTimeout = TimeSpan.FromSeconds(3),
                ServerSelectionTimeout = TimeSpan.FromSeconds(3)
            };
            MongoClient? client = null;
            List<BsonDocument>? allDatabases = null;
            try
            {
                client = new MongoClient(mongoSettings);
                allDatabases = client.ListDatabases().ToList();
            }
            catch (Exception ex)
            {
                Out.Error($"Unable to connecto to server {ColorAs.Value(mongoServer)} using port {ColorAs.Value(mongoPort.ToString())}: {Environment.NewLine}{ex.StackTrace}");
                return eventStoreDatabaseNames;
            }

            if (client is null || allDatabases is null)
                return eventStoreDatabaseNames;

            foreach (var database in allDatabases)
            {
                var dbName = database["name"].AsString;
                if (dbName == null)
                    continue;

                var db = client.GetDatabase(dbName);
                var collectionNames = db.ListCollectionNames().ToList();
                if (collectionNames.Any(collectionName => collectionName.Equals("event-log")))
                {
                    eventStoreDatabaseNames.Add(dbName);
                }
            }
            if (!eventStoreDatabaseNames.Any())
                Out.Error($"No eventstores found in {ColorAs.Value(mongoServer)} using port {ColorAs.Value(mongoPort.ToString())}");

            return eventStoreDatabaseNames;
        }

        static string GetConfigurationName(List<ElogConfiguration> currentConfig)
        {
            var configName = AnsiConsole.Ask("What will you [yellow]name[/] your configuration?", "default");

            while (string.IsNullOrEmpty(configName.Trim()))
                AnsiConsole.Ask("What will you [yellow]name[/] your configuration?", "default");

            if (currentConfig.Any(c => c.Name.Equals(configName, StringComparison.InvariantCultureIgnoreCase)))
            {
                var chosen = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title($"Configuration '[yellow]{configName.ToUpper()}[/]' already exists. [yellow]Choose an action[/]")
                    .AddChoices("Replace", "Rename", "Abort"));

                switch (chosen)
                {
                    case "Rename": configName = RenameConfiguration(currentConfig, configName); break;
                    case "Abort": configName = string.Empty; break;
                    case "Replace":
                    default: break;
                }
            }
            return configName;
        }

        static string RenameConfiguration(List<ElogConfiguration> currentConfig, string configName)
        {
            var newName = configName;
            do
            {
                newName = AnsiConsole.Ask("Enter a new configuration name", $"{newName}_new");
                if (newName.Equals(configName, StringComparison.InvariantCultureIgnoreCase) || currentConfig.Any(c => c.Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase)))
                    AnsiConsole.MarkupLine($"[red]Nope, '{newName}' is taken. Try again![/]");

            } while (configName.Equals(newName, StringComparison.CurrentCultureIgnoreCase) && currentConfig.Any(c => c.Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase)));
            AnsiConsole.MarkupLine($"Configuration name [yellow]'{newName}'[/] accepted");
            configName = newName;
            return configName;
        }

        private bool MongoConfigurationIsValid(string mongoServer, string mongoDatabase, int mongoPort)
        {
            var tempConfig = new MongoConfig
            {
                MongoServer = mongoServer,
                MongoDB = mongoDatabase,
                Port = mongoPort
            };
            var eventStoreReader = new EventStoreReader(tempConfig);
            return eventStoreReader.ConnectionWorks();
        }

        private List<ElogConfiguration> LoadConfigurations()
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
                Out.Success($"Configuration saved to: {ColorAs.Value(_configurationFile)}");
            }
        }
    }
}
