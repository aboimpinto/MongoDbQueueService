namespace MongoDbQueueService
{
    public class SubscriptionResult<T>
    {
        public T Payload { get; set; }

        public bool ProcessSucessful { get; set; }        
    }
}