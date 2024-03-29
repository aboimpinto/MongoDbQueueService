using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbQueueService.Configuration;

namespace MongoDbQueueService
{
    public class Publisher : IPublisher
    {
        private IMongoDatabase _database;
        private IMongoCollection<QueueCollection> _queueCollection;
        private Semaphore _openConnectionSemaphore;

        public Publisher(bool debug = false)
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

            var publishSettings = new PublisherSettings();
            configuration.Bind("publisherSettings", publishSettings);

            if (debug)
            {
                Console.WriteLine($"--> Publisher: connectionString: {publishSettings.ConnectionString}");
                Console.WriteLine($"--> Publisher: database: {publishSettings.Database}");
                Console.WriteLine($"--> Publisher: queue: {publishSettings.Queue}");
            }

            if (
                string.IsNullOrEmpty(publishSettings.ConnectionString) || 
                string.IsNullOrEmpty(publishSettings.Database) || 
                string.IsNullOrEmpty(publishSettings.Queue))
            {
                Console.WriteLine("Could not read properly the configuration file (Publisher)");
                Console.WriteLine("=====================================================");
                Console.WriteLine($"settingFolder -> {settingsFolder}");

                var settingFile = Path.Combine(settingsFolder, "appsettings.json");
                Console.WriteLine($"settingsFile exists: {File.Exists(settingFile)}");
            }

            this.ConnectDatabase(
                publishSettings.ConnectionString,
                publishSettings.Database,
                publishSettings.Queue);
        }

        public Publisher(PublisherSettings settings)
        {
            this.ConnectDatabase(
                settings.ConnectionString,
                settings.Database,
                settings.Queue);
        }

        public Publisher(string url, string database, string collection)
        {
            this.ConnectDatabase(
                url, 
                database, 
                collection);
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
                LastTimeChanged = DateTime.UtcNow,
                Priority = priority,
                WorkerName = string.Empty,
                Processed = false
            };

            await this.ThrottlingPipeline<Task>(() => this._queueCollection.InsertOneAsync(item));
        }

        private void ConnectDatabase(string url, string database, string collection)
        {
            var client = new MongoClient(url);
            this._database = client.GetDatabase(database);
            this._queueCollection = this._database.GetCollection<QueueCollection>(collection);

            this._openConnectionSemaphore = new Semaphore(
                client.Settings.MaxConnectionPoolSize / 2, 
                client.Settings.MaxConnectionPoolSize / 2);

            var lastTimeChangedIndex = Builders<QueueCollection>.IndexKeys.Ascending(x => x.LastTimeChanged);
            this._queueCollection.Indexes.CreateOne(new CreateIndexModel<QueueCollection>(lastTimeChangedIndex));

            var workerNameIndex = Builders<QueueCollection>.IndexKeys.Ascending(x => x.WorkerName);
            this._queueCollection.Indexes.CreateOne(new CreateIndexModel<QueueCollection>(workerNameIndex));

            var processedIndex = Builders<QueueCollection>.IndexKeys.Ascending(x => x.Processed);
            this._queueCollection.Indexes.CreateOne(new CreateIndexModel<QueueCollection>(processedIndex));
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
