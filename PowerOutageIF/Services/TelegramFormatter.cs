using System.Globalization;
using System.Text;
using PowerOutageIF.Models;

namespace PowerOutageIF.Services;

public class TelegramFormatter : ITelegramFormatter
{
    private static readonly CultureInfo UkCulture = new("uk-UA");

    public string FormatScheduleMessage(string queueCode, DayScheduleDto? schedule, bool isTomorrow)
    {
        var nowKyiv = ScheduleTrackerService.GetKyivNow();
        var targetDate = isTomorrow ? nowKyiv.AddDays(1) : nowKyiv;
        var dayTitle = isTomorrow ? "Завтра" : "Сьогодні";
        var dateFormatted = targetDate.ToString("dddd, d MMMM yyyy", UkCulture);

        var sb = new StringBuilder();

        sb.AppendLine($"⚡ <b>Графік відключень • Підчерга {queueCode}</b>");
        sb.AppendLine($"📅 <b>{char.ToUpper(dayTitle[0]) + dayTitle[1..]}</b> ({dateFormatted})");
        sb.AppendLine();

        if (schedule == null || !schedule.Queues.TryGetValue(queueCode, out var periods))
        {
            sb.AppendLine("⚠️ <i>Інформація про графік на цю дату наразі відсутня на сервері обленерго.</i>");
            sb.AppendLine("Як тільки графік з'явиться — ви отримаєте миттєве сповіщення!");
            return sb.ToString();
        }

        if (periods.Count == 0)
        {
            sb.AppendLine("🎉 <b>ГРАФІК НЕ ЗАСТОСОВУЄТЬСЯ!</b>");
            sb.AppendLine("Світло має бути протягом усієї доби без планових відключень.");
            sb.AppendLine();
            sb.AppendLine("🟩🟩🟩🟩🟩🟩🟩🟩🟩🟩🟩🟩");
            sb.AppendLine("🟩🟩🟩🟩🟩🟩🟩🟩🟩🟩🟩🟩");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(schedule.ScheduleApprovedSince))
            {
                sb.AppendLine($"🔄 <i>Затверджено: {schedule.ScheduleApprovedSince}</i>");
            }
            return sb.ToString();
        }

        // Current status for today
        if (!isTomorrow)
        {
            var (isOutageNow, nextEventText) = CalculateCurrentStatus(nowKyiv, periods);
            if (isOutageNow)
            {
                sb.AppendLine("🔴 <b>ЗАРАЗ: ВІДКЛЮЧЕННЯ ТРИВАЄ</b>");
            }
            else
            {
                sb.AppendLine("🟢 <b>ЗАРАЗ: СВІТЛО Є</b>");
            }

            if (!string.IsNullOrEmpty(nextEventText))
            {
                sb.AppendLine($"⏳ {nextEventText}");
            }
            sb.AppendLine();
        }

        // 24-hour visual progress bar
        var timeline = Generate24HourTimeline(periods);
        sb.AppendLine("<b>Добова шкала (00:00 — 24:00):</b>");
        sb.AppendLine(timeline);
        sb.AppendLine("<code>00 · · 06 · · 12 · · 18 · · 24</code>");
        sb.AppendLine("<i>(🟩 — є світло, 🟥 — відключення)</i>");
        sb.AppendLine();

        // Detailed intervals list
        sb.AppendLine("🕒 <b>Інтервали відключень:</b>");
        TimeSpan totalOutageTime = TimeSpan.Zero;

        foreach (var p in periods.OrderBy(p => ParseTime(p.From)))
        {
            var duration = CalculateDuration(p.From, p.To);
            totalOutageTime += duration;
            var durationStr = FormatDuration(duration);
            sb.AppendLine($"  🔴 <b>{p.From} — {p.To}</b> <i>({durationStr})</i>");
        }

        sb.AppendLine();
        sb.AppendLine($"⏱ <b>Загалом без світла:</b> {FormatDuration(totalOutageTime)}");

        if (!string.IsNullOrEmpty(schedule.ScheduleApprovedSince))
        {
            sb.AppendLine($"🔄 <i>Затверджено обленерго: {schedule.ScheduleApprovedSince}</i>");
        }

        return sb.ToString();
    }

    public string FormatChangeNotification(ScheduleChangeResult change)
    {
        var sb = new StringBuilder();

        if (change.IsNewDayPublished)
        {
            sb.AppendLine("📢 <b>ОПУБЛІКОВАНО ГРАФІК НА ЗАВТРА!</b>");
            sb.AppendLine($"⚡ <b>Підчерга:</b> {change.QueueCode}");
            sb.AppendLine($"📅 <b>Дата:</b> {change.EventDate}");
            sb.AppendLine();

            if (change.NewPeriods.Count == 0)
            {
                sb.AppendLine("🎉 <b>Відключень не заплановано!</b> Світло буде цілий день.");
            }
            else
            {
                sb.AppendLine("<b>Шкала доби:</b>");
                sb.AppendLine(Generate24HourTimeline(change.NewPeriods));
                sb.AppendLine();
                sb.AppendLine("🕒 <b>Заплановані відключення:</b>");
                foreach (var p in change.NewPeriods.OrderBy(p => ParseTime(p.From)))
                {
                    sb.AppendLine($"  🔴 <b>{p.From} — {p.To}</b>");
                }
            }
        }
        else
        {
            sb.AppendLine("⚠️ <b>УВАГА! ЗМІНА В ГРАФІКУ ВІДКЛЮЧЕНЬ!</b>");
            sb.AppendLine($"⚡ <b>Підчерга:</b> {change.QueueCode}");
            sb.AppendLine($"📅 <b>Дата:</b> {change.EventDate}");
            sb.AppendLine();
            sb.AppendLine($"📝 <b>Зміни:</b> {change.ChangeDescriptionUa}");
            sb.AppendLine();

            if (change.NewPeriods.Count == 0)
            {
                sb.AppendLine("🎉 <b>Всі відключення скасовано!</b>");
            }
            else
            {
                sb.AppendLine("<b>Нова шкала доби:</b>");
                sb.AppendLine(Generate24HourTimeline(change.NewPeriods));
                sb.AppendLine();
                sb.AppendLine("🕒 <b>Оновлений графік:</b>");
                foreach (var p in change.NewPeriods.OrderBy(p => ParseTime(p.From)))
                {
                    sb.AppendLine($"  🔴 <b>{p.From} — {p.To}</b>");
                }
            }

            if (!string.IsNullOrEmpty(change.NewApprovedSince))
            {
                sb.AppendLine();
                sb.AppendLine($"🔄 <i>Нове затвердження: {change.NewApprovedSince}</i>");
            }
        }

        return sb.ToString();
    }

    public string FormatWelcomeMessage(BotUser user)
    {
        var sb = new StringBuilder();
        sb.AppendLine("👋 <b>Вітаю в боті моніторингу графіків відключень світла!</b>");
        sb.AppendLine("<i>(ПАТ «Прикарпаттяобленерго» / Івано-Франківська область)</i>");
        sb.AppendLine();
        sb.AppendLine("Тут ви можете:");
        sb.AppendLine("• Обрати вашу підчергу (від 1.1 до 6.2)");
        sb.AppendLine("• Отримувати <b>миттєві сповіщення</b>, якщо графік змінюється протягом дня");
        sb.AppendLine("• Першими дізнаватися про <b>появу графіка на завтра</b>");
        sb.AppendLine("• Переглядати зручну добову шкалу в один клік");
        sb.AppendLine();

        if (string.IsNullOrEmpty(user.SubscribedQueue))
        {
            sb.AppendLine("👇 <b>Будь ласка, оберіть вашу підчергу зі списку нижче:</b>");
        }
        else
        {
            sb.AppendLine($"✅ Ваша поточна підчерга: <b>{user.SubscribedQueue}</b>");
            sb.AppendLine("Скористайтеся кнопками внизу екрана для перегляду розкладу.");
        }

        return sb.ToString();
    }

    public string FormatSettingsMessage(BotUser user)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚙️ <b>Налаштування сповіщень</b>");
        sb.AppendLine();
        sb.AppendLine($"• Підчерга: <b>{(string.IsNullOrEmpty(user.SubscribedQueue) ? "не обрано" : user.SubscribedQueue)}</b>");
        sb.AppendLine($"• Сповіщення: <b>{(user.NotificationsEnabled ? "🔔 Увімкнено" : "🔕 Вимкнено")}</b>");
        sb.AppendLine($"• Тихий нічний режим (23:00 - 07:00): <b>{(user.SilentModeAtNight ? "🌙 Увімкнено (без звуку)" : "🔊 Зі звуком")}</b>");
        sb.AppendLine();
        sb.AppendLine("<i>Ви можете змінити параметри за допомогою кнопок нижче:</i>");
        return sb.ToString();
    }

    public string FormatQueueSelectionPrompt(string? currentQueue)
    {
        if (string.IsNullOrEmpty(currentQueue))
        {
            return "📍 <b>Оберіть вашу підчергу вимкнень:</b>\n<i>(Якщо не знаєте свою чергу, перевірте на svitlo.oe.if.ua за адресою)</i>";
        }
        return $"📍 <b>Зміна підчерги</b> (поточна: <b>{currentQueue}</b>):\nОберіть нову підчергу зі списку:";
    }

    public string FormatHelpMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ℹ️ <b>Довідка по боту:</b>");
        sb.AppendLine();
        sb.AppendLine("📌 <b>Основні команди:</b>");
        sb.AppendLine("• /start — Головне меню та початок роботи");
        sb.AppendLine("• /today — Графік вимкнень на сьогодні");
        sb.AppendLine("• /tomorrow — Графік вимкнень на завтра (якщо вже опублікований)");
        sb.AppendLine("• /status — Поточний статус (чи є світло прямо зараз)");
        sb.AppendLine("• /queue — Змінити підчергу (1.1 — 6.2)");
        sb.AppendLine("• /settings — Налаштування сповіщень та тихого режиму");
        sb.AppendLine();
        sb.AppendLine("💡 <i>Дані надходять напряму з офіційного сервісу ПАТ «Прикарпаттяобленерго» (svitlo.oe.if.ua) без затримок.</i>");
        return sb.ToString();
    }

    public static string Generate24HourTimeline(List<SchedulePeriodDto> periods)
    {
        // 24 slots (0..23). Each represents 1 hour.
        var hourlyStatus = new int[24]; // 0 = power on, 1 = outage

        foreach (var p in periods)
        {
            var start = ParseTime(p.From);
            var end = ParseTime(p.To);
            if (end <= start)
            {
                end = 24.0; // end of day 00:00 -> 24:00
            }

            int startHour = (int)Math.Floor(start);
            int endHour = (int)Math.Ceiling(end);

            for (int h = startHour; h < endHour && h < 24; h++)
            {
                hourlyStatus[h] = 1;
            }
        }

        var sb = new StringBuilder();
        for (int h = 0; h < 24; h++)
        {
            sb.Append(hourlyStatus[h] == 1 ? "🟥" : "🟩");
            if (h == 11)
            {
                // break line into two neat 12-hour rows for perfect mobile display!
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static (bool isOutageNow, string nextEventText) CalculateCurrentStatus(DateTime nowKyiv, List<SchedulePeriodDto> periods)
    {
        double currentHour = nowKyiv.Hour + nowKyiv.Minute / 60.0;

        SchedulePeriodDto? currentPeriod = null;
        SchedulePeriodDto? nextPeriod = null;

        foreach (var p in periods.OrderBy(p => ParseTime(p.From)))
        {
            double from = ParseTime(p.From);
            double to = ParseTime(p.To);
            if (to <= from) to = 24.0;

            if (currentHour >= from && currentHour < to)
            {
                currentPeriod = p;
                break;
            }
            if (from > currentHour && nextPeriod == null)
            {
                nextPeriod = p;
            }
        }

        if (currentPeriod != null)
        {
            var endTime = ParseDateTimeToday(nowKyiv, currentPeriod.To);
            var remaining = endTime - nowKyiv;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            return (true, $"Світло увімкнуть о <b>{currentPeriod.To}</b> (через {FormatDuration(remaining)})");
        }

        if (nextPeriod != null)
        {
            var startTime = ParseDateTimeToday(nowKyiv, nextPeriod.From);
            var remaining = startTime - nowKyiv;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            return (false, $"Наступне вимкнення о <b>{nextPeriod.From}</b> (через {FormatDuration(remaining)})");
        }

        return (false, "Сьогодні більше відключень не очікується 🎉");
    }

    private static double ParseTime(string timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return 0;
        var parts = timeStr.Trim().Split(':');
        if (parts.Length >= 2 && double.TryParse(parts[0], out var h) && double.TryParse(parts[1], out var m))
        {
            return h + m / 60.0;
        }
        return 0;
    }

    private static DateTime ParseDateTimeToday(DateTime today, string timeStr)
    {
        var parts = timeStr.Trim().Split(':');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
        {
            if (h == 24 || (h == 0 && parts[0] == "00" && timeStr == "00:00"))
            {
                // end of day
                return today.Date.AddDays(1);
            }
            return today.Date.AddHours(h).AddMinutes(m);
        }
        return today;
    }

    private static TimeSpan CalculateDuration(string from, string to)
    {
        var f = ParseTime(from);
        var t = ParseTime(to);
        if (t <= f) t = 24.0;
        return TimeSpan.FromHours(t - f);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        int hours = (int)duration.TotalHours;
        int minutes = duration.Minutes;

        if (hours > 0 && minutes > 0)
            return $"{hours} год {minutes} хв";
        if (hours > 0)
            return $"{hours} год";
        return $"{minutes} хв";
    }
}
