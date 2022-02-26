using Spectre.Console;

namespace OutputWriting
{
    public static class MessageColors
    {
        public const string InfoColor    = "[silver]";
        public const string WarningColor = "[yellow]";
        public const string ErrorColor   = "[red]";
        public const string ValueColor   = "[orange1]";
        public const string SuccessColor = "[green3]";
    }

    public static class ColorAs
    {                
        public static string Info(string message) => $"{MessageColors.InfoColor}{message}[/]";
        public static string Warning(string message) => $"{MessageColors.WarningColor}{message}[/]";
        public static string Error(string message) => $"{MessageColors.ErrorColor}{message}[/]";
        public static string Value(string message) => $"{MessageColors.ValueColor}{message}[/]";
        public static string Success(string message) => $"{MessageColors.SuccessColor}{message}[/]";
    }

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

        public static void Content(string title, string message)
        {
            var style = Style.Parse("red dim");
            var titleRule = new Rule(title);
            titleRule.Style = style;
            AnsiConsole.Write(titleRule);
            
            Info(message);

            var bottomRule = new Rule();
            bottomRule.Style = style;
            AnsiConsole.Write(bottomRule);

        }
    }
}
