namespace PowerOutageIF.Models;

public class ScheduleChangeLog
{
    public int Id { get; set; }
    public string QueueCode { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string ChangeSummary { get; set; } = string.Empty;
    public string? PreviousApprovedSince { get; set; }
    public string? NewApprovedSince { get; set; }
    public int NotifiedUsersCount { get; set; }
}
