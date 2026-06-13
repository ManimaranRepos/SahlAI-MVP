using Microsoft.Extensions.Options;

namespace SahlAI.Api.Services;

/// <summary>
/// Ties everything together: builds the industry system prompt, pulls history,
/// calls Azure OpenAI, stores the turn, and returns the reply text.
/// </summary>
public class ChatOrchestrator
{
    private readonly IAzureOpenAIService _ai;
    private readonly IConversationStore _store;
    private readonly BotOptions _bot;

    public ChatOrchestrator(IAzureOpenAIService ai, IConversationStore store, IOptions<BotOptions> bot)
    {
        _ai = ai;
        _store = store;
        _bot = bot.Value;
    }

    public async Task<string> HandleIncomingAsync(string userId, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Hello! How can I help you today?";

        var systemPrompt = BuildSystemPrompt(_bot.Industries, _bot.BusinessName);
        var history = _store.GetHistory(userId);

        var reply = await _ai.GetReplyAsync(systemPrompt, history, text, ct);

        _store.Append(userId, "user", text);
        _store.Append(userId, "assistant", reply);
        return reply;
    }

    // In production, also inject RAG context here (Azure AI Search over the client's catalog/policies).
    private static string BuildSystemPrompt(List<string> industries, string businessName)
    {
        var baseRules =
            $"You are the AI assistant for \"{businessName}\", a business in Dubai, UAE. " +
            "Be warm, concise, and professional. Detect the customer's language and reply in the SAME language " +
            "(support English and Arabic; Hindi if used). Use AED for prices and UAE context. " +
            "Your goals: answer questions, qualify the lead, and book an appointment/viewing. " +
            "When the customer wants to book, collect their NAME and PHONE/preferred time, then confirm. " +
            "If you cannot help, offer to connect a human agent. Never invent prices or availability you don't know — " +
            "ask a clarifying question instead.";

        var personas = industries
            .Select(i => i.ToLowerInvariant() switch
            {
                "healthcare" =>
                    "HEALTHCARE: Help with appointment booking, clinic timings, services offered, and insurance eligibility " +
                    "(e.g. Daman, AXA, Sukoon, Thiqa). Do NOT give medical diagnoses — for symptoms, recommend booking a " +
                    "consultation. Be reassuring and respect patient privacy.",
                "retail" =>
                    "RETAIL: Help with product questions, order tracking, offers, store locations, and returns. " +
                    "Encourage purchases and upsell relevant items politely.",
                _ =>
                    "REAL ESTATE: Help with property search (area, budget, bedrooms), prices, payment plans, and booking " +
                    "viewings. Qualify the buyer's budget and timeline. Mention off-plan and ready properties where relevant."
            })
            .ToList();

        if (personas.Count == 1)
            return baseRules + " " + personas[0];

        var combined =
            $" This business operates across {personas.Count} divisions. Identify which division the customer's query relates " +
            "to from context, then respond using the relevant expertise below:\n" +
            string.Join("\n", personas.Select((p, i) => $"{i + 1}. {p}"));

        return baseRules + combined;
    }
}
