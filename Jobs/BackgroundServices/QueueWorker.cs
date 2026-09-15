using Jobs.Abstractions;
using Jobs.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Jobs.BackgroundServices;

internal sealed class QueueWorker(
    QueueOptions options,
    IJobChannelStore channelStore,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<QueueWorker> logger
    ) : IQueueWorker
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        options.Validate();

        var reader = channelStore.GetReader(options.QueueName);
        var consumers = Enumerable
            .Range(0, options.MaxConcurrency)
            .Select(_ => ConsumeAsync(reader, cancellationToken));

        return Task.WhenAll(consumers);
    }

    private async Task ConsumeAsync(ChannelReader<IJob> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var job in reader.ReadAllAsync(cancellationToken))
            {
                await ExecuteAsync(job, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            while (reader.TryRead(out var job))
            {
                await ExecuteAsync(job, CancellationToken.None);
            }
        }
    }

    public async Task ExecuteAsync(IJob job, CancellationToken cancellationToken)
    {
        var dispatcher = JobDispatcher.For(job.GetType());

        using var serviceScope = serviceScopeFactory.CreateScope();
        var handler = serviceScope.ServiceProvider.GetService(dispatcher.HandlerType);

        if (handler is null)
        {
            logger.LogError(
                "No handler registered for job type {JobType}.",
                job.GetType().Name
            );

            return;
        }

        try
        {
            await dispatcher.HandleAsync(handler, job, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Unhandled exception in handler for job type {JobType}.",
                job.GetType().Name
            );
        }
    }
}
