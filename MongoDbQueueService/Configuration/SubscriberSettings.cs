namespace MongoDbQueueService.Configuration
{
    public class SubscriberSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Queue { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public bool DeleteOnAcknowledge { get; set; }
    }
}
