using System;
using Spectre.Console;

namespace OutputWriting
{
    public class ConsoleOutputWriter : IOutputWriter
    {
        const string DIVIDER_LINE = "--------------------------------------------------------------------------------";

        public string AskForValue(string question, string defaultAnswer)
        {
            return AnsiConsole.Ask<string>(question);

            //Console.Write(question);
            //var currColor = Console.ForegroundColor;
            //Console.ForegroundColor = ConsoleColor.DarkGray;
            //Console.WriteLine(defaultAnswer);
            //Console.ForegroundColor = currColor;

            //var (_, top) = Console.GetCursorPosition();
            //Console.SetCursorPosition(question.Length, top - 1);
            //var key = Console.ReadKey();

            //while (key.Key == ConsoleKey.Backspace)
            //{
            //    Console.SetCursorPosition(question.Length, top - 1);
            //    key = Console.ReadKey();
            //}

            //if (key.Key == ConsoleKey.Enter)
            //{
            //    Console.SetCursorPosition(0, top);
            //    return defaultAnswer;
            //}
            //else
            //{
            //    Console.Write(new string(' ', defaultAnswer.Length)); // clean this line
            //    Console.SetCursorPosition(question.Length, top - 1);
            //    Console.Write(key.KeyChar);
            //    var res = key.KeyChar + Console.ReadLine();
            //    Console.SetCursorPosition(0, top);
            //    return res;
            //}
        }

        public void DisplayError(string errorMessage)
        {
            Write($"ERROR: {errorMessage}");
        }

        public void Divider()
        {
            Write(DIVIDER_LINE);
        }

        public bool Confirm(string message)
        {
            Console.Write($"{message}\nPress [ENTER] or 'Y' to confirm. Any other key cancels");
            var readKey = Console.ReadKey();

            return readKey.Key.Equals(ConsoleKey.Enter) || readKey.Key.Equals(ConsoleKey.Y);
        }

        public void Write(string message)
        {
            Console.WriteLine($"{message}");
        }
    }
}
