using Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Jobs.Tests.BackgroundServices;

public sealed class QueueWorkerTests
{
    private static (QueueWorker Worker, JobChannelStore Store) Create(
        QueueOptions options,
        IServiceCollection? services = null)
    {
        var store = new JobChannelStore([options]);
        var sp = (services ?? new ServiceCollection()).BuildServiceProvider();
        var worker = new QueueWorker(options, store, sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<QueueWorker>.Instance);
        return (worker, store);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerIsRegistered_ShouldDispatchTheJob()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler<TestJob>>();
        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<TestJob>>(handler);

        var (worker, _) = Create(new QueueOptions { QueueName = "q" }, services);
        var job = new TestJob("q");

        // Act
        await worker.ExecuteAsync(job, CancellationToken.None);

        // Assert
        await handler.Received(1).HandleAsync(job, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHandlerIsRegistered_ShouldNotThrow()
    {
        // Arrange
        var (worker, _) = Create(new QueueOptions { QueueName = "q" });
        var job = new TestJob("q");

        // Act & Assert
        await worker.ExecuteAsync(job, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WhenAJobHasNoHandler_ShouldKeepProcessingTheRest()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler<OrderedJob>>();
        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<OrderedJob>>(handler);

        var options = new QueueOptions { QueueName = "q", MaxConcurrency = 1 };
        var (worker, store) = Create(options, services);

        var bus = new JobBus(store);
        await bus.PublishAsync(new TestJob("q"));
        await bus.PublishAsync(new OrderedJob("q", 1));
        store.Complete("q");

        // Act
        await worker.RunAsync(CancellationToken.None);

        // Assert
        await handler.Received(1).HandleAsync(Arg.Any<OrderedJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenCancelledWithJobsStillQueued_ShouldDrainThem()
    {
        // Arrange
        var handled = 0;
        var handler = Substitute.For<IJobHandler<TestJob>>();
        handler
            .HandleAsync(Arg.Any<TestJob>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref handled);
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<TestJob>>(handler);
        var (worker, store) = Create(new QueueOptions { QueueName = "q" }, services);

        var bus = new JobBus(store);
        await bus.PublishAsync(new TestJob("q"));
        await bus.PublishAsync(new TestJob("q"));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await worker.RunAsync(cts.Token);

        // Assert
        Assert.Equal(2, handled);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerThrowsSynchronously_ShouldNotWrapTheException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<TestJob>>(new ThrowingJobHandler());
        var (worker, _) = Create(new QueueOptions { QueueName = "q" }, services);

        // Act & Assert
        await worker.ExecuteAsync(new TestJob("q"), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerThrows_ShouldNotPropagateException()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler<TestJob>>();
        handler
            .HandleAsync(Arg.Any<TestJob>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<TestJob>>(handler);
        var (worker, _) = Create(new QueueOptions { QueueName = "q" }, services);

        // Act & Assert (no throw)
        await worker.ExecuteAsync(new TestJob("q"), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerThrowsOperationCancelled_ShouldPropagate()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handler = Substitute.For<IJobHandler<TestJob>>();
        handler
            .HandleAsync(Arg.Any<TestJob>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));

        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<TestJob>>(handler);
        var (worker, _) = Create(new QueueOptions { QueueName = "q" }, services);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => worker.ExecuteAsync(new TestJob("q"), cts.Token));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ShouldComplete()
    {
        // Arrange
        var (worker, _) = Create(new QueueOptions { QueueName = "q" });
        using var cts = new CancellationTokenSource();

        // Act
        var task = worker.RunAsync(cts.Token);
        await cts.CancelAsync();

        // Assert — task completes and does not hang
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completed);
    }

    [Fact]
    public async Task RunAsync_WhenJobsArePublished_ShouldProcessThemInFifoOrder()
    {
        // Arrange
        var processed = new List<int>();
        var handler = Substitute.For<IJobHandler<OrderedJob>>();
        handler
            .HandleAsync(Arg.Any<OrderedJob>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                processed.Add(ci.Arg<OrderedJob>().Sequence);
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<OrderedJob>>(handler);
        var options = new QueueOptions { QueueName = "q", MaxConcurrency = 1 };
        var (worker, store) = Create(options, services);

        var bus = new JobBus(store);
        await bus.PublishAsync(new OrderedJob("q", 1));
        await bus.PublishAsync(new OrderedJob("q", 2));
        await bus.PublishAsync(new OrderedJob("q", 3));
        store.Complete("q");

        // Act
        await worker.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal([1, 2, 3], processed);
    }

    [Fact]
    public async Task RunAsync_WhenConcurrencyIsN_ShouldProcessJobsInParallel()
    {
        // Arrange
        const int concurrency = 3;
        var startedCount = 0;
        var allStartedTcs = new TaskCompletionSource();
        var blockTcs = new TaskCompletionSource();

        var handler = Substitute.For<IJobHandler<TestJob>>();
        handler
            .HandleAsync(Arg.Any<TestJob>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (Interlocked.Increment(ref startedCount) >= concurrency)
                {
                    allStartedTcs.TrySetResult();
                }

                await blockTcs.Task;
            });

        var services = new ServiceCollection();
        services.AddSingleton<IJobHandler<TestJob>>(handler);
        var options = new QueueOptions { QueueName = "q", MaxConcurrency = concurrency };
        var (worker, store) = Create(options, services);

        var bus = new JobBus(store);
        for (var i = 0; i < concurrency; i++)
        {
            await bus.PublishAsync(new TestJob("q"));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var runTask = worker.RunAsync(cts.Token);
        await allStartedTcs.Task.WaitAsync(cts.Token);

        // Assert — all N consumers are running simultaneously
        Assert.Equal(concurrency, startedCount);

        // Cleanup
        blockTcs.SetResult();
        await cts.CancelAsync();
    }
}
