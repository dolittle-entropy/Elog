namespace MongoDbReading
{
    public class EventEntry
    {
        public string Aggregate { get; set; }
        public string Event { get; set; }
        public DateTime Time { get; set; }
        public string PayLoad { get; set; }
        public bool IsPublic { get; set; }
    }
}