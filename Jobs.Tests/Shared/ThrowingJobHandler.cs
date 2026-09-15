namespace Jobs.Tests.Shared;

internal sealed class ThrowingJobHandler : IJobHandler<TestJob>
{
    public Task HandleAsync(TestJob job, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("boom");
}
