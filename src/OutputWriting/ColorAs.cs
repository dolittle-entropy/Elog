namespace OutputWriting
{
    public static class ColorAs
    {
        public static string Info(string message) => $"{MessageColors.InfoColor}{message}[/]";
        public static string Warning(string message) => $"{MessageColors.WarningColor}{message}[/]";
        public static string Error(string message) => $"{MessageColors.ErrorColor}{message}[/]";
        public static string Value(string message) => $"{MessageColors.ValueColor}{message}[/]";
        public static string Success(string message) => $"{MessageColors.SuccessColor}{message}[/]";
    }
}
