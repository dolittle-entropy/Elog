using Spectre.Console;

namespace OutputWriting
{

    public static class Out
    {
        public const string DetailedTimeFormat = "dddd dd.MMM.yyyy HH:mm:ss.ffff";
        public const string BigNumberFormat = "### ### ###";

        public static string BigNumber(long number)
            => ColorAs.Value(number.ToString(BigNumberFormat).Trim());

        public static void Info(string message)
            => AnsiConsole.MarkupLine(ColorAs.Info(CleanMessage(message)));

        public static void Warning(string message)
            => AnsiConsole.MarkupLine(ColorAs.Warning(CleanMessage(message)));

        public static void Error(string message)
            => AnsiConsole.MarkupLine(ColorAs.Error(CleanMessage(message)));

        public static void Success(string message)
            => AnsiConsole.MarkupLine(ColorAs.Success(CleanMessage(message)));

        static string? CleanMessage(string? message)
        {
            return message?.Replace("[", "[[").Replace("]", "]]") ?? default;
        }

        public static void Content(string title, string content)
        {
            var style = Style.Parse("red dim");
            var titleRule = new Rule(title);
            titleRule.Style = style;
            AnsiConsole.Write(titleRule);

            Info(ColorAs.Value(content));

            var bottomRule = new Rule();
            bottomRule.Style = style;
            AnsiConsole.Write(bottomRule);
        }
    }
}
