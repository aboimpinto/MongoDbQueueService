using System.Threading.Tasks;

namespace WorkerUtilitiesService
{
    public interface IWorkerLifeCycleService
    {
        Task StartWorker();

        Task SetWorkerProgress(WorkerProgress workerProgress);
    }
}
