using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using WorkerUtilitiesService.Configuration;

namespace WorkerUtilitiesService
{
    public class WorkerLifeCycleService : IWorkerLifeCycleService
    {
        private IMongoDatabase _database;
        private IMongoCollection<WorkerDetails> _workerDetailsCollection;
        private Guid _internalGuid = Guid.Empty;
        private WorkerSettings _workerSettings;

        public WorkerLifeCycleService()
        {
            // Read configuration for the WorkerLifeCycle AppSettings
            this.ReadConfigurations();

            var client = new MongoClient(this._workerSettings.ConnectionString);
            this._database = client.GetDatabase(this._workerSettings.Database);
            this._workerDetailsCollection = this._database.GetCollection<WorkerDetails>(nameof(WorkerDetails));
        }

        public async Task StartWorker()
        {
            this.CreateOrRetrieveInternalWorkerGUID();

            var filter = Builders<WorkerDetails>.Filter.And(
                Builders<WorkerDetails>.Filter.Eq(x => x.WorkerName, this._workerSettings.WorkerName),
                Builders<WorkerDetails>.Filter.Eq(x => x.WorkerInternalId, this._internalGuid)
            );

            var worker = await this._workerDetailsCollection
                .Find<WorkerDetails>(filter)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);

            if (worker == null)
            {
                var workerDetails = new WorkerDetails
                {
                    LastOperationTimeStamp = DateTime.UtcNow,
                    LastOperationMessage = "Worker started",
                    WorkerName = this._workerSettings.WorkerName,
                    WorkerInternalId = this._internalGuid
                };
                await this._workerDetailsCollection.InsertOneAsync(workerDetails);
            }
            else
            {
                var update = Builders<WorkerDetails>.Update
                    .Set(x => x.LastOperationTimeStamp, DateTime.UtcNow);
                await this._workerDetailsCollection
                    .UpdateOneAsync(filter, update)
                    .ConfigureAwait(false);
            }
        }

        public async Task SetWorkerProgress(WorkerProgress workerProgress)
        {
            var filter = Builders<WorkerDetails>.Filter.And(
                Builders<WorkerDetails>.Filter.Eq(x => x.WorkerName, this._workerSettings.WorkerName),
                Builders<WorkerDetails>.Filter.Eq(x => x.WorkerInternalId, this._internalGuid)
            );

            var update = Builders<WorkerDetails>.Update
                .Set(x => x.LastOperationTimeStamp, DateTime.UtcNow)
                .Set(x => x.LastOperationMessage, workerProgress.ProgressMessage);
            await this._workerDetailsCollection
                .UpdateOneAsync(filter, update)
                .ConfigureAwait(false);
        }

        private void ReadConfigurations()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            this._workerSettings = new WorkerSettings();
            configuration.Bind("workerLiveCycleSettings", this._workerSettings);
        }

        // [TODO] [AboimPinto] [15.11.2021]: 
        // This process can be extracted to a service that will be injected in this class.
        // There should be a Bootstrapable class in this componenet that will be catched by the main process. 
        // This class will be able to register the specific objects of the module.
        private void CreateOrRetrieveInternalWorkerGUID()
        {
            var fileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "InternalGuid");
            if (File.Exists(fileName))
            {
                this._internalGuid = new Guid(File.ReadLines(fileName).First());
            }
            else
            {
                using (var w = File.AppendText(fileName))
                {
                    this._internalGuid = Guid.NewGuid();
                    w.WriteLine(this._internalGuid);
                }
            }
        }
    }
}
