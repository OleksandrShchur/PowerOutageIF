using Xunit;
using PowerOutageIF.Services;

namespace PowerOutageIF.Tests;

public class BroadcastQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_MaintainsOrderAndCounts()
    {
        var queue = new BroadcastQueue();

        Assert.Equal(0, queue.PendingCount);

        var notif1 = new QueuedNotification(1001, "Message 1", false);
        var notif2 = new QueuedNotification(1002, "Message 2", true);

        queue.Enqueue(notif1);
        queue.Enqueue(notif2);

        Assert.Equal(2, queue.PendingCount);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var item1 = await queue.DequeueAsync(cts.Token);
        var item2 = await queue.DequeueAsync(cts.Token);

        Assert.Equal(1001, item1.ChatId);
        Assert.Equal("Message 1", item1.Text);
        Assert.False(item1.DisableNotification);

        Assert.Equal(1002, item2.ChatId);
        Assert.Equal("Message 2", item2.Text);
        Assert.True(item2.DisableNotification);

        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task EnqueueRange_AddsMultipleItemsCorrectly()
    {
        var queue = new BroadcastQueue();

        var items = Enumerable.Range(1, 10)
            .Select(i => new QueuedNotification(i, $"Batch {i}", false))
            .ToList();

        queue.EnqueueRange(items);

        Assert.Equal(10, queue.PendingCount);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        for (int i = 1; i <= 10; i++)
        {
            var item = await queue.DequeueAsync(cts.Token);
            Assert.Equal(i, item.ChatId);
        }

        Assert.Equal(0, queue.PendingCount);
    }
}
