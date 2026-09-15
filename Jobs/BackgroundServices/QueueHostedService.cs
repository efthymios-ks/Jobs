using Jobs.Configuration;
using Microsoft.Extensions.Hosting;

namespace Jobs.BackgroundServices;

internal sealed class QueueHostedService(
    QueueOptions options,
    IJobChannelStore channelStore,
    IQueueWorker worker
    ) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => worker.RunAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        channelStore.Complete(options.QueueName);

        await base.StopAsync(cancellationToken);
    }
}
