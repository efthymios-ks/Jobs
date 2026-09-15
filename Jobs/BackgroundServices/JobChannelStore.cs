using Jobs.Abstractions;
using Jobs.Configuration;
using System.Threading.Channels;

namespace Jobs.BackgroundServices;

internal sealed class JobChannelStore : IJobChannelStore
{
    private readonly Dictionary<string, Channel<IJob>> _channels;
    private readonly Dictionary<string, QueueOptions> _options;

    internal JobChannelStore(IEnumerable<QueueOptions> options)
    {
        var optionsArray = options.ToArray();
        _channels = optionsArray.ToDictionary(optionsElement => optionsElement.QueueName, CreateChannel);
        _options = optionsArray.ToDictionary(optionsElement => optionsElement.QueueName);
    }

    public ChannelReader<IJob> GetReader(string queueName)
    {
        if (!_channels.TryGetValue(queueName, out var channel))
        {
            throw new InvalidOperationException($"No queue registered with name '{queueName}'.");
        }

        return channel.Reader;
    }

    public void Complete(string queueName)
    {
        if (_channels.TryGetValue(queueName, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
    private static Channel<IJob> CreateChannel(QueueOptions options)
    {
        if (options.MaxCapacity is int capacity)
        {
            return Channel.CreateBounded<IJob>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        return Channel.CreateUnbounded<IJob>();
    }

    internal ChannelWriter<IJob> GetWriter(string queueName)
    {
        if (!_channels.TryGetValue(queueName, out var channel))
        {
            throw new InvalidOperationException($"No queue registered with name '{queueName}'.");
        }

        return channel.Writer;
    }

    internal QueueOptions GetOptions(string queueName)
    {
        if (!_options.TryGetValue(queueName, out var opts))
        {
            throw new InvalidOperationException($"No queue registered with name '{queueName}'.");
        }

        return opts;
    }
}
