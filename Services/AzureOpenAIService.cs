using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SahlAI.Api.Services;

public interface IAzureOpenAIService
{
    /// <summary>Sends the system prompt + history + user message to Azure OpenAI and returns the reply text.</summary>
    Task<string> GetReplyAsync(string systemPrompt, IReadOnlyList<ChatTurn> history, string userMessage, CancellationToken ct = default);
}

/// <summary>
/// Calls the Azure OpenAI Chat Completions REST API directly.
/// Endpoint shape:
///   {Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}
/// Auth header: "api-key: {ApiKey}"
/// </summary>
public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AzureOpenAIOptions _opt;
    private readonly ILogger<AzureOpenAIService> _log;

    public AzureOpenAIService(IHttpClientFactory httpFactory, IOptions<AzureOpenAIOptions> opt, ILogger<AzureOpenAIService> log)
    {
        _httpFactory = httpFactory;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<string> GetReplyAsync(string systemPrompt, IReadOnlyList<ChatTurn> history, string userMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.Endpoint) || string.IsNullOrWhiteSpace(_opt.ApiKey))
        {
            _log.LogWarning("Azure OpenAI not configured — returning stub reply.");
            return "[Sahl AI not configured yet] You said: " + userMessage;
        }

        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        foreach (var turn in history)
            messages.Add(new { role = turn.Role, content = turn.Content });
        messages.Add(new { role = "user", content = userMessage });

        var payload = new
        {
            messages,
            max_tokens = _opt.MaxTokens,
            temperature = _opt.Temperature
        };

        var url = $"{_opt.Endpoint.TrimEnd('/')}/openai/deployments/{_opt.Deployment}/chat/completions?api-version={_opt.ApiVersion}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", _opt.ApiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogError("Azure OpenAI error {Status}: {Body}", resp.StatusCode, body);
                return "Sorry, I'm having trouble right now. A team member will follow up shortly.";
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? "Thanks! Could you tell me a little more so I can help?"
                : content.Trim();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Azure OpenAI call failed");
            return "Sorry, something went wrong. Please try again in a moment.";
        }
    }
}
