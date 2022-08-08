namespace MongoDbQueueService.Configuration
{
    public class PublisherSettings
    {
        public string ConnectionString { get; set; }
        public string Database { get; set; }
        public string Queue { get; set; }
    }
}
