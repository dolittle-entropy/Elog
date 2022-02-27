using Spectre.Console.Cli;

namespace Elog.Commands
{
    public class EventSettings : CommandSettings
    {
        [CommandArgument(0, "[EventType name]")]
        public string Filter { get; set; }
    }
}
