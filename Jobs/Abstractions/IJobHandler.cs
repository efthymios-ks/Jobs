namespace Jobs.Abstractions;

public interface IJobHandler<in TJob>
    where TJob : IJob
{
    Task HandleAsync(TJob job, CancellationToken cancellationToken = default);
}
