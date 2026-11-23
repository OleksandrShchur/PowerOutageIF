using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using PowerOutageIF.Data;
using PowerOutageIF.Models;
using PowerOutageIF.UI;

namespace PowerOutageIF.Services;

public interface ITelegramBotHandler
{
    Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken);
    Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken);
}

public class TelegramBotHandler : ITelegramBotHandler
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IScheduleTrackerService _trackerService;
    private readonly ITelegramFormatter _formatter;
    private readonly ILogger<TelegramBotHandler> _logger;

    public TelegramBotHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IScheduleTrackerService trackerService,
        ITelegramFormatter formatter,
        ILogger<TelegramBotHandler> logger)
    {
        _dbContextFactory = dbContextFactory;
        _trackerService = trackerService;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message != null)
            {
                await HandleMessageAsync(botClient, update.Message, cancellationToken);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при обробці апдейту Telegram: {UpdateId}", update.Id);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Помилка клієнта Telegram Bot");
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.From == null) return;

        var chatId = message.Chat.Id;
        var text = message.Text?.Trim() ?? string.Empty;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == chatId, cancellationToken);

        if (user == null)
        {
            user = new BotUser
            {
                Id = chatId,
                Username = message.From.Username,
                FirstName = message.From.FirstName,
                LastName = message.From.LastName,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user.Username = message.From.Username;
            user.FirstName = message.From.FirstName;
            user.LastName = message.From.LastName;
            user.LastActiveAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        switch (text)
        {
            case "/start":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: _formatter.FormatWelcomeMessage(user),
                    parseMode: ParseMode.Html,
                    replyMarkup: string.IsNullOrEmpty(user.SubscribedQueue)
                        ? TelegramKeyboards.GetQueueSelectionKeyboard(null)
                        : TelegramKeyboards.GetMainReplyKeyboard(),
                    cancellationToken: cancellationToken);
                break;

            case "/today":
            case "📅 Сьогодні":
            case "/status":
            case "⚡ Мій статус":
                await SendScheduleForUserAsync(botClient, chatId, user.SubscribedQueue, false, cancellationToken);
                break;

            case "/tomorrow":
            case "📆 Завтра":
                await SendScheduleForUserAsync(botClient, chatId, user.SubscribedQueue, true, cancellationToken);
                break;

            case "/queue":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: _formatter.FormatQueueSelectionPrompt(user.SubscribedQueue),
                    parseMode: ParseMode.Html,
                    replyMarkup: TelegramKeyboards.GetQueueSelectionKeyboard(user.SubscribedQueue),
                    cancellationToken: cancellationToken);
                break;

            case "/settings":
            case "⚙️ Налаштування":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: _formatter.FormatSettingsMessage(user),
                    parseMode: ParseMode.Html,
                    replyMarkup: TelegramKeyboards.GetSettingsKeyboard(user),
                    cancellationToken: cancellationToken);
                break;

            case "/help":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: _formatter.FormatHelpMessage(),
                    parseMode: ParseMode.Html,
                    replyMarkup: TelegramKeyboards.GetMainReplyKeyboard(),
                    cancellationToken: cancellationToken);
                break;

            default:
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Не розпізнав команду. Будь ласка, скористайтеся кнопками меню або відправте /start.",
                    replyMarkup: TelegramKeyboards.GetMainReplyKeyboard(),
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback.Message == null) return;

        var chatId = callback.Message.Chat.Id;
        var data = callback.Data ?? string.Empty;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == chatId, cancellationToken);
        if (user == null)
        {
            user = new BotUser { Id = chatId, SubscribedQueue = null };
            dbContext.Users.Add(user);
        }

        if (data.StartsWith("set_queue:"))
        {
            var selectedQueue = data.Substring("set_queue:".Length);
            user.SubscribedQueue = selectedQueue;
            user.LastActiveAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: $"✅ Підчергу {selectedQueue} обрано!",
                cancellationToken: cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Ви успішно обрали <b>Підчергу {selectedQueue}</b>!\nТепер ви будете оперативно отримувати сповіщення про будь-які зміни в графіку.",
                parseMode: ParseMode.Html,
                replyMarkup: TelegramKeyboards.GetMainReplyKeyboard(),
                cancellationToken: cancellationToken);

            // Send current schedule immediately
            await SendScheduleForUserAsync(botClient, chatId, selectedQueue, false, cancellationToken);
        }
        else if (data == "toggle_notif")
        {
            user.NotificationsEnabled = !user.NotificationsEnabled;
            user.LastActiveAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: user.NotificationsEnabled ? "🔔 Сповіщення увімкнено" : "🔕 Сповіщення вимкнено",
                cancellationToken: cancellationToken);

            await botClient.EditMessageText(
                chatId: chatId,
                messageId: callback.Message.MessageId,
                text: _formatter.FormatSettingsMessage(user),
                parseMode: ParseMode.Html,
                replyMarkup: TelegramKeyboards.GetSettingsKeyboard(user),
                cancellationToken: cancellationToken);
        }
        else if (data == "toggle_silent")
        {
            user.SilentModeAtNight = !user.SilentModeAtNight;
            user.LastActiveAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: user.SilentModeAtNight ? "🌙 Тихий режим увімкнено (23:00 - 07:00 без звуку)" : "🔊 Звичайний режим (зі звуком)",
                cancellationToken: cancellationToken);

            await botClient.EditMessageText(
                chatId: chatId,
                messageId: callback.Message.MessageId,
                text: _formatter.FormatSettingsMessage(user),
                parseMode: ParseMode.Html,
                replyMarkup: TelegramKeyboards.GetSettingsKeyboard(user),
                cancellationToken: cancellationToken);
        }
        else if (data == "choose_queue")
        {
            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            await botClient.SendMessage(
                chatId: chatId,
                text: _formatter.FormatQueueSelectionPrompt(user.SubscribedQueue),
                parseMode: ParseMode.Html,
                replyMarkup: TelegramKeyboards.GetQueueSelectionKeyboard(user.SubscribedQueue),
                cancellationToken: cancellationToken);
        }
        else if (data == "view_today")
        {
            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            await SendScheduleForUserAsync(botClient, chatId, user.SubscribedQueue, false, cancellationToken);
        }
        else if (data == "view_tomorrow")
        {
            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            await SendScheduleForUserAsync(botClient, chatId, user.SubscribedQueue, true, cancellationToken);
        }
    }

    private async Task SendScheduleForUserAsync(ITelegramBotClient botClient, long chatId, string? queueCode, bool tomorrow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueCode))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "⚠️ Ви ще не обрали підчергу. Будь ласка, оберіть вашу чергу зі списку:",
                replyMarkup: TelegramKeyboards.GetQueueSelectionKeyboard(null),
                cancellationToken: cancellationToken);
            return;
        }

        var schedule = await _trackerService.GetCachedScheduleAsync(queueCode, tomorrow, cancellationToken);
        var messageText = _formatter.FormatScheduleMessage(queueCode, schedule, tomorrow);

        await botClient.SendMessage(
            chatId: chatId,
            text: messageText,
            parseMode: ParseMode.Html,
            replyMarkup: TelegramKeyboards.GetMainReplyKeyboard(),
            cancellationToken: cancellationToken);
    }
}
