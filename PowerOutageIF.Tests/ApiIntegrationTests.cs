using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using PowerOutageIF.Services;

namespace PowerOutageIF.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task GetQueuesAsync_Returns12QueuesFromLiveOblenergoApi()
    {
        var config = new ConfigurationBuilder().Build();
        using var httpClient = new HttpClient();
        var client = new PrykarpattiaApiClient(httpClient, config, NullLogger<PrykarpattiaApiClient>.Instance);

        var queues = await client.GetQueuesAsync();

        Assert.NotNull(queues);
        Assert.Equal(12, queues.Count);
        Assert.Contains(queues, q => q.Code == "1.1");
        Assert.Contains(queues, q => q.Code == "6.2");
    }

    [Fact]
    public async Task GetScheduleByQueueAsync_ReturnsScheduleForQueue1_1()
    {
        var config = new ConfigurationBuilder().Build();
        using var httpClient = new HttpClient();
        var client = new PrykarpattiaApiClient(httpClient, config, NullLogger<PrykarpattiaApiClient>.Instance);

        var schedules = await client.GetScheduleByQueueAsync("1.1");

        Assert.NotNull(schedules);
        Assert.NotEmpty(schedules);
        Assert.True(schedules[0].Queues.ContainsKey("1.1"));
    }
}
