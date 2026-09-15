namespace Jobs.Configuration;

public sealed class QueueFullException(string queueName)
    : Exception($"Queue '{queueName}' is full.")
{
    public string QueueName { get; } = queueName;
}
