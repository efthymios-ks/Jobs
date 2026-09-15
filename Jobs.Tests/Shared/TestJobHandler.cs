namespace Jobs.Tests.Shared;

internal sealed class TestJobHandler : IJobHandler<TestJob>
{
    public Task HandleAsync(TestJob job, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
