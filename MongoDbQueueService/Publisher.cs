using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoDbQueueService
{
    public class Publisher : IPublisher
    {
        private IMongoDatabase _database;
        private IMongoCollection<QueueCollection> _queueCollection;
        private readonly Semaphore _openConnectionSemaphore;

        public Publisher(string url, string database, string collection)
        {
            var client = new MongoClient(url);
            this._database = client.GetDatabase(database);
            this._queueCollection = this._database.GetCollection<QueueCollection>(collection);

            this._openConnectionSemaphore = new Semaphore(
                client.Settings.MaxConnectionPoolSize / 2, 
                client.Settings.MaxConnectionPoolSize / 2);
        }

        public Task SendAsync<T>(T payload, int priority)
        {
            return this.SendAsync(JsonSerializer.Serialize<T>(payload), priority);
        }

        public async Task SendAsync(string payload, int priority = 0)
        {
            var item = new QueueCollection
            {
                Payload = BsonDocument.Parse(payload),
                LastTimeChanged = DateTime.Now,
                Priority = priority,
                WorkerName = string.Empty,
                Processed = false
            };

            await this.ThrottlingPipeline<Task>(() => this._queueCollection.InsertOneAsync(item));
        }

        private async Task ThrottlingPipeline<T>(Func<Task> task)
        {
            this._openConnectionSemaphore.WaitOne();
            try
            {
                 await task
                    .Invoke()
                    .ConfigureAwait(false);
            }
            finally
            {
                this._openConnectionSemaphore.Release();
            }
        }
    }
}
