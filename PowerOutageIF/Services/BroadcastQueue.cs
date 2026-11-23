using System.Threading.Channels;

namespace PowerOutageIF.Services;

public class BroadcastQueue : IBroadcastQueue
{
    private readonly Channel<QueuedNotification> _channel;

    public BroadcastQueue()
    {
        var options = new BoundedChannelOptions(50_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<QueuedNotification>(options);
    }

    public int PendingCount => _channel.Reader.Count;

    public void Enqueue(QueuedNotification notification)
    {
        _channel.Writer.TryWrite(notification);
    }

    public void EnqueueRange(IEnumerable<QueuedNotification> notifications)
    {
        foreach (var notif in notifications)
        {
            _channel.Writer.TryWrite(notif);
        }
    }

    public ValueTask<QueuedNotification> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
