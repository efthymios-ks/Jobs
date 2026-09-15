using Jobs.Abstractions;

namespace Jobs.BackgroundServices;

internal sealed class JobDispatcher<TJob> : JobDispatcher
    where TJob : IJob
{
    public override Type HandlerType => typeof(IJobHandler<TJob>);

    public override Task HandleAsync(object handler, IJob job, CancellationToken cancellationToken)
        => ((IJobHandler<TJob>)handler).HandleAsync((TJob)job, cancellationToken);
}
