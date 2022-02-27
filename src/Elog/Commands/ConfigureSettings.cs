using Spectre.Console.Cli;

namespace Elog.Commands
{
    public class ConfigureSettings : CommandSettings
    {

        [CommandOption("-c|--create")]
        public bool Create { get; set; }

        [CommandOption("-d|--delete")]
        public bool Delete { get; set; }

        [CommandOption("-a|--active")]
        public bool ActiveConfiguration { get; set; }

        [CommandOption("-e|--edit")]
        public bool Update { get; set; }
    }
}
