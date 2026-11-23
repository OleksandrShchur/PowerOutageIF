using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using PowerOutageIF.Data;
using PowerOutageIF.Services;
using PowerOutageIF.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();

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

// Map Controllers (HealthController, TelegramWebhookController)
app.MapControllers();

await app.RunAsync();
