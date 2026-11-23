using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using PowerOutageIF.Services;

namespace PowerOutageIF.Workers;

public class TelegramBotWorker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramBotHandler _botHandler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotWorker> _logger;

    public TelegramBotWorker(
        ITelegramBotClient botClient,
        ITelegramBotHandler botHandler,
        IConfiguration configuration,
        ILogger<TelegramBotWorker> logger)
    {
        _botClient = botClient;
        _botHandler = botHandler;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["BotSettings:BotToken"];
        if (string.IsNullOrWhiteSpace(token) || token.Contains("YOUR_TELEGRAM_BOT_TOKEN_HERE"))
        {
            _logger.LogWarning("⚠️ Токен Telegram-бота не налаштовано в appsettings.json! Бот очікує валідний токен.");
            return;
        }

        try
        {
            var me = await _botClient.GetMe(cancellationToken: stoppingToken);
            _logger.LogInformation("🤖 Бот успішно підключений: @{BotUsername} (ID: {BotId})", me.Username, me.Id);

            // Register standard bot commands in Telegram Menu button
            await _botClient.SetMyCommands(new[]
            {
                new BotCommand { Command = "start", Description = "Головне меню / перезапуск" },
                new BotCommand { Command = "today", Description = "Графік вимкнень на сьогодні" },
                new BotCommand { Command = "tomorrow", Description = "Графік вимкнень на завтра" },
                new BotCommand { Command = "status", Description = "Поточний стан світла" },
                new BotCommand { Command = "queue", Description = "Обрати або змінити підчергу" },
                new BotCommand { Command = "settings", Description = "Налаштування сповіщень" },
                new BotCommand { Command = "help", Description = "Довідка по боту" }
            }, cancellationToken: stoppingToken);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
                DropPendingUpdates = true
            };

            _logger.LogInformation("👂 Початок прослуховування повідомлень Telegram...");

            await _botClient.ReceiveAsync(
                updateHandler: _botHandler.HandleUpdateAsync,
                errorHandler: _botHandler.HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Критична помилка при запуску Telegram бота");
        }
    }
}
