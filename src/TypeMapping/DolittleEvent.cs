using System.Diagnostics;

namespace TypeMapping
{
    [DebuggerDisplay("{Name}:{Id}")]
    public class DolittleEvent
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}