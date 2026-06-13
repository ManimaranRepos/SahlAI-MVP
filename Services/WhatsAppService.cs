using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SahlAI.Api.Services;

public interface IWhatsAppService
{
    Task SendTextAsync(string toPhone, string message, CancellationToken ct = default);
}

/// <summary>
/// Sends WhatsApp text messages via the Meta Graph API (WhatsApp Cloud API).
///   POST https://graph.facebook.com/{version}/{phoneNumberId}/messages
///   Authorization: Bearer {accessToken}
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly WhatsAppOptions _opt;
    private readonly ILogger<WhatsAppService> _log;

    public WhatsAppService(IHttpClientFactory httpFactory, IOptions<WhatsAppOptions> opt, ILogger<WhatsAppService> log)
    {
        _httpFactory = httpFactory;
        _opt = opt.Value;
        _log = log;
    }

    public async Task SendTextAsync(string toPhone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken) || string.IsNullOrWhiteSpace(_opt.PhoneNumberId))
        {
            _log.LogWarning("WhatsApp not configured — would have sent to {To}: {Msg}", toPhone, message);
            return;
        }

        var url = $"https://graph.facebook.com/{_opt.GraphApiVersion}/{_opt.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhone,
            type = "text",
            text = new { preview_url = false, body = message }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", $"Bearer {_opt.AccessToken}");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var client = _httpFactory.CreateClient();
        try
        {
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _log.LogError("WhatsApp send failed {Status}: {Body}", resp.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WhatsApp send threw");
        }
    }
}
