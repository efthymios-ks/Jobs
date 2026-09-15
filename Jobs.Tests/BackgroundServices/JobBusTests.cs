namespace Jobs.Tests.BackgroundServices;

public sealed class JobBusTests
{
    private static JobChannelStore CreateStore(params QueueOptions[] options)
        => new(options);

    private static JobBus CreateBus(JobChannelStore store)
        => new(store);

    [Fact]
    public async Task PublishAsync_WhenJobIsNull_ShouldThrow()
    {
        // Arrange
        var store = CreateStore(new QueueOptions { QueueName = "q" });
        var bus = CreateBus(store);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => bus.PublishAsync(null!).AsTask());
    }

    [Fact]
    public async Task PublishAsync_WhenQueueIsNotRegistered_ShouldThrow()
    {
        // Arrange
        var store = CreateStore(new QueueOptions { QueueName = "q" });
        var bus = CreateBus(store);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync(new TestJob("other")).AsTask());
    }

    [Fact]
    public async Task PublishAsync_WhenQueueIsUnbounded_ShouldEnqueueSuccessfully()
    {
        // Arrange
        var store = CreateStore(new QueueOptions { QueueName = "q" });
        var bus = CreateBus(store);
        var job = new TestJob("q");

        // Act
        await bus.PublishAsync(job);

        // Assert
        var reader = store.GetReader("q");
        Assert.True(reader.TryRead(out var dequeued));
        Assert.Same(job, dequeued);
    }

    [Fact]
    public async Task PublishAsync_WhenBoundedQueueIsFull_AndBehaviorIsThrow_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxCapacity = 1, FullBehavior = QueueFullBehavior.Throw };
        var store = CreateStore(options);
        var bus = CreateBus(store);
        await bus.PublishAsync(new TestJob("q")); // fills the queue

        // Act
        var ex = await Assert.ThrowsAsync<QueueFullException>(() => bus.PublishAsync(new TestJob("q")).AsTask());

        // Assert
        Assert.Equal("q", ex.QueueName);
        Assert.Contains("q", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_WhenBoundedQueueHasSpace_AndBehaviorIsThrow_ShouldSucceed()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxCapacity = 2, FullBehavior = QueueFullBehavior.Throw };
        var store = CreateStore(options);
        var bus = CreateBus(store);

        // Act & Assert (no throw — capacity is not reached)
        await bus.PublishAsync(new TestJob("q"));
        await bus.PublishAsync(new TestJob("q"));
    }

    [Fact]
    public async Task PublishAsync_WhenUnboundedQueue_AndBehaviorIsThrow_ShouldNeverThrow()
    {
        // Arrange — Throw only applies when MaxCapacity is set
        var options = new QueueOptions { QueueName = "q", MaxCapacity = null, FullBehavior = QueueFullBehavior.Throw };
        var store = CreateStore(options);
        var bus = CreateBus(store);

        // Act & Assert (unbounded queues cannot be full)
        for (var i = 0; i < 100; i++)
        {
            await bus.PublishAsync(new TestJob("q"));
        }
    }

    [Fact]
    public async Task PublishAsync_WhenMultipleQueuesRegistered_ShouldRouteToCorrectQueue()
    {
        // Arrange
        var store = CreateStore(
            new() { QueueName = "a" },
            new() { QueueName = "b" });
        var bus = CreateBus(store);
        var jobA = new TestJob("a");
        var jobB = new TestJob("b");

        // Act
        await bus.PublishAsync(jobA);
        await bus.PublishAsync(jobB);

        // Assert
        var provider = store;
        Assert.True(provider.GetReader("a").TryRead(out var dequeuedA));
        Assert.True(provider.GetReader("b").TryRead(out var dequeuedB));
        Assert.Same(jobA, dequeuedA);
        Assert.Same(jobB, dequeuedB);
    }

    private sealed record TestJob(string QueueName) : IJob;
}
