namespace PowerOutageIF.Models;

public class QueueScheduleSnapshot
{
    public int Id { get; set; }
    public string QueueCode { get; set; } = string.Empty; // e.g. "1.1"
    public string EventDate { get; set; } = string.Empty; // "23.11.2026"
    public string? ScheduleApprovedSince { get; set; } // "23.11.2026 12:37"
    public string? CreatedAtFromApi { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
