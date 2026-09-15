using Jobs.Abstractions;
using System.Collections.Concurrent;

namespace Jobs.BackgroundServices;

internal abstract class JobDispatcher
{
    private static readonly ConcurrentDictionary<Type, JobDispatcher> _dispatchers = new();

    public abstract Type HandlerType { get; }

    public static JobDispatcher For(Type jobType)
        => _dispatchers.GetOrAdd(
            jobType,
            static type => (JobDispatcher)Activator.CreateInstance(typeof(JobDispatcher<>).MakeGenericType(type))!
        );

    public abstract Task HandleAsync(object handler, IJob job, CancellationToken cancellationToken);
}
