namespace TypeMapping
{
    public record DolittleEvent
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public const string AttributeName = "EventTypeAttribute";
    }
}
