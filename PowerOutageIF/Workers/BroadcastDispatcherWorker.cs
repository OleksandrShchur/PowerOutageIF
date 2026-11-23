using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using PowerOutageIF.Data;
using PowerOutageIF.Services;
using PowerOutageIF.UI;

namespace PowerOutageIF.Workers;

public class BroadcastDispatcherWorker : BackgroundService
{
    private readonly IBroadcastQueue _queue;
    private readonly ITelegramBotClient _botClient;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<BroadcastDispatcherWorker> _logger;

    // 25 messages per second = 40 milliseconds per message (well below Telegram's 30 msg/s cap)
    private const int DelayBetweenMessagesMs = 40;

    public BroadcastDispatcherWorker(
        IBroadcastQueue queue,
        ITelegramBotClient botClient,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<BroadcastDispatcherWorker> logger)
    {
        _queue = queue;
        _botClient = botClient;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📬 Черга розсилки (Rate-Limiter: 25 msg/s) активована.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(stoppingToken);

                await SendWithRateLimitingAsync(item, stoppingToken);

                // Rate limiting delay
                await Task.Delay(DelayBetweenMessagesMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка диспетчера розсилки повідомлень");
            }
        }

        _logger.LogInformation("🛑 Черга розсилки зупинена.");
    }

    private async Task SendWithRateLimitingAsync(QueuedNotification item, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: item.ChatId,
                text: item.Text,
                parseMode: ParseMode.Html,
                disableNotification: item.DisableNotification,
                replyMarkup: TelegramKeyboards.GetNotificationActionKeyboard(),
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 403)
        {
            // User blocked the bot or chat was deleted!
            _logger.LogWarning("🚫 Користувач {ChatId} заблокував бота. Деактивуємо сповіщення в базі даних.", item.ChatId);
            await DeactivateBlockedUserAsync(item.ChatId, cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 429)
        {
            // Telegram rate limit hit - respect retry_after
            var retrySeconds = ex.Parameters?.RetryAfter ?? 5;
            _logger.LogWarning("⚠️ Отримано Telegram 429 (Too Many Requests). Очікування {Seconds} сек перед повтором...", retrySeconds);
            await Task.Delay(TimeSpan.FromSeconds(retrySeconds + 1), cancellationToken);

            // Re-enqueue the message
            _queue.Enqueue(item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Помилка відправки користувачу {ChatId}: {Error}", item.ChatId, ex.Message);
        }
    }

    private async Task DeactivateBlockedUserAsync(long chatId, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await dbContext.Users.FindAsync(new object[] { chatId }, cancellationToken);
            if (user != null && user.NotificationsEnabled)
            {
                user.NotificationsEnabled = false;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося деактивувати заблокованого користувача {ChatId}", chatId);
        }
    }
}
