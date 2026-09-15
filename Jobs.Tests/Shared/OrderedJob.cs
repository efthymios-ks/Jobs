namespace Jobs.Tests.Shared;

internal sealed record OrderedJob(string QueueName, int Sequence) : IJob;
