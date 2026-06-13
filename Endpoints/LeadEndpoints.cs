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
            var stats = store.GetVisitStats();
            var bySource = store.GetVisitsBySource();
            var visits = store.GetRecentVisits(40);
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
                  .cards{display:flex;gap:14px;flex-wrap:wrap;margin:0 0 18px}
                  .card{background:#fff;border-radius:8px;box-shadow:0 1px 4px rgba(0,0,0,.08);padding:14px 20px;min-width:118px}
                  .card .n{font-size:26px;font-weight:700;color:#1a2a4a}
                  .card .l{font-size:11px;color:#888;text-transform:uppercase;letter-spacing:.5px;margin-top:2px}
                  .src{margin:0 0 22px;font-size:13px;color:#555}.src b{color:#1a2a4a}
                  .pill{display:inline-block;background:#eef2ff;border-radius:12px;padding:2px 10px;margin:2px 4px 2px 0;font-size:12px}
                  h2{font-size:17px;margin:26px 0 10px}
                  .ua{color:#888;font-size:12px;max-width:340px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;display:inline-block;vertical-align:bottom}
                </style></head><body>
                """);
            sb.Append($"<h1>Captured Leads</h1><div class='sub'>{leads.Count} chat session(s) &bull; Dubai Homes Realty</div>");

            var conv = stats.Total > 0 ? stats.ChatSessions * 100 / stats.Total : 0;
            sb.Append("<div class='cards'>");
            sb.Append($"<div class='card'><div class='n'>{stats.Total}</div><div class='l'>Total visits</div></div>");
            sb.Append($"<div class='card'><div class='n'>{stats.Today}</div><div class='l'>Visits today</div></div>");
            sb.Append($"<div class='card'><div class='n'>{stats.Week}</div><div class='l'>Visits (7 days)</div></div>");
            sb.Append($"<div class='card'><div class='n'>{stats.ChatSessions}</div><div class='l'>Chat sessions</div></div>");
            sb.Append($"<div class='card'><div class='n'>{conv}%</div><div class='l'>Visit &rarr; chat</div></div>");
            sb.Append("</div>");

            if (bySource.Count > 0)
            {
                sb.Append("<div class='src'><b>Traffic by source:</b> ");
                foreach (var s in bySource)
                    sb.Append($"<span class='pill'>{enc.Encode(s.Source)} &middot; {s.Count}</span>");
                sb.Append("</div>");
            }

            sb.Append("<h2>Captured leads</h2>");
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
            sb.Append("</table>");

            sb.Append($"<h2>Recent visits <span class='count'>{visits.Count}</span></h2>");
            sb.Append("<table><tr><th>Time (UTC)</th><th>Source</th><th>IP</th><th>Device / browser</th><th>Referrer</th></tr>");
            foreach (var v in visits)
            {
                var vsrc = string.IsNullOrWhiteSpace(v.Source) ? "<span class='none'>(direct)</span>" : enc.Encode(v.Source!);
                var vip = string.IsNullOrWhiteSpace(v.Ip) ? "<span class='none'>—</span>" : enc.Encode(v.Ip!);
                var vua = string.IsNullOrWhiteSpace(v.UserAgent)
                    ? "<span class='none'>—</span>"
                    : $"<span class='ua' title='{enc.Encode(v.UserAgent!)}'>{enc.Encode(v.UserAgent!)}</span>";
                var vref = string.IsNullOrWhiteSpace(v.Referrer) ? "<span class='none'>—</span>" : enc.Encode(v.Referrer!);
                sb.Append($"<tr><td>{enc.Encode(v.CreatedAt)}</td><td>{vsrc}</td><td>{vip}</td><td>{vua}</td><td>{vref}</td></tr>");
            }
            sb.Append("</table></body></html>");
            return Results.Content(sb.ToString(), "text/html; charset=utf-8");
        });
        return app;
    }
}
