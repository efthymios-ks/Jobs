namespace Jobs.BackgroundServices;

internal interface IQueueWorker
{
    Task RunAsync(CancellationToken cancellationToken);
}
