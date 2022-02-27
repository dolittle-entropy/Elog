using System;
using System.Diagnostics;

namespace OutputWriting
{
    public class TimedAction : IDisposable
    {
        readonly Stopwatch _stopwatch;
        readonly string _message;

        public TimedAction(string mmessage)
        {
            _message = mmessage;
            _stopwatch = Stopwatch.StartNew();
        }
        public void Dispose()
            => Out.Info($"{_message} completed in {_stopwatch.ElapsedMilliseconds.ToString("### ###.0").Trim()}ms");

    }
}
