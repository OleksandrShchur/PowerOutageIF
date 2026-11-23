using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using PowerOutageIF.Services;

namespace PowerOutageIF.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramBotHandler _botHandler;
    private readonly IConfiguration _configuration;

    public TelegramWebhookController(
        ITelegramBotClient botClient,
        ITelegramBotHandler botHandler,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _botHandler = botHandler;
        _configuration = configuration;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook(
        [FromBody] Update update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
        CancellationToken cancellationToken)
    {
        var expectedSecret = _configuration["BotSettings:SecretToken"];
        if (!string.IsNullOrWhiteSpace(expectedSecret) && secretToken != expectedSecret)
        {
            return Unauthorized();
        }

        await _botHandler.HandleUpdateAsync(_botClient, update, cancellationToken);
        return Ok();
    }
}
