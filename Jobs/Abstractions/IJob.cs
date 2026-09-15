namespace Jobs.Abstractions;

public interface IJob
{
    string QueueName { get; }
}
