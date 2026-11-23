using System.Text.Json.Serialization;

namespace PowerOutageIF.Models;

public class QueueInfoItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

public class DayScheduleDto
{
    [JsonPropertyName("eventDate")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("queues")]
    public Dictionary<string, List<SchedulePeriodDto>> Queues { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("scheduleApprovedSince")]
    public string? ScheduleApprovedSince { get; set; }
}

public class SchedulePeriodDto
{
    [JsonPropertyName("shutdownHours")]
    public string ShutdownHours { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public record ScheduleChangeResult(
    bool HasChanges,
    bool IsNewDayPublished,
    string QueueCode,
    string EventDate,
    string? OldApprovedSince,
    string? NewApprovedSince,
    List<SchedulePeriodDto> OldPeriods,
    List<SchedulePeriodDto> NewPeriods,
    string ChangeDescriptionUa
);
