using Telegram.Bot.Types.ReplyMarkups;
using PowerOutageIF.Models;

namespace PowerOutageIF.UI;

public static class TelegramKeyboards
{
    public static readonly string[] Queues = new[]
    {
        "1.1", "1.2",
        "2.1", "2.2",
        "3.1", "3.2",
        "4.1", "4.2",
        "5.1", "5.2",
        "6.1", "6.2"
    };

    public static InlineKeyboardMarkup GetQueueSelectionKeyboard(string? currentQueue = null)
    {
        var rows = new List<InlineKeyboardButton[]>();
        for (int i = 0; i < Queues.Length; i += 2)
        {
            var q1 = Queues[i];
            var q2 = Queues[i + 1];

            var label1 = (q1 == currentQueue) ? $"✅ {q1}" : $"Підчерга {q1}";
            var label2 = (q2 == currentQueue) ? $"✅ {q2}" : $"Підчерга {q2}";

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(label1, $"set_queue:{q1}"),
                InlineKeyboardButton.WithCallbackData(label2, $"set_queue:{q2}")
            });
        }
        return new InlineKeyboardMarkup(rows);
    }

    public static ReplyKeyboardMarkup GetMainReplyKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📅 Сьогодні", "📆 Завтра" },
            new KeyboardButton[] { "⚡ Мій статус", "⚙️ Налаштування" }
        })
        {
            ResizeKeyboard = true
        };
    }

    public static InlineKeyboardMarkup GetSettingsKeyboard(BotUser user)
    {
        var notifText = user.NotificationsEnabled ? "🔔 Сповіщення: УВІМКНЕНО" : "🔕 Сповіщення: ВИМКНЕНО";
        var silentText = user.SilentModeAtNight ? "🌙 Ніч (23-07): ТИХИЙ РЕЖИМ" : "🔊 Ніч (23-07): ЗІ ЗВУКОМ";

        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(notifText, "toggle_notif") },
            new[] { InlineKeyboardButton.WithCallbackData(silentText, "toggle_silent") },
            new[] { InlineKeyboardButton.WithCallbackData("🔄 Змінити підчергу", "choose_queue") }
        });
    }

    public static InlineKeyboardMarkup GetNotificationActionKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("📅 Графік на сьогодні", "view_today"),
                InlineKeyboardButton.WithCallbackData("📆 Графік на завтра", "view_tomorrow")
            }
        });
    }
}
