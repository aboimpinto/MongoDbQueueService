namespace WorkerUtilitiesService
{
    public class WorkerProgress
    {
        public string ProgressMessage { get; }

        public WorkerProgress(string progressMessage)
        {
            this.ProgressMessage = progressMessage;
        }
    }
}