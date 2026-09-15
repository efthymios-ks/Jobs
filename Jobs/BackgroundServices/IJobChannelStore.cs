using Jobs.Abstractions;
using System.Threading.Channels;

namespace Jobs.BackgroundServices;

internal interface IJobChannelStore
{
    ChannelReader<IJob> GetReader(string queueName);
    void Complete(string queueName);
}
