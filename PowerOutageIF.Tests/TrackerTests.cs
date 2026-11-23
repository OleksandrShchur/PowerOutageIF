using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using PowerOutageIF.Data;
using PowerOutageIF.Models;
using PowerOutageIF.Services;

namespace PowerOutageIF.Tests;

public class TrackerTests
{
    private class InMemoryDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public InMemoryDbContextFactory(string dbName)
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);
    }

    [Fact]
    public async Task ProcessQueueSchedulesAsync_WhenScheduleChangesDuringDay_DetectsChangeAndLogs()
    {
        var dbFactory = new InMemoryDbContextFactory(Guid.NewGuid().ToString());
        var tracker = new ScheduleTrackerService(dbFactory, NullLogger<ScheduleTrackerService>.Instance);

        var today = ScheduleTrackerService.GetKyivNow().ToString("dd.MM.yyyy");

        // Initial schedule
        var initialSchedule = new List<DayScheduleDto>
        {
            new()
            {
                EventDate = today,
                ScheduleApprovedSince = $"{today} 08:00",
                Queues = new Dictionary<string, List<SchedulePeriodDto>>
                {
                    ["1.1"] = new()
                    {
                        new() { From = "10:00", To = "12:00", ShutdownHours = "10:00-12:00", Status = 1 }
                    }
                }
            }
        };

        var initialChanges = await tracker.ProcessQueueSchedulesAsync("1.1", initialSchedule);
        Assert.Empty(initialChanges); // first snapshot created, no changes alerted for existing day

        // Updated schedule during the day
        var updatedSchedule = new List<DayScheduleDto>
        {
            new()
            {
                EventDate = today,
                ScheduleApprovedSince = $"{today} 11:30",
                Queues = new Dictionary<string, List<SchedulePeriodDto>>
                {
                    ["1.1"] = new()
                    {
                        new() { From = "10:00", To = "12:00", ShutdownHours = "10:00-12:00", Status = 1 },
                        new() { From = "16:00", To = "18:00", ShutdownHours = "16:00-18:00", Status = 1 }
                    }
                }
            }
        };

        var detectedChanges = await tracker.ProcessQueueSchedulesAsync("1.1", updatedSchedule);

        Assert.Single(detectedChanges);
        var change = detectedChanges[0];
        Assert.True(change.HasChanges);
        Assert.False(change.IsNewDayPublished);
        Assert.Equal("1.1", change.QueueCode);
        Assert.Contains("➕ Додано: 16:00-18:00", change.ChangeDescriptionUa);
    }

    [Fact]
    public async Task ProcessQueueSchedulesAsync_WhenTomorrowAppears_DetectsNewDayPublished()
    {
        var dbFactory = new InMemoryDbContextFactory(Guid.NewGuid().ToString());
        var tracker = new ScheduleTrackerService(dbFactory, NullLogger<ScheduleTrackerService>.Instance);

        var tomorrow = ScheduleTrackerService.GetKyivNow().AddDays(1).ToString("dd.MM.yyyy");

        var schedule = new List<DayScheduleDto>
        {
            new()
            {
                EventDate = tomorrow,
                ScheduleApprovedSince = $"{tomorrow} 17:00",
                Queues = new Dictionary<string, List<SchedulePeriodDto>>
                {
                    ["2.2"] = new()
                    {
                        new() { From = "08:00", To = "10:00", ShutdownHours = "08:00-10:00", Status = 1 }
                    }
                }
            }
        };

        var detectedChanges = await tracker.ProcessQueueSchedulesAsync("2.2", schedule);

        Assert.Single(detectedChanges);
        var change = detectedChanges[0];
        Assert.True(change.HasChanges);
        Assert.True(change.IsNewDayPublished);
        Assert.Equal("2.2", change.QueueCode);
        Assert.Equal(tomorrow, change.EventDate);
    }
}
