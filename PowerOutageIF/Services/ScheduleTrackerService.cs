using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PowerOutageIF.Data;
using PowerOutageIF.Models;

namespace PowerOutageIF.Services;

public class ScheduleTrackerService : IScheduleTrackerService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<ScheduleTrackerService> _logger;

    // In-memory cache for ultra-fast bot responses: Key = "queueCode_eventDate"
    private readonly ConcurrentDictionary<string, DayScheduleDto> _cache = new();

    public ScheduleTrackerService(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<ScheduleTrackerService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<List<ScheduleChangeResult>> ProcessQueueSchedulesAsync(
        string queueCode, 
        List<DayScheduleDto> apiSchedules, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScheduleChangeResult>();
        if (apiSchedules == null || apiSchedules.Count == 0)
        {
            return results;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Load existing snapshots for this queue
        var existingSnapshots = await dbContext.ScheduleSnapshots
            .Where(s => s.QueueCode == queueCode)
            .ToDictionaryAsync(s => s.EventDate, s => s, cancellationToken);

        foreach (var apiDay in apiSchedules)
        {
            if (string.IsNullOrWhiteSpace(apiDay.EventDate))
                continue;

            var cacheKey = $"{queueCode}_{apiDay.EventDate}";
            _cache[cacheKey] = apiDay;

            var newPeriods = apiDay.Queues.TryGetValue(queueCode, out var periods) ? periods : new List<SchedulePeriodDto>();

            if (!existingSnapshots.TryGetValue(apiDay.EventDate, out var existingSnapshot))
            {
                // Date wasn't present before in DB!
                // If this is for tomorrow or future date, it's a new day publication.
                // If it's the very first time the app ever runs for today, we record it without spamming a "change" unless it's genuinely tomorrow.
                var isTomorrow = IsTomorrowDate(apiDay.EventDate);

                var snapshot = new QueueScheduleSnapshot
                {
                    QueueCode = queueCode,
                    EventDate = apiDay.EventDate,
                    ScheduleApprovedSince = apiDay.ScheduleApprovedSince,
                    CreatedAtFromApi = apiDay.CreatedAt,
                    RawJson = JsonSerializer.Serialize(apiDay),
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                dbContext.ScheduleSnapshots.Add(snapshot);

                // Notify if it's tomorrow's schedule being published
                if (isTomorrow)
                {
                    var changeDesc = BuildChangeDescription(true, new List<SchedulePeriodDto>(), newPeriods);
                    results.Add(new ScheduleChangeResult(
                        HasChanges: true,
                        IsNewDayPublished: true,
                        QueueCode: queueCode,
                        EventDate: apiDay.EventDate,
                        OldApprovedSince: null,
                        NewApprovedSince: apiDay.ScheduleApprovedSince,
                        OldPeriods: new List<SchedulePeriodDto>(),
                        NewPeriods: newPeriods,
                        ChangeDescriptionUa: changeDesc
                    ));

                    dbContext.ChangeLogs.Add(new ScheduleChangeLog
                    {
                        QueueCode = queueCode,
                        EventDate = apiDay.EventDate,
                        DetectedAt = DateTime.UtcNow,
                        ChangeSummary = changeDesc,
                        PreviousApprovedSince = null,
                        NewApprovedSince = apiDay.ScheduleApprovedSince
                    });
                }
            }
            else
            {
                // Day was already known - check if changed!
                var oldDay = DeserializeSnapshot(existingSnapshot.RawJson);
                var oldPeriods = oldDay?.Queues.TryGetValue(queueCode, out var op) == true ? op : new List<SchedulePeriodDto>();

                bool periodsChanged = HavePeriodsChanged(oldPeriods, newPeriods);
                bool approvalChanged = existingSnapshot.ScheduleApprovedSince != apiDay.ScheduleApprovedSince;

                if (periodsChanged || (approvalChanged && !string.IsNullOrEmpty(apiDay.ScheduleApprovedSince)))
                {
                    var changeDesc = BuildChangeDescription(false, oldPeriods, newPeriods);

                    results.Add(new ScheduleChangeResult(
                        HasChanges: true,
                        IsNewDayPublished: false,
                        QueueCode: queueCode,
                        EventDate: apiDay.EventDate,
                        OldApprovedSince: existingSnapshot.ScheduleApprovedSince,
                        NewApprovedSince: apiDay.ScheduleApprovedSince,
                        OldPeriods: oldPeriods,
                        NewPeriods: newPeriods,
                        ChangeDescriptionUa: changeDesc
                    ));

                    // Update snapshot
                    existingSnapshot.ScheduleApprovedSince = apiDay.ScheduleApprovedSince;
                    existingSnapshot.CreatedAtFromApi = apiDay.CreatedAt;
                    existingSnapshot.RawJson = JsonSerializer.Serialize(apiDay);
                    existingSnapshot.LastUpdatedAt = DateTime.UtcNow;

                    dbContext.ChangeLogs.Add(new ScheduleChangeLog
                    {
                        QueueCode = queueCode,
                        EventDate = apiDay.EventDate,
                        DetectedAt = DateTime.UtcNow,
                        ChangeSummary = changeDesc,
                        PreviousApprovedSince = existingSnapshot.ScheduleApprovedSince,
                        NewApprovedSince = apiDay.ScheduleApprovedSince
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return results;
    }

    public async Task<DayScheduleDto?> GetCachedScheduleAsync(string queueCode, bool tomorrow = false, CancellationToken cancellationToken = default)
    {
        var targetDate = GetDateString(tomorrow);
        var cacheKey = $"{queueCode}_{targetDate}";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Try load from DB
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await dbContext.ScheduleSnapshots
            .FirstOrDefaultAsync(s => s.QueueCode == queueCode && s.EventDate == targetDate, cancellationToken);

        if (snapshot != null)
        {
            var deserialized = DeserializeSnapshot(snapshot.RawJson);
            if (deserialized != null)
            {
                _cache[cacheKey] = deserialized;
                return deserialized;
            }
        }

        return null;
    }

    public async Task<List<QueueScheduleSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ScheduleSnapshots.AsNoTracking().ToListAsync(cancellationToken);
    }

    private static bool HavePeriodsChanged(List<SchedulePeriodDto> oldList, List<SchedulePeriodDto> newList)
    {
        if (oldList.Count != newList.Count)
            return true;

        for (int i = 0; i < oldList.Count; i++)
        {
            if (oldList[i].From != newList[i].From ||
                oldList[i].To != newList[i].To ||
                oldList[i].Status != newList[i].Status ||
                oldList[i].ShutdownHours != newList[i].ShutdownHours)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildChangeDescription(bool isNewDay, List<SchedulePeriodDto> oldList, List<SchedulePeriodDto> newList)
    {
        if (isNewDay)
        {
            if (newList.Count == 0)
                return "Графік погодинних вимкнень не застосовується (світло буде цілий день)";

            return string.Join(", ", newList.Select(p => p.ShutdownHours));
        }

        var sb = new System.Text.StringBuilder();

        var oldHours = oldList.Select(p => p.ShutdownHours).ToHashSet();
        var newHours = newList.Select(p => p.ShutdownHours).ToHashSet();

        var added = newList.Where(p => !oldHours.Contains(p.ShutdownHours)).ToList();
        var removed = oldList.Where(p => !newHours.Contains(p.ShutdownHours)).ToList();

        if (added.Count > 0)
        {
            sb.Append("➕ Додано: ").Append(string.Join(", ", added.Select(a => a.ShutdownHours))).Append(". ");
        }

        if (removed.Count > 0)
        {
            sb.Append("➖ Скасовано: ").Append(string.Join(", ", removed.Select(r => r.ShutdownHours))).Append(". ");
        }

        if (added.Count == 0 && removed.Count == 0)
        {
            sb.Append("Оновлено час затвердження або параметри черги.");
        }

        return sb.ToString().Trim();
    }

    private static DayScheduleDto? DeserializeSnapshot(string rawJson)
    {
        try
        {
            return JsonSerializer.Deserialize<DayScheduleDto>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static string GetDateString(bool tomorrow)
    {
        var kyivTime = GetKyivNow();
        var date = tomorrow ? kyivTime.AddDays(1) : kyivTime;
        return date.ToString("dd.MM.yyyy");
    }

    private static bool IsTomorrowDate(string eventDate)
    {
        var tomorrowStr = GetDateString(true);
        return eventDate.Trim() == tomorrowStr;
    }

    public static DateTime GetKyivNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            // Fallback for systems where FLE Standard Time isn't recognized
            return DateTime.UtcNow.AddHours(2);
        }
    }
}
