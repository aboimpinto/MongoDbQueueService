namespace MongoDbQueueService.Configuration
{
    public class SubscriberSettings
    {
        public string ConnectionString { get; set; }
        public string Database { get; set; }
        public string Queue { get; set; }
        public string WorkerName { get; set; }
    }
}
