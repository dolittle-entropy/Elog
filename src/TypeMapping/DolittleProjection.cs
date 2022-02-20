namespace TypeMapping
{
    public record DolittleProjection
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public const string AttributeName = "ProjectionAttribute";
    }
}
