namespace MongoDbQueueService.Configuration
{
    public class PublisherSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Queue { get; set; } = string.Empty;
    }
}
