namespace PowerOutageIF.Services;

public record QueuedNotification(
    long ChatId, 
    string Text, 
    bool DisableNotification
);

public interface IBroadcastQueue
{
    void Enqueue(QueuedNotification notification);
    void EnqueueRange(IEnumerable<QueuedNotification> notifications);
    ValueTask<QueuedNotification> DequeueAsync(CancellationToken cancellationToken);
    int PendingCount { get; }
}
