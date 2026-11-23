using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using PowerOutageIF.Data;
using PowerOutageIF.Models;
using PowerOutageIF.Services;
using PowerOutageIF.UI;

namespace PowerOutageIF.Workers;

public class SchedulePollingWorker : BackgroundService
{
    private readonly IPrykarpattiaApiClient _apiClient;
    private readonly IScheduleTrackerService _trackerService;
    private readonly ITelegramFormatter _formatter;
    private readonly ITelegramBotClient _botClient;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<SchedulePollingWorker> _logger;
    private readonly TimeSpan _pollingInterval;
    private bool _isFirstRun = true;

    public SchedulePollingWorker(
        IPrykarpattiaApiClient apiClient,
        IScheduleTrackerService trackerService,
        ITelegramFormatter formatter,
        ITelegramBotClient botClient,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IConfiguration configuration,
        ILogger<SchedulePollingWorker> logger)
    {
        _apiClient = apiClient;
        _trackerService = trackerService;
        _formatter = formatter;
        _botClient = botClient;
        _dbContextFactory = dbContextFactory;
        _logger = logger;

        var intervalSec = configuration.GetValue<int>("OblenergoSettings:PollingIntervalSeconds", 120);
        _pollingInterval = TimeSpan.FromSeconds(Math.Max(30, intervalSec));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Служба моніторингу графіків обленерго запущена. Інтервал опитування: {Interval} сек.", _pollingInterval.TotalSeconds);

        // Small initial delay so bot service can initialize
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cycleStart = DateTime.UtcNow;
                _logger.LogInformation("🔍 Початок циклу перевірки графіків (12 черг)...");

                int totalChangesDetected = 0;

                foreach (var queueCode in TelegramKeyboards.Queues)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var schedules = await _apiClient.GetScheduleByQueueAsync(queueCode, stoppingToken);
                    if (schedules != null && schedules.Count > 0)
                    {
                        var changes = await _trackerService.ProcessQueueSchedulesAsync(queueCode, schedules, stoppingToken);

                        // Don't send notifications during first baseline run, just populate state
                        if (!_isFirstRun && changes.Count > 0)
                        {
                            totalChangesDetected += changes.Count;
                            foreach (var change in changes)
                            {
                                await BroadcastChangeAsync(change, stoppingToken);
                            }
                        }
                    }

                    // Respectful small delay between queue requests
                    await Task.Delay(300, stoppingToken);
                }

                if (_isFirstRun)
                {
                    _isFirstRun = false;
                    _logger.LogInformation("✅ Базовий стан усіх 12 черг успішно збережено в базі даних.");
                }
                else
                {
                    var duration = DateTime.UtcNow - cycleStart;
                    _logger.LogInformation("✨ Перевірка завершена за {Duration:F1} сек. Виявлено нових змін: {Changes}", duration.TotalSeconds, totalChangesDetected);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "⚠️ Помилка під час циклу перевірки графіків");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("🛑 Служба моніторингу графіків зупинена.");
    }

    private async Task BroadcastChangeAsync(ScheduleChangeResult change, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var subscribedUsers = await dbContext.Users
                .Where(u => u.SubscribedQueue == change.QueueCode && u.NotificationsEnabled)
                .ToListAsync(cancellationToken);

            if (subscribedUsers.Count == 0)
            {
                _logger.LogInformation("📢 Зміна в черзі {Queue}, але немає підписаних користувачів.", change.QueueCode);
                return;
            }

            var messageText = _formatter.FormatChangeNotification(change);
            var nowKyiv = ScheduleTrackerService.GetKyivNow();
            bool isNightTime = nowKyiv.Hour >= 23 || nowKyiv.Hour < 7;

            _logger.LogInformation("📢 Розсилка сповіщення для черги {Queue} ({Count} користувачів). Нічний час: {IsNight}",
                change.QueueCode, subscribedUsers.Count, isNightTime);

            int sentCount = 0;
            foreach (var user in subscribedUsers)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool disableSound = isNightTime && user.SilentModeAtNight;

                try
                {
                    await _botClient.SendMessage(
                        chatId: user.Id,
                        text: messageText,
                        parseMode: ParseMode.Html,
                        disableNotification: disableSound,
                        replyMarkup: TelegramKeyboards.GetNotificationActionKeyboard(),
                        cancellationToken: cancellationToken);

                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не вдалося надіслати сповіщення користувачу {UserId}: {Error}", user.Id, ex.Message);
                }

                // Telegram rate limit friendly delay
                await Task.Delay(50, cancellationToken);
            }

            _logger.LogInformation("✅ Сповіщення для черги {Queue} успішно надіслано {Sent}/{Total} користувачам.",
                change.QueueCode, sentCount, subscribedUsers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при розсилці сповіщень для черги {Queue}", change.QueueCode);
        }
    }
}
