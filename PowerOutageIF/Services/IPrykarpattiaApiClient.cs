using PowerOutageIF.Models;

namespace PowerOutageIF.Services;

public interface IPrykarpattiaApiClient
{
    Task<List<QueueInfoItem>> GetQueuesAsync(CancellationToken cancellationToken = default);
    Task<List<DayScheduleDto>> GetScheduleByQueueAsync(string queueCode, CancellationToken cancellationToken = default);
}
