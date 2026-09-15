using Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Reflection;

namespace Jobs.Tests.Extensions;

public sealed class DependencyInjectionTests
{
    private static readonly Assembly _testAssembly = typeof(TestJobHandler).Assembly;

    [Fact]
    public void AddJobHandlersFromAssemblies_WhenAssemblyHasHandlers_ShouldRegisterAll()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJobHandlersFromAssemblies(_testAssembly);

        // Assert
        using var scope = services.BuildServiceProvider().CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IJobHandler<TestJob>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IJobHandler<OrderedJob>>());
    }

    [Fact]
    public void AddJobHandlersFromAssemblies_WhenCalledTwice_ShouldBeIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJobHandlersFromAssemblies(_testAssembly);
        var countAfterFirst = services.Count;

        // Act
        services.AddJobHandlersFromAssemblies(_testAssembly);

        // Assert
        Assert.Equal(countAfterFirst, services.Count);
    }

    [Fact]
    public void AddJobHandlersFromAssemblies_WhenHandlerAlreadyRegistered_ShouldNotOverride()
    {
        // Arrange
        var services = new ServiceCollection();
        var existing = new TestJobHandler();
        services.AddScoped<IJobHandler<TestJob>>(_ => existing);

        // Act
        services.AddJobHandlersFromAssemblies(_testAssembly);

        // Assert
        using var scope = services.BuildServiceProvider().CreateScope();
        Assert.Same(existing, scope.ServiceProvider.GetRequiredService<IJobHandler<TestJob>>());
    }

    [Fact]
    public void AddJobHandlersFromAssemblyContainingType_WhenGeneric_ShouldScanMemberAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJobHandlersFromAssemblyContainingType<TestJobHandler>();

        // Assert
        using var scope = services.BuildServiceProvider().CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IJobHandler<TestJob>>());
    }

    [Fact]
    public void AddJobHandlersFromAssemblyContainingType_WhenNonGeneric_ShouldScanMemberAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJobHandlersFromAssemblyContainingType(typeof(TestJobHandler));

        // Assert
        using var scope = services.BuildServiceProvider().CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IJobHandler<TestJob>>());
    }

    [Fact]
    public void AddJobHandlersFromAssemblyContainingType_WhenTypeIsNull_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => services.AddJobHandlersFromAssemblyContainingType(null!));
    }

    [Fact]
    public void AddJobQueue_WhenAQueueIsRegistered_ShouldResolveTheBus()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddJobQueue(queue => queue.QueueName = "emails");

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IJobBus>());
    }

    [Fact]
    public void AddJobQueue_WhenAQueueIsRegistered_ShouldResolveItsHostedService()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddJobQueue(queue => queue.QueueName = "emails");

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IHostedService>());
    }

    [Fact]
    public void AddJobQueue_WhenTwoQueuesAreRegistered_ShouldGiveEachOneItsOwnHostedService()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddJobQueue(queue => queue.QueueName = "emails");
        services.AddJobQueue(queue => queue.QueueName = "reports");

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.Equal(2, provider.GetServices<IHostedService>().Count());
    }

    [Fact]
    public async Task AddJobQueue_WhenAJobIsPublished_ShouldReachItsHandler()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler<TestJob>>();
        var services = CreateServices();
        services.AddScoped<IJobHandler<TestJob>>(_ => handler);
        services.AddJobQueue(queue => queue.QueueName = "emails");

        using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);
        await provider.GetRequiredService<IJobBus>().PublishAsync(new TestJob("emails"));
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        await handler.Received(1).HandleAsync(Arg.Any<TestJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AddJobQueue_WhenOptionsAreInvalid_ShouldThrow()
    {
        // Arrange
        var services = CreateServices();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => services.AddJobQueue(queue => queue.QueueName = string.Empty));
    }

    [Fact]
    public void AddJobQueue_WhenConfigureIsNull_ShouldThrow()
    {
        // Arrange
        var services = CreateServices();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddJobQueue(null!));
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<QueueWorker>>(NullLogger<QueueWorker>.Instance);

        return services;
    }
}
