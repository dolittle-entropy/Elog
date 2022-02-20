using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using MongoDbReading;
using Newtonsoft.Json;
using OutputWriting;
using Spectre.Console;

namespace Elog
{
    [Command("configure", Description = "If no parameters are given, a wizard will help you create a new configuration.")]
    public class Configure
    {
        [Option(Description = "Lists your configurations")]
        public bool List { get; set; }

        [Option(Description = "Select a configuration to delete")]
        public bool Delete { get; set; }

        [Option(Description = "Change default configuration")]
        public bool ChangeDefault { get; set; }

        [Option(Description = "Edit an existing configuration")]
        public bool EditConfiguration { get; set; }

        const string ConfigurationFileName = "elog.config";

        readonly string _configurationFile;

        readonly IOutputWriter _out = new ConsoleOutputWriter();

        public Configure()
        {
            var applicationFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configurationFile = Path.Combine(applicationFolder, ConfigurationFileName);
        }

        static string InformationString(string content) => $"[green]{content}[/]";
        static string WarningString(string content) => $"[yellow]{content}[/]";
        static string ErrorString(string content) => $"[red]{content}[/]";

        // warnings that the parameter is not used and can be removed
        #pragma warning disable RCS1163, IDE0060

        public void OnExecute(CommandLineApplication app)
        {
            if (List) { DisplayConfigurations(); return; }
            if (Delete) { DeleteConfiguration(); return; }
            if (ChangeDefault) { ChangeDefaultConfiguration(); return; }
            if (EditConfiguration) { EditSomeConfiguration(); return; }

            Console.WriteLine("");

            var config = PromptForConfiguration();
            if (config is null)
            {
                AnsiConsole.MarkupLine("Configuration [yellow]aborted[/]");
                return;
            }
        }
        #pragma warning restore RCS1163, IDE0060

        private void EditSomeConfiguration()
        {
            const string cancelMessage = "[red]* Cancel[/]";
            const string marker = "[yellow] *active*[/]";
                        
            var configurations = LoadConfiguration();
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
                AnsiConsole.MarkupLine(WarningString("Edit configuration cancelled."));
                return;
            }
            editConfigName = editConfigName.Replace(marker, "");
            var configToEdit = PopConfiguration(configurations, editConfigName);
            if (configToEdit is null)
                AnsiConsole.MarkupLine($"ERROR: Configuration '{editConfigName}' not found");

            AnsiConsole.MarkupLine($"Editing configuration '{InformationString(editConfigName)}'.\nInput new values, or hit [[Enter]] to accept their current values:\n");
            configToEdit.Name = AnsiConsole.Ask("Configuration name:", configToEdit.Name);
            configToEdit.IsDefault = AnsiConsole.Confirm("Make this the default configuration?", configToEdit.IsDefault);
            configToEdit.MongoConfig.MongoServer = AnsiConsole.Ask("MongoDb server:", configToEdit.MongoConfig.MongoServer);
            configToEdit.MongoConfig.MongoDB = AnsiConsole.Ask("EventStore Database name:", configToEdit.MongoConfig.MongoDB);
            configToEdit.MongoConfig.Port = AnsiConsole.Ask("EventStore Database port:", configToEdit.MongoConfig.Port);                       
            configToEdit.BinariesPath = AnsiConsole.Ask("Path to binaries:", configToEdit.BinariesPath);
            while(!Directory.Exists(configToEdit.BinariesPath))
            {
                AnsiConsole.MarkupLine($"{ErrorString("ERROR: Directory does not exist:")} {configToEdit.BinariesPath}");
                configToEdit.BinariesPath = AnsiConsole.Ask("Path to binaries:", configToEdit.BinariesPath);
            }

            if(TestConfiguration(configToEdit))
            {

            }
            return;
        }

        ElogConfiguration? PopConfiguration(List<ElogConfiguration> configurations, string configurationName)
        {
            var matchingConfiguration = configurations.FirstOrDefault(c => c.Name.Equals(configurationName, StringComparison.InvariantCultureIgnoreCase));
            if(matchingConfiguration is { })
            {
                configurations.Remove(matchingConfiguration);
                return matchingConfiguration;
            }
            return null;
        }

        private void ChangeDefaultConfiguration()
        {
            const string cancelMessage = "[red]* Cancel[/]";
            const string marker = "[yellow] *default*[/]";

            var configurations = LoadConfiguration();
            var list = new List<string>();
            list.Add(cancelMessage);
            list.AddRange(configurations.Select(c => c.IsDefault ? $"{c.Name}{marker}" : c.Name));
            var newDefaultConfigName = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title($"Select the configuration to edit:")
                    .MoreChoicesText("[green]* scroll with your arrow keys to see more[/]")
                    .AddChoices(list));

            if (newDefaultConfigName.Equals(cancelMessage))
            {
                AnsiConsole.MarkupLine(WarningString("Canceled default selection"));
                return;
            }

            if (newDefaultConfigName.EndsWith(marker))
                newDefaultConfigName = newDefaultConfigName.Replace(marker, "");

            var yesDoIt = AnsiConsole.Confirm($"Make '{WarningString(newDefaultConfigName)}' your new default config?", true);
            if (!yesDoIt)
            {
                AnsiConsole.MarkupLine(WarningString("Ok. Canceled default selection"));
                return;
            }

            for (int i = 0; i < configurations.Count; i++)
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
            AnsiConsole.MarkupLine($"Done. '{InformationString(newDefaultConfigName)}' is now the default configuration");

        }

        private void DeleteConfiguration()
        {
            const string cancelMessage = "[red]* Cancel[/]";

            var configurations = LoadConfiguration();
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
                AnsiConsole.MarkupLine($"'[yellow]{selectedConfiguration}[/]' was the default configuration!");
                var newDefault = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .PageSize(10)
                    .Title("Select your new default configuration:")
                    .AddChoices(configurations.Select(c => c.Name))
                    ); ;

                var newDefaultConfig = configurations.First(c => c.Name.Equals(newDefault));
                newDefaultConfig.IsDefault = true;
                AnsiConsole.MarkupLine($"'[green]{newDefault}[/]' is now your default configuration");
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
            var savedConfigs = LoadConfiguration();
            AnsiConsole.MarkupLine($"Configuration loaded from [yellow]{_configurationFile}[/]");

            var ansiTable = new Spectre.Console.Table()
                .Border(TableBorder.Rounded)
                .RoundedBorder()
                .AddColumn("Config")
                .AddColumn("Solution(s)")
                .AddColumn("Server")
                .AddColumn("EventStoreDb")
                .AddColumn("Port")
                .AddColumn("Default");

            ansiTable.Columns[4].RightAligned();
            ansiTable.Columns[5].Centered();

            foreach (var config in savedConfigs)
            {
                var solutionName = FindSolutionNameInBinariesPath(config.BinariesPath);
                if(solutionName.Contains("-Bad Path-"))
                {
                    ansiTable.AddRow(
                        ErrorString(config.Name),
                        ErrorString(solutionName),
                        ErrorString(config.MongoConfig.MongoServer),
                        ErrorString(config.MongoConfig.MongoDB),
                        ErrorString(config.MongoConfig.Port.ToString()),
                        ErrorString("")); 
                }
                else
                {
                    ansiTable.AddRow(
                        config.IsDefault ? InformationString(config.Name) : config.Name,
                        config.IsDefault ? InformationString(solutionName) : solutionName,
                        config.IsDefault ? InformationString(config.MongoConfig.MongoServer) : config.MongoConfig.MongoServer,
                        config.IsDefault ? InformationString(config.MongoConfig.MongoDB) : config.MongoConfig.MongoDB,
                        config.IsDefault ? InformationString(config.MongoConfig.Port.ToString()) : config.MongoConfig.Port.ToString(),
                        config.IsDefault ? InformationString("yes") : "");
                }
            }
            AnsiConsole.Write(ansiTable);
            AnsiConsole.MarkupLine($"[green]{savedConfigs.Count}[/] configurations found");
        }

        static string FindSolutionNameInBinariesPath(string binariesPath)
        {
            var startFolder = new DirectoryInfo(binariesPath);
            if(!startFolder.Exists)
            {
                return ErrorString("-Bad Path-");
            }
            while (startFolder.Parent is DirectoryInfo && startFolder.GetFiles("*.sln").Length == 0)
                startFolder = startFolder.Parent;

            if (startFolder is null)
            {
                AnsiConsole.MarkupLine(ErrorString("No solution files found in path"));
                return String.Empty;
            }

            var solutionFiles = startFolder.GetFiles("*.sln");
            if(solutionFiles.Length == 1)
                return solutionFiles[0].Name.Replace(".sln", "");

            return string.Join("", solutionFiles.Select(s => s.Name.Replace(".sln", "\n")));
        }

        bool TestConfiguration(ElogConfiguration config)
        {
            const string KeyDolittleSDKFile = "Dolittle.SDK.*";
            bool passed = false;
            AnsiConsole.Status().Start($"Checking {config.BinariesPath}", ctx =>
            {                
                var folder = new DirectoryInfo(config.BinariesPath);
                if (folder.GetFiles(KeyDolittleSDKFile).Length == 0)
                {
                    AnsiConsole.MarkupLine($"The folder [yellow]'{config.BinariesPath}'[/] does not contain any files [yellow]'{KeyDolittleSDKFile}'[/]. Configuration fails!");
                    return;
                }
                Thread.Sleep(500);
                
                ctx.Status("Testing connection to MongoDb");
                var eventStoreReader = new EventStoreReader(
                    config.MongoConfig.MongoServer,
                    config.MongoConfig.Port,
                    config.MongoConfig.MongoDB,
                    _out);

                if(!eventStoreReader.ConnectionWorks())
                {
                    AnsiConsole.MarkupLine($"Unable to connect to mongo using '{config.MongoConfig.MongoServer}.{config.MongoConfig.MongoDB}:{config.MongoConfig.Port}'");
                    return;
                }
                Thread.Sleep(500);

                ctx.Status("Cleaning up");
                Thread.Sleep(500);
            });
            return passed;
        }

        ElogConfiguration PromptForConfiguration()
        {
            var currentConfig = LoadConfiguration();
            if (currentConfig == null)
                currentConfig = new List<ElogConfiguration>();

            // Get config name or fail
            var configName = GetConfigurationName(currentConfig);
            if (string.IsNullOrEmpty(configName))
                return null;

            // Get mongo settings
            var mongoServer = AnsiConsole.Ask("MongoDB [yellow]ServerInstance[/]          : ", "localhost");
            var mongoDB = AnsiConsole.Ask("MongoDB Database [yellow]name[/]           : ", "event_store");
            var mongoPort = AnsiConsole.Ask("[yellow]Port[/] to use                     : ", 27017);

            // Get binaries folder
            var pathToBinaries = AnsiConsole.Ask("Complete [yellow]path[/] to binaries folder: ", "C:\\dev");
            while (!Directory.Exists(pathToBinaries))
            {
                AnsiConsole.MarkupLine($"[red]Path does not exist:[/] {pathToBinaries}");
                pathToBinaries = AnsiConsole.Ask("Complete [yellow]path[/] to binaries folder: ", "C:\\dev");
            }
            // Ask if this should be the new default
            var makeDefaultConfig = AnsiConsole.Confirm("Make this the default configuration?", false);
            var testPass = false;
            AnsiConsole.Status().Start("Checking the MongoDb settings", ctx =>
            {
                if (!MongoConfigurationIsValid(mongoServer, mongoDB, mongoPort))
                {
                    AnsiConsole.Markup("[red]Unable to connect to your mongo database![/]");
                }
                var config = new ElogConfiguration
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
                if (!TestConfiguration(config))
                {
                    AnsiConsole.Markup("[red]Testing failed. Aborting.[/]");
                    return;
                }

                ctx.Status("Applying the new configuration...");
                if (makeDefaultConfig)
                {
                    for (int i = 0; i < currentConfig.Count; i++)
                        currentConfig[i].IsDefault = false;

                    config.IsDefault = true;
                }
                currentConfig.Add(config);
                Thread.Sleep(1000);

                ctx.Status("Writing configuration...");
                WriteConfiguration(currentConfig);
                Thread.Sleep(1000);

                testPass = true;
            });

            return null;
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
                newName = AnsiConsole.Ask<string>("Enter a new configuration name", $"{newName}_new");
                if (newName.Equals(configName, StringComparison.InvariantCultureIgnoreCase) || currentConfig.Any(c => c.Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase)))
                    AnsiConsole.MarkupLine($"[red]Nope, '{newName}' is taken. Try again![/]");

            } while (configName.Equals(newName, StringComparison.CurrentCultureIgnoreCase) && currentConfig.Any(c => c.Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase)));
            AnsiConsole.MarkupLine($"Configuration name [yellow]'{newName}'[/] accepted");
            configName = newName;
            return configName;
        }

        private bool MongoConfigurationIsValid(string mongoServer, string mongoDatabase, int mongoPort)
        {
            var eventStoreReader = new EventStoreReader(
                mongoServer,
                mongoPort,
                mongoDatabase,
                _out);

            return eventStoreReader.ConnectionWorks();
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
                AnsiConsole.MarkupLine($"Configuration saved to: [yellow]{_configurationFile}[/]");
            }
        }
    }
}
