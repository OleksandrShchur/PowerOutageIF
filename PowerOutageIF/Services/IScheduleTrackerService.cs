using PowerOutageIF.Models;

namespace PowerOutageIF.Services;

public interface IScheduleTrackerService
{
    Task<List<ScheduleChangeResult>> ProcessQueueSchedulesAsync(string queueCode, List<DayScheduleDto> apiSchedules, CancellationToken cancellationToken = default);
    Task<DayScheduleDto?> GetCachedScheduleAsync(string queueCode, bool tomorrow = false, CancellationToken cancellationToken = default);
    Task<List<QueueScheduleSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default);
}
