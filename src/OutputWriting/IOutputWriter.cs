namespace OutputWriting
{
    public interface IOutputWriter
    {
        void Write(string message);

        string AskForValue(string question, string defaultAnswer);

        bool Confirm(string message);

        void DisplayError(string errorMessage);

        void Divider();
    }
}