namespace PowerOutageIF.Models;

public class BotUser
{
    /// <summary>
    /// Telegram ChatId / UserId
    /// </summary>
    public long Id { get; set; }
    
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    /// <summary>
    /// Code of the subscribed queue (e.g. "1.1", "2.1", "6.2")
    /// </summary>
    public string? SubscribedQueue { get; set; }
    
    /// <summary>
    /// Global toggle for notifications
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Whether notifications between 23:00 and 07:00 should be sent silently (without sound)
    /// </summary>
    public bool SilentModeAtNight { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
}
