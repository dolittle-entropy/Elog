using System;
using Spectre.Console.Cli;

namespace Elog.Commands
{
    public class RunSettings : CommandSettings
    {
        [CommandArgument(0, "[Aggregate name]")]
        public string AggregateName { get; set; } = string.Empty;

        [CommandArgument(1, "[Aggregate Id]")]
        public string Id { get; set; } = Guid.Empty.ToString();

        [CommandArgument(2, "[Event number]")]
        public int EventNumber { get; set; } = -1;

    }
}
