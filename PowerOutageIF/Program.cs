using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using PowerOutageIF.Data;
using PowerOutageIF.Services;
using PowerOutageIF.Workers;

var builder = WebApplication.CreateBuilder(args);

// Database configuration (SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=PowerOutageIfDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Oblenergo API Client
builder.Services.AddHttpClient<IPrykarpattiaApiClient, PrykarpattiaApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

// Singletons & Services
builder.Services.AddSingleton<IBroadcastQueue, BroadcastQueue>();
builder.Services.AddSingleton<IScheduleTrackerService, ScheduleTrackerService>();
builder.Services.AddSingleton<ITelegramFormatter, TelegramFormatter>();
builder.Services.AddSingleton<ITelegramBotHandler, TelegramBotHandler>();

// Telegram Bot Client
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var token = config["BotSettings:BotToken"] ?? "dummy_token";
    return new TelegramBotClient(token);
});

// Background Workers
builder.Services.AddHostedService<BroadcastDispatcherWorker>();
builder.Services.AddHostedService<SchedulePollingWorker>();
builder.Services.AddHostedService<TelegramBotWorker>();

var app = builder.Build();

// Ensure Database & Tables exist on startup
try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("📦 Ініціалізація бази даних SQL Server...");

    var dbFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    logger.LogInformation("✅ База даних SQL Server успішно перевірена та готова до роботи.");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Помилка при ініціалізації бази даних SQL Server");
}

// -------------------------------------------------------------
// HTTP Endpoints (for MonsterASP / Webhook / Keep-Alive)
// -------------------------------------------------------------

// Root & Health endpoints (for UptimeRobot / Cron-Job keep-alive)
app.MapGet("/", () => Results.Text("⚡ PowerOutageIF Telegram Bot Service is running."));

app.MapGet("/health", (IBroadcastQueue queue) => Results.Ok(new
{
    status = "Healthy",
    serverTimeUtc = DateTime.UtcNow,
    kyivTime = ScheduleTrackerService.GetKyivNow().ToString("yyyy-MM-dd HH:mm:ss"),
    pendingBroadcasts = queue.PendingCount
}));

// Telegram Webhook endpoint
app.MapPost("/api/telegram/webhook", async (
    HttpContext context,
    [FromBody] Update update,
    ITelegramBotClient botClient,
    ITelegramBotHandler botHandler,
    IConfiguration config,
    CancellationToken ct) =>
{
    var expectedSecret = config["BotSettings:SecretToken"];
    if (!string.IsNullOrWhiteSpace(expectedSecret))
    {
        var providedSecret = context.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
        if (providedSecret != expectedSecret)
        {
            return Results.Unauthorized();
        }
    }

    await botHandler.HandleUpdateAsync(botClient, update, ct);
    return Results.Ok();
});

await app.RunAsync();
