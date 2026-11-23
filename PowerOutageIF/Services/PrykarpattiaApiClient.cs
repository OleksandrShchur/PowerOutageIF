using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PowerOutageIF.Models;

namespace PowerOutageIF.Services;

public class PrykarpattiaApiClient : IPrykarpattiaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PrykarpattiaApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PrykarpattiaApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<PrykarpattiaApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["OblenergoSettings:BaseUrl"] ?? "https://be-svitlo.oe.if.ua/";
        if (!baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    }

    public async Task<List<QueueInfoItem>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "gpv-queue-list");
            request.Content = new StringContent(string.Empty);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var queues = JsonSerializer.Deserialize<List<QueueInfoItem>>(content, JsonOptions);
            return queues ?? new List<QueueInfoItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні списку черг з обленерго");
            return new List<QueueInfoItem>();
        }
    }

    public async Task<List<DayScheduleDto>> GetScheduleByQueueAsync(string queueCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"schedule-by-queue?queue={Uri.EscapeDataString(queueCode)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var schedules = JsonSerializer.Deserialize<List<DayScheduleDto>>(content, JsonOptions);
            return schedules ?? new List<DayScheduleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні графіка для черги {QueueCode}", queueCode);
            return new List<DayScheduleDto>();
        }
    }
}
