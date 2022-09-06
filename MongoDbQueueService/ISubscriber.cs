using System;
using System.Threading;

namespace MongoDbQueueService
{
    public interface ISubscriber
    {
        IObservable<SubscriptionResult<T>> SubscribeQueueCollection<T>(CancellationToken token);
    }
}