using Xunit;
using PowerOutageIF.Models;
using PowerOutageIF.Services;

namespace PowerOutageIF.Tests;

public class FormatterTests
{
    private readonly TelegramFormatter _formatter = new();

    [Fact]
    public void Generate24HourTimeline_WhenNoOutages_ShouldBeAllGreen()
    {
        var periods = new List<SchedulePeriodDto>();
        var timeline = TelegramFormatter.Generate24HourTimeline(periods);

        Assert.DoesNotContain("🟥", timeline);
        Assert.Contains("🟩", timeline);
    }

    [Fact]
    public void Generate24HourTimeline_WhenOutageFrom11To16_ShouldHaveRedBlocks()
    {
        var periods = new List<SchedulePeriodDto>
        {
            new() { From = "11:00", To = "16:00", ShutdownHours = "11:00-16:00", Status = 1 }
        };

        var timeline = TelegramFormatter.Generate24HourTimeline(periods);

        Assert.Contains("🟥", timeline);
        Assert.Contains("🟩", timeline);
    }

    [Fact]
    public void FormatScheduleMessage_WhenScheduleAvailable_ContainsRequiredInformation()
    {
        var schedule = new DayScheduleDto
        {
            EventDate = "23.11.2026",
            ScheduleApprovedSince = "23.11.2026 12:37",
            Queues = new Dictionary<string, List<SchedulePeriodDto>>
            {
                ["1.1"] = new()
                {
                    new() { From = "11:00", To = "16:30", ShutdownHours = "11:00-16:30", Status = 1 },
                    new() { From = "20:00", To = "22:00", ShutdownHours = "20:00-22:00", Status = 1 }
                }
            }
        };

        var message = _formatter.FormatScheduleMessage("1.1", schedule, false);

        Assert.Contains("Підчерга 1.1", message);
        Assert.Contains("11:00 — 16:30", message);
        Assert.Contains("20:00 — 22:00", message);
        Assert.Contains("23.11.2026 12:37", message);
        Assert.Contains("Добова шкала", message);
    }

    [Fact]
    public void FormatScheduleMessage_WhenNoOutagesPlanned_ShowsCelebrationText()
    {
        var schedule = new DayScheduleDto
        {
            EventDate = "23.11.2026",
            Queues = new Dictionary<string, List<SchedulePeriodDto>>
            {
                ["1.1"] = new()
            }
        };

        var message = _formatter.FormatScheduleMessage("1.1", schedule, false);

        Assert.Contains("ГРАФІК НЕ ЗАСТОСОВУЄТЬСЯ", message);
    }

    [Fact]
    public void FormatChangeNotification_WhenChangeOccurred_ContainsDiffDetails()
    {
        var change = new ScheduleChangeResult(
            HasChanges: true,
            IsNewDayPublished: false,
            QueueCode: "2.1",
            EventDate: "23.11.2026",
            OldApprovedSince: "23.11.2026 09:00",
            NewApprovedSince: "23.11.2026 13:45",
            OldPeriods: new List<SchedulePeriodDto>
            {
                new() { From = "10:00", To = "14:00", ShutdownHours = "10:00-14:00", Status = 1 }
            },
            NewPeriods: new List<SchedulePeriodDto>
            {
                new() { From = "12:00", To = "16:00", ShutdownHours = "12:00-16:00", Status = 1 }
            },
            ChangeDescriptionUa: "➕ Додано: 12:00-16:00. ➖ Скасовано: 10:00-14:00."
        );

        var message = _formatter.FormatChangeNotification(change);

        Assert.Contains("УВАГА! ЗМІНА В ГРАФІКУ ВІДКЛЮЧЕНЬ!", message);
        Assert.Contains("2.1", message);
        Assert.Contains("➕ Додано: 12:00-16:00", message);
        Assert.Contains("➖ Скасовано: 10:00-14:00", message);
        Assert.Contains("23.11.2026 13:45", message);
    }

    [Fact]
    public void FormatChangeNotification_WhenNewDayPublished_ContainsTomorrowHeadline()
    {
        var change = new ScheduleChangeResult(
            HasChanges: true,
            IsNewDayPublished: true,
            QueueCode: "3.2",
            EventDate: "24.11.2026",
            OldApprovedSince: null,
            NewApprovedSince: "23.11.2026 18:00",
            OldPeriods: new List<SchedulePeriodDto>(),
            NewPeriods: new List<SchedulePeriodDto>
            {
                new() { From = "08:00", To = "12:00", ShutdownHours = "08:00-12:00", Status = 1 }
            },
            ChangeDescriptionUa: "08:00-12:00"
        );

        var message = _formatter.FormatChangeNotification(change);

        Assert.Contains("ОПУБЛІКОВАНО ГРАФІК НА ЗАВТРА!", message);
        Assert.Contains("3.2", message);
        Assert.Contains("08:00 — 12:00", message);
    }
}
