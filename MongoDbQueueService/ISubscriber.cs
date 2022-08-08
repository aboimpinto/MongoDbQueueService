using System;
using System.Threading;

namespace MongoDbQueueService
{
    public interface ISubscriber
    {
        IObservable<T> SubscribeQueueCollection<T>(CancellationToken token);
    }
}