using PowerOutageIF.Models;

namespace PowerOutageIF.Services;

public interface ITelegramFormatter
{
    string FormatScheduleMessage(string queueCode, DayScheduleDto? schedule, bool isTomorrow);
    string FormatChangeNotification(ScheduleChangeResult change);
    string FormatWelcomeMessage(BotUser user);
    string FormatSettingsMessage(BotUser user);
    string FormatQueueSelectionPrompt(string? currentQueue);
    string FormatHelpMessage();
}
