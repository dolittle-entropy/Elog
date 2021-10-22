namespace Elog
{
    public class ElogConfiguration
    {
        public string Name { get; set; }

        public string BinariesPath { get; set; }

        public MongoConfig MongoConfig { get; set; }

        public const string ConfigurationFileName = "elog.config";
    }
}
