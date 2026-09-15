namespace Jobs.Tests.Shared;

internal sealed class OrderedJobHandler : IJobHandler<OrderedJob>
{
    public Task HandleAsync(OrderedJob job, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
