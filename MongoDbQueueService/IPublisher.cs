using System.Threading.Tasks;


namespace MongoDbQueueService
{
    public interface IPublisher
    {
        Task SendAsync<T>(T payload, int priority = 0);

        Task SendAsync(string payload, int priority = 0);
    }
}
