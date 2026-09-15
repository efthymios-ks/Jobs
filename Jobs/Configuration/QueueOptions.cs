namespace Jobs.Configuration;

public sealed class QueueOptions
{
    public string QueueName { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 1;
    public int? MaxCapacity { get; set; }
    public QueueFullBehavior FullBehavior { get; set; } = QueueFullBehavior.Wait;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new InvalidOperationException($"{nameof(QueueName)} must not be empty.");
        }

        if (MaxConcurrency <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxConcurrency)} must be greater than zero.");
        }

        if (MaxCapacity is <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxCapacity)} must be greater than zero when specified.");
        }
    }
}
