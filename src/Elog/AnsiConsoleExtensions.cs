using Spectre.Console;

namespace Elog
{
    public static class MessageColors
    {
        public const string InfoColor    = "[silver]";
        public const string WarningColor = "[yellow]";
        public const string ErrorColor   = "[red]";
        public const string ValueColor   = "[orange1]";
    }

    public static class ColorAs
    {                
        public static string Info(string message) => $"{MessageColors.InfoColor}{message}[/]";
        public static string Warning(string message) => $"{MessageColors.WarningColor}{message}[/]";
        public static string Error(string message) => $"{MessageColors.ErrorColor}{message}[/]";
        public static string Value(string message) => $"{MessageColors.ValueColor}{message}[/]";
    }

    public static class Ansi
    {
        public static void Info(string message)
            => AnsiConsole.MarkupLine(ColorAs.Info(message));

        public static void Warning(string message)
            => AnsiConsole.MarkupLine(ColorAs.Warning(message));

        public static void Error(string message)
            => AnsiConsole.MarkupLine(ColorAs.Error(message));
    }
}
