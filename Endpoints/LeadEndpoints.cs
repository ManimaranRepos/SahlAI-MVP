using System.Text;
using System.Text.Encodings.Web;
using SahlAI.Api.Services;

namespace SahlAI.Api;

public record ChatRequest(string? SessionId, string? Text);

public static class LeadEndpoints
{
    /// <summary>Chat endpoint used by the website widget — orchestrates a reply and logs the turn.</summary>
    public static IEndpointRouteBuilder MapChatApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", async (ChatRequest req, ChatOrchestrator orch, ILeadStore leads) =>
        {
            var session = string.IsNullOrWhiteSpace(req.SessionId) ? "anon-" + Guid.NewGuid().ToString("n")[..8] : req.SessionId!;
            var text = req.Text ?? "";
            var reply = await orch.HandleIncomingAsync(session, text);
            leads.LogTurn(session, text, reply);
            return Results.Ok(new { reply });
        });
        return app;
    }

    /// <summary>Simple key-protected dashboard of captured leads and their conversations.</summary>
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/leads", (HttpContext ctx, ILeadStore store, IConfiguration cfg) =>
        {
            var expected = cfg["Admin:Key"] ?? "sahl-admin-2026";
            if (ctx.Request.Query["key"] != expected)
                return Results.Content(
                    "<html><body style='font-family:sans-serif;padding:40px'><h2>401 — Unauthorized</h2><p>Append <code>?key=YOUR_ADMIN_KEY</code> to the URL.</p></body></html>",
                    "text/html; charset=utf-8");

            var enc = HtmlEncoder.Default;
            var leads = store.GetLeads();
            var sb = new StringBuilder();
            sb.Append("""
                <!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>Sahl AI — Captured Leads</title>
                <style>
                  body{font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:#f4f5f7;margin:0;padding:24px;color:#1a2a4a}
                  h1{font-size:22px;margin:0 0 4px}.sub{color:#777;margin-bottom:20px;font-size:14px}
                  table{width:100%;border-collapse:collapse;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.08);border-radius:8px;overflow:hidden}
                  th,td{padding:11px 14px;text-align:left;border-bottom:1px solid #eee;font-size:14px;vertical-align:top}
                  th{background:#1a2a4a;color:#fff;font-weight:600}
                  tr:hover{background:#faf8f2}
                  .phone{color:#0a7d2c;font-weight:600}.none{color:#bbb}
                  details{margin:0}summary{cursor:pointer;color:#c9a55c;font-weight:600}
                  .msg{margin:6px 0;padding:6px 10px;border-radius:6px;font-size:13px;max-width:600px}
                  .user{background:#eef2ff}.assistant{background:#f3f3f3}
                  .count{display:inline-block;background:#c9a55c;color:#1a2a4a;border-radius:10px;padding:1px 9px;font-weight:700;font-size:12px}
                </style></head><body>
                """);
            sb.Append($"<h1>Captured Leads</h1><div class='sub'>{leads.Count} visitor session(s) &bull; Dubai Homes Realty</div>");
            sb.Append("<table><tr><th>Name</th><th>Phone</th><th>Msgs</th><th>First seen</th><th>Last seen</th><th>Conversation</th></tr>");

            foreach (var l in leads)
            {
                var name = string.IsNullOrWhiteSpace(l.Name) ? "<span class='none'>—</span>" : enc.Encode(l.Name);
                var phone = string.IsNullOrWhiteSpace(l.Phone) ? "<span class='none'>—</span>" : $"<span class='phone'>{enc.Encode(l.Phone)}</span>";
                sb.Append($"<tr><td>{name}</td><td>{phone}</td><td><span class='count'>{l.MessageCount}</span></td><td>{enc.Encode(l.FirstSeen)}</td><td>{enc.Encode(l.LastSeen)}</td><td><details><summary>view</summary>");
                foreach (var m in store.GetMessages(l.SessionId))
                {
                    var cls = m.Role == "user" ? "user" : "assistant";
                    var who = m.Role == "user" ? "Visitor" : "AI";
                    sb.Append($"<div class='msg {cls}'><b>{who}:</b> {enc.Encode(m.Text)}</div>");
                }
                sb.Append("</details></td></tr>");
            }
            sb.Append("</table></body></html>");
            return Results.Content(sb.ToString(), "text/html; charset=utf-8");
        });
        return app;
    }
}
