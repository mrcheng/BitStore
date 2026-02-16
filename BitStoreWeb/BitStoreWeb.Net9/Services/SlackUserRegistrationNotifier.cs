using System.Net.Http.Json;
using BitStoreWeb.Net9.Models;

namespace BitStoreWeb.Net9.Services;

public class SlackUserRegistrationNotifier : IUserRegistrationNotifier
{
    private const string WebhookConfigKey = "Slack:RegistrationWebhookUrl";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SlackUserRegistrationNotifier> _logger;

    public SlackUserRegistrationNotifier(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SlackUserRegistrationNotifier> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyUserRegisteredAsync(AppUser user)
    {
        var webhookUrl = _configuration[WebhookConfigKey];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        var payload = new
        {
            text = $"New BitStore account registered: {user.UserName} (role: {user.Role}) at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC."
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Slack registration webhook failed with status {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slack registration webhook call failed.");
        }
    }
}
