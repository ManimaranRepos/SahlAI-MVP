using System.Text.Json;
using Microsoft.Extensions.Options;
using SahlAI.Api.Services;

namespace SahlAI.Api;

public static class WhatsAppWebhook
{
    public static void MapWhatsAppWebhook(this WebApplication app)
    {
        // ---- 1) GET /webhook  → Meta verification handshake ----
        // Meta calls this once when you register the webhook. Echo hub.challenge
        // back if the verify token matches what you configured.
        app.MapGet("/webhook", (HttpRequest request, IOptions<WhatsAppOptions> waOpt) =>
        {
            var mode = request.Query["hub.mode"];
            var token = request.Query["hub.verify_token"];
            var challenge = request.Query["hub.challenge"];

            if (mode == "subscribe" && token == waOpt.Value.VerifyToken)
                return Results.Text(challenge!); // must return the raw challenge

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        // ---- 2) POST /webhook  → incoming messages from customers ----
        app.MapPost("/webhook", async (
            HttpRequest request,
            ChatOrchestrator orchestrator,
            IWhatsAppService whatsapp,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger("WhatsAppWebhook");

            using var reader = new StreamReader(request.Body);
            var raw = await reader.ReadToEndAsync();

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                // Navigate: entry[].changes[].value.messages[]
                if (!root.TryGetProperty("entry", out var entries)) return Results.Ok();

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes)) continue;

                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value)) continue;
                        if (!value.TryGetProperty("messages", out var messages)) continue; // status callbacks have no "messages"

                        foreach (var msg in messages.EnumerateArray())
                        {
                            var from = msg.GetProperty("from").GetString() ?? "";
                            var type = msg.TryGetProperty("type", out var t) ? t.GetString() : "text";

                            // We handle text messages in the MVP. (Extend for buttons/media later.)
                            if (type != "text") continue;

                            var text = msg.GetProperty("text").GetProperty("body").GetString() ?? "";
                            log.LogInformation("Incoming from {From}: {Text}", from, text);

                            var reply = await orchestrator.HandleIncomingAsync(from, text);
                            await whatsapp.SendTextAsync(from, reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to process webhook payload: {Raw}", raw);
            }

            // Always return 200 quickly so Meta doesn't retry/disable the webhook.
            return Results.Ok();
        });
    }
}
