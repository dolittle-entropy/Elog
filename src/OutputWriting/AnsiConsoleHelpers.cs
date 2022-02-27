using Spectre.Console;

namespace OutputWriting
{

    public static class Out
    {
        public static void Info(string message)
            => AnsiConsole.MarkupLine(ColorAs.Info(message));

        public static void Warning(string message)
            => AnsiConsole.MarkupLine(ColorAs.Warning(message));

        public static void Error(string message)
            => AnsiConsole.MarkupLine(ColorAs.Error(message));

        public static void Success(string message)
            => AnsiConsole.MarkupLine(ColorAs.Success(message));

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
