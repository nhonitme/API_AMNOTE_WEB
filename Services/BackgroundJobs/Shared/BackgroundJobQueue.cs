using System.Threading.Channels;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

public sealed class BackgroundJobQueue<T>
{
    private readonly Channel<T> _channel;

    public BackgroundJobQueue(int? capacity = null)
    {
        if (capacity.HasValue && capacity.Value > 0)
        {
            _channel = Channel.CreateBounded<T>(
                new BoundedChannelOptions(capacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                });
            return;
        }

        _channel = Channel.CreateUnbounded<T>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
    }

    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
