using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;

namespace MongoDbQueueService
{
    public class Subscriber : ISubscriber
    {
        private readonly string _workerName;
        private readonly bool _deleteOnAcknowledge;
        private IMongoDatabase _database;
        private IMongoCollection<QueueCollection> _queueCollection;

        public Subscriber(string url, string database, string collection, string workerName, bool deleteOnAcknowledge = false)
        {
            var client = new MongoClient(url);
            this._database = client.GetDatabase(database);
            this._queueCollection = this._database.GetCollection<QueueCollection>(collection);
            this._workerName = workerName;
            this._deleteOnAcknowledge = deleteOnAcknowledge;
        }

        public IObservable<T> SubscribeQueueCollection<T>(CancellationToken token)
        {
            var scheduleInstance = ThreadPoolScheduler.Instance;

            return Observable.Create<T>(item =>
            {
                var disposable = Observable
                    .Interval(TimeSpan.FromSeconds(1), scheduleInstance)
                    .Subscribe(async _ => 
                    {
                        var itemProcessingForWorker = await this._queueCollection
                            .FindAsync(x => x.WorkerName == this._workerName)
                            .Result
                            .SingleOrDefaultAsync();

                        if (itemProcessingForWorker != null)
                        {
                            return;
                        }

                        var filter = Builders<QueueCollection>.Filter.And
                        (
                            Builders<QueueCollection>.Filter.Eq(x => x.WorkerName, string.Empty),
                            Builders<QueueCollection>.Filter.Eq(x => x.Processed, false)
                        );
                        var update = Builders<QueueCollection>.Update.Set(x => x.WorkerName, this._workerName);
                        var result = await this._queueCollection.UpdateOneAsync(filter, update);

                        var itemFromQueue = await this._queueCollection
                            .FindAsync(x => x.WorkerName == this._workerName)
                            .Result
                            .SingleOrDefaultAsync();

                        if (itemFromQueue != null)
                        {
                            try
                            {
                                // item.OnNext(JsonSerializer.Deserialize<T>(itemFromQueue.Payload.ToString()));
                                // var json = itemFromQueue.Payload.ToJson();

                                // var deserializedPayload = BsonSerializer.Deserialize<T>(itemFromQueue.Payload);
                                // item.OnNext(deserializedPayload);

                                // item.OnNext(itemFromQueue.Payload);
                                if (this._deleteOnAcknowledge)
                                {
                                    await this.AcknowledgeAndDelete().ConfigureAwait(false);
                                }
                                else
                                {
                                    await this.AcknowledgeWithoutDelete().ConfigureAwait(false);    
                                }
                            }
                            catch
                            {
                                throw new InvalidOperationException($"Was not possible to process payload: {itemFromQueue.Payload}");
                            }
                        }

                    });
                token.Register(() => disposable.Dispose());

                return Disposable.Empty;
            });
        }

        private async Task AcknowledgeAndDelete()
        {
            var filter = Builders<QueueCollection>.Filter.Eq(x => x.WorkerName, this._workerName);
            var item = await this._queueCollection
                .FindAsync(filter)
                .Result
                .SingleAsync()
                .ConfigureAwait(false);

            if (item == null)
            {
                throw new InvalidOperationException($"Cannot Acknowledge last operation from Worker: {this._workerName}");
            }

            await this._queueCollection
                .DeleteOneAsync(filter)
                .ConfigureAwait(false);
        }

        private async Task AcknowledgeWithoutDelete()
        {
            var filter = Builders<QueueCollection>.Filter.Eq(x => x.WorkerName, this._workerName);
            var item = await this._queueCollection
                .FindAsync(filter)
                .Result
                .SingleAsync()
                .ConfigureAwait(false);

            if (item == null)
            {
                throw new InvalidOperationException($"Cannot Acknowledge last operation from Worker: {this._workerName}");
            }

            item.WorkerName = string.Empty;
            item.Processed = true;

            var update = Builders<QueueCollection>.Update
                .Set(x => x.WorkerName, string.Empty)
                .Set(x => x.Processed, true);

            await this._queueCollection
                .UpdateOneAsync(filter, update)
                .ConfigureAwait(false);
        }
    }
}
