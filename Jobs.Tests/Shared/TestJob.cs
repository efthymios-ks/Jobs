namespace Jobs.Tests.Shared;

internal sealed record TestJob(string QueueName) : IJob;
