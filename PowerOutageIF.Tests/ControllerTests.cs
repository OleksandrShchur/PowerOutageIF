using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Xunit;
using PowerOutageIF.Controllers;
using PowerOutageIF.Services;

namespace PowerOutageIF.Tests;

public class ControllerTests
{
    [Fact]
    public void HealthController_GetHealth_ReturnsOkWithStatus()
    {
        var queue = new BroadcastQueue();
        var controller = new HealthController(queue);

        var result = controller.GetHealth() as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void HealthController_GetRoot_ReturnsTextContent()
    {
        var queue = new BroadcastQueue();
        var controller = new HealthController(queue);

        var result = controller.GetRoot() as ContentResult;

        Assert.NotNull(result);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Contains("PowerOutageIF", result.Content);
    }

    [Fact]
    public async Task TelegramWebhookController_WhenSecretInvalid_ReturnsUnauthorized()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotSettings:SecretToken"] = "correct_secret"
            })
            .Build();

        var botClientMock = new Mock<ITelegramBotClient>();
        var botHandlerMock = new Mock<ITelegramBotHandler>();

        var controller = new TelegramWebhookController(botClientMock.Object, botHandlerMock.Object, config);

        var update = new Update { Id = 123 };
        var result = await controller.PostWebhook(update, "wrong_secret", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task TelegramWebhookController_WhenSecretValid_DispatchesAndReturnsOk()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotSettings:SecretToken"] = "correct_secret"
            })
            .Build();

        var botClientMock = new Mock<ITelegramBotClient>();
        var botHandlerMock = new Mock<ITelegramBotHandler>();

        var controller = new TelegramWebhookController(botClientMock.Object, botHandlerMock.Object, config);

        var update = new Update { Id = 123 };
        var result = await controller.PostWebhook(update, "correct_secret", CancellationToken.None);

        Assert.IsType<OkResult>(result);
        botHandlerMock.Verify(h => h.HandleUpdateAsync(botClientMock.Object, update, It.IsAny<CancellationToken>()), Times.Once);
    }
}
