using Jobs.Abstractions;
using Jobs.Configuration;

namespace Jobs.BackgroundServices;

internal sealed class JobBus(JobChannelStore store) : IJobBus
{
    public async ValueTask PublishAsync(IJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var queueName = job.QueueName;
        var writer = store.GetWriter(queueName);
        var options = store.GetOptions(queueName);

        if (options.MaxCapacity is not null
            && options.FullBehavior is QueueFullBehavior.Throw
        )
        {
            if (!writer.TryWrite(job))
            {
                throw new QueueFullException(queueName);
            }

            return;
        }

        await writer.WriteAsync(job, cancellationToken);
    }
}
