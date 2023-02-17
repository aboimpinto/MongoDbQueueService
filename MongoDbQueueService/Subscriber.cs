using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDbQueueService.Configuration;

namespace MongoDbQueueService
{
    public class Subscriber : ISubscriber
    {
        private string _workerName;
        private bool _deleteOnAcknowledge;
        private MongoClient _mongoClient;
        private IMongoDatabase _database;
        private string _collection;
        private IMongoCollection<QueueCollection> _queueCollection;

        public Subscriber(bool debug = false)
        {
            var settingsFolder = "/settings";

            if (debug)
            {
                Console.WriteLine("--> DEBUG ON");
                settingsFolder = Directory.GetCurrentDirectory();

                Console.WriteLine($"--> settingFolder: {settingsFolder}");
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(settingsFolder)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var subscriberSettings = new SubscriberSettings();
            configuration.Bind("SubscriberSettings", subscriberSettings);

            if (debug)
            {
                Console.WriteLine($"--> Subscriber: connectionString: {subscriberSettings.ConnectionString}");
                Console.WriteLine($"--> Subscriber: database: {subscriberSettings.Database}");
                Console.WriteLine($"--> Subscriber: queue: {subscriberSettings.Queue}");
                Console.WriteLine($"--> Subscriber: worker: {subscriberSettings.WorkerName}");
            }

            if (
                string.IsNullOrEmpty(subscriberSettings.ConnectionString) || 
                string.IsNullOrEmpty(subscriberSettings.Database) || 
                string.IsNullOrEmpty(subscriberSettings.Queue) ||
                string.IsNullOrEmpty(subscriberSettings.WorkerName))
            {
                Console.WriteLine("Could not read properly the configuration file");
                Console.WriteLine("=====================================================");
                Console.WriteLine($"settingFolder -> {settingsFolder}");

                var settingFile = Path.Combine(settingsFolder, "appsettings.json");
                Console.WriteLine($"settingsFile exists: {File.Exists(settingFile)}");

                throw new InvalidOperationException("Something wrong wih the configuration file");
            }

            this.ConnectDatabase(
                subscriberSettings.ConnectionString,
                subscriberSettings.Database,
                subscriberSettings.Queue,
                subscriberSettings.WorkerName);
        }

        public Subscriber(SubscriberSettings settings)
        {
            this.ConnectDatabase(
                settings.ConnectionString,
                settings.Database,
                settings.Queue,
                settings.WorkerName);
        }

        public Subscriber(string url, string database, string collection, string workerName, bool deleteOnAcknowledge = false)
        {
            this.ConnectDatabase(
                url,
                database,
                collection,
                workerName,
                deleteOnAcknowledge);
        }

        public IObservable<SubscriptionResult<T>> SubscribeQueueCollection<T>(CancellationToken token)
        {
            var scheduleInstance = ThreadPoolScheduler.Instance;

            return Observable.Create<SubscriptionResult<T>>(item =>
            {
                var disposable = Observable
                    .Interval(TimeSpan.FromSeconds(1), scheduleInstance)
                    .Subscribe(async _ => 
                    {
                        var sortOptions = Builders<QueueCollection>
                            .Sort.Ascending("LastTimeChanged");

                        var sort = new FindOptions<QueueCollection>
                        {
                            Sort = sortOptions
                        };

                        var itemProcessingForWorker = await this._queueCollection
                            .FindAsync(x => x.WorkerName == this._workerName, sort)
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


                        QueueCollection itemFromQueue;

                        try
                        {
                            var options = new FindOneAndUpdateOptions<QueueCollection>
                            {
                                Sort = sortOptions, 
                                ReturnDocument = ReturnDocument.After,
                                Projection = Builders<QueueCollection>.Projection.Include(x => x.Id)
                            };

                            var update = Builders<QueueCollection>.Update.Set(x => x.WorkerName, this._workerName);

                            var updateResult = await this._queueCollection.FindOneAndUpdateAsync(
                                filter, 
                                update, 
                                options);

                            if (updateResult == null)
                            {
                                return;
                            }

                             itemFromQueue = updateResult;
                        }
                        catch
                        {
                            return;
                        }

                        try
                        {
                            var jsonFromDocument = itemFromQueue.Payload.ToJson();
                            var deserializedObject = JsonSerializer.Deserialize<T>(jsonFromDocument);

                            var subscriptionResult = new SubscriptionResult<T>();
                            subscriptionResult.ProcessSucessful = false;
                            subscriptionResult.Payload = deserializedObject;

                            item.OnNext(subscriptionResult);
                            
                            if (subscriptionResult.ProcessSucessful)
                            {
                                if (this._deleteOnAcknowledge)
                                {
                                    await this.AcknowledgeAndDelete()
                                        .ConfigureAwait(false);
                                }
                                else
                                {
                                    await this.AcknowledgeWithoutDelete(
                                            JsonSerializer.Serialize<T>(subscriptionResult.Payload), 
                                            subscriptionResult.ProcessSucessful)
                                        .ConfigureAwait(false);    
                                }
                            }
                            else 
                            {
                                await this.AcknowledgeWithoutDelete(
                                        JsonSerializer.Serialize<T>(subscriptionResult.Payload), 
                                        subscriptionResult.ProcessSucessful)
                                    .ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                            throw new InvalidOperationException($"Was not possible to process payload: {itemFromQueue.Payload}");
                        }
                    });
                token.Register(() => disposable.Dispose());

                return Disposable.Empty;
            });
        }

        private void ConnectDatabase(
            string url, 
            string database, 
            string collection, 
            string workerName, 
            bool deleteOnAcknowledge = false)
        {
            this._mongoClient = new MongoClient(url);
            this._database = this._mongoClient.GetDatabase(database);
            this._collection = collection;
            this._workerName = workerName;
            this._deleteOnAcknowledge = deleteOnAcknowledge;

            this._queueCollection = this._database.GetCollection<QueueCollection>(collection);

            var lastTimeChangedIndex = Builders<QueueCollection>.IndexKeys.Ascending(x => x.LastTimeChanged);
            this._queueCollection.Indexes.CreateOne(new CreateIndexModel<QueueCollection>(lastTimeChangedIndex));

            var workerNameIndex = Builders<QueueCollection>.IndexKeys.Ascending(x => x.WorkerName);
            this._queueCollection.Indexes.CreateOne(new CreateIndexModel<QueueCollection>(workerNameIndex));

            var processedIndex = Builders<QueueCollection>.IndexKeys.Ascending(x => x.Processed);
            this._queueCollection.Indexes.CreateOne(new CreateIndexModel<QueueCollection>(processedIndex));
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

        private async Task AcknowledgeWithoutDelete(string payload, bool processedSuccessful)
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
                .Set(x => x.Processed, processedSuccessful)
                .Set(x => x.LastTimeChanged, DateTime.UtcNow)
                .Set(x => x.Payload, BsonDocument.Parse(payload));

            await this._queueCollection
                .UpdateOneAsync(filter, update)
                .ConfigureAwait(false);
        }
    }
}
