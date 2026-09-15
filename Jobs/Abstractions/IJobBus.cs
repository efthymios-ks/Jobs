namespace Jobs.Abstractions;

public interface IJobBus
{
    ValueTask PublishAsync(IJob job, CancellationToken cancellationToken = default);
}
