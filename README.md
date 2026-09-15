# Jobs

An in-memory queue worker for .NET. Publish typed jobs to named queues; background workers dispatch them to the right handler inside a DI scope.

## Core types

| Type | Role |
|---|---|
| `IJob` | Marker interface — carries `QueueName` |
| `IJobHandler<TJob>` | Processes a specific job type |
| `IJobBus` | Publishes jobs to a named queue |
| `QueueOptions` | Per-queue configuration |
| `QueueFullBehavior` | `Wait` (default) or `Throw` when a bounded queue is full |
| `QueueFullException` | Thrown by `PublishAsync` when the queue is full and behaviour is `Throw` |

## Namespaces

```csharp
using Jobs.Abstractions;   // IJob, IJobHandler<T>, IJobBus
using Jobs.Configuration;  // QueueOptions, QueueFullBehavior, QueueFullException
using Jobs.Extensions;     // AddJobQueue, AddJobHandlers*
```

## Setup

```csharp
builder.Services.AddJobQueue(options =>
{
    options.QueueName = "orders";
    options.MaxConcurrency = 4;
    options.MaxCapacity = 1000;
    options.FullBehavior = QueueFullBehavior.Throw;
});

// Register all IJobHandler<T> implementations in the given assembly
builder.Services.AddJobHandlersFromAssemblyContainingType<PlaceOrderHandler>();
```

Call `AddJobQueue` once per queue. Each call registers a dedicated `BackgroundService` consumer.

Handlers can also be registered individually or from multiple assemblies:

```csharp
// From multiple assemblies
builder.Services.AddJobHandlersFromAssemblies(typeof(PlaceOrderHandler).Assembly, typeof(SendEmailHandler).Assembly);
```

## Defining a job

```csharp
public sealed record PlaceOrderJob(Guid OrderId) : IJob
{
    public string QueueName 
        => "orders";
}
```

## Handling a job

```csharp
public sealed class PlaceOrderHandler : IJobHandler<PlaceOrderJob>
{
    public async Task HandleAsync(
        PlaceOrderJob job, 
        CancellationToken cancellationToken = default
    )
    {
        // Process the order
    }
}
```

Handlers are resolved per job from a fresh DI scope — scoped services (e.g. `DbContext`) are safe to inject.

## Publishing

```csharp
public sealed class OrderService(IJobBus bus)
{
    public async Task PlaceOrderAsync(Guid orderId)
        => await bus.PublishAsync(new PlaceOrderJob(orderId));
}
```

## Concurrency and capacity

Each queue runs `MaxConcurrency` consumer loops in parallel. Jobs are processed in FIFO order within a single loop; across multiple loops order is not guaranteed.

When `MaxCapacity` is set:
- `FullBehavior = Wait` — `PublishAsync` awaits until space is available (default).
- `FullBehavior = Throw` — `PublishAsync` throws `QueueFullException` immediately.

Omitting `MaxCapacity` creates an unbounded queue; `FullBehavior` is ignored.

## Failure handling

Handler exceptions are caught, logged, and swallowed — the consumer loop keeps running. `OperationCanceledException` is not swallowed; it propagates and stops the consumer.

Missing handlers (no `IJobHandler<TJob>` registered) are logged as an error and throw `InvalidOperationException`.

## Multiple queues

```csharp
builder.Services.AddJobQueue(options => 
{ 
    options.QueueName = "orders"; 
    options.MaxConcurrency = 4; 
});

builder.Services.AddJobQueue(options => 
{ 
    options.QueueName = "emails"; 
    options.MaxConcurrency = 1; 
});
```
