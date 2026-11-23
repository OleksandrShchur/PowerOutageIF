using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IBroadcastQueue _broadcastQueue;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<SchedulePollingWorker> _logger;
    private readonly TimeSpan _pollingInterval;
    private bool _isFirstRun = true;

    public SchedulePollingWorker(
        IPrykarpattiaApiClient apiClient,
        IScheduleTrackerService trackerService,
        ITelegramFormatter formatter,
        IBroadcastQueue broadcastQueue,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IConfiguration configuration,
        ILogger<SchedulePollingWorker> logger)
    {
        _apiClient = apiClient;
        _trackerService = trackerService;
        _formatter = formatter;
        _broadcastQueue = broadcastQueue;
        _dbContextFactory = dbContextFactory;
        _logger = logger;

        var intervalSec = configuration.GetValue<int>("OblenergoSettings:PollingIntervalSeconds", 120);
        _pollingInterval = TimeSpan.FromSeconds(Math.Max(30, intervalSec));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Служба моніторингу графіків обленерго запущена. Інтервал опитування: {Interval} сек.", _pollingInterval.TotalSeconds);

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

                        if (!_isFirstRun && changes.Count > 0)
                        {
                            totalChangesDetected += changes.Count;
                            foreach (var change in changes)
                            {
                                await QueueNotificationsForChangeAsync(change, stoppingToken);
                            }
                        }
                    }

                    await Task.Delay(200, stoppingToken);
                }

                if (_isFirstRun)
                {
                    _isFirstRun = false;
                    _logger.LogInformation("✅ Базовий стан усіх 12 черг успішно збережено в базі даних.");
                }
                else
                {
                    var duration = DateTime.UtcNow - cycleStart;
                    _logger.LogInformation("✨ Перевірка завершена за {Duration:F1} сек. Виявлено нових змін: {Changes}. У черзі на відправку: {Pending}", 
                        duration.TotalSeconds, totalChangesDetected, _broadcastQueue.PendingCount);
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

    private async Task QueueNotificationsForChangeAsync(ScheduleChangeResult change, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var subscribedUsers = await dbContext.Users
                .Where(u => u.SubscribedQueue == change.QueueCode && u.NotificationsEnabled)
                .Select(u => new { u.Id, u.SilentModeAtNight })
                .ToListAsync(cancellationToken);

            if (subscribedUsers.Count == 0)
            {
                _logger.LogInformation("📢 Зміна в черзі {Queue}, але немає активних підписників.", change.QueueCode);
                return;
            }

            var messageText = _formatter.FormatChangeNotification(change);
            var nowKyiv = ScheduleTrackerService.GetKyivNow();
            bool isNightTime = nowKyiv.Hour >= 23 || nowKyiv.Hour < 7;

            var notifications = subscribedUsers.Select(u => new QueuedNotification(
                ChatId: u.Id,
                Text: messageText,
                DisableNotification: isNightTime && u.SilentModeAtNight
            ));

            _broadcastQueue.EnqueueRange(notifications);

            _logger.LogInformation("📥 Додано до черги розсилки: {Count} повідомлень для підчерги {Queue}. Нічний режим: {IsNight}",
                subscribedUsers.Count, change.QueueCode, isNightTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка додавання повідомлень до черги для черги {Queue}", change.QueueCode);
        }
    }
}
