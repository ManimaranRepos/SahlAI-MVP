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
            return "Hello! 👋 How can I help you today?";

        var systemPrompt = BuildSystemPrompt(_bot.Industry, _bot.BusinessName);
        var history = _store.GetHistory(userId);

        var reply = await _ai.GetReplyAsync(systemPrompt, history, text, ct);

        _store.Append(userId, "user", text);
        _store.Append(userId, "assistant", reply);
        return reply;
    }

    /// <summary>
    /// Per-industry persona. In production you would also inject RAG context here
    /// (retrieved from Azure AI Search over the client's catalog / policies).
    /// </summary>
    private static string BuildSystemPrompt(string industry, string businessName)
    {
        var baseRules =
            $"You are the AI assistant for \"{businessName}\", a business in Dubai, UAE. " +
            "Be warm, concise, and professional. Detect the customer's language and reply in the SAME language " +
            "(support English and Arabic; Hindi if used). Use AED for prices and UAE context. " +
            "Your goals: answer questions, qualify the lead, and book an appointment/viewing. " +
            "When the customer wants to book, collect their NAME and PHONE/preferred time, then confirm. " +
            "If you cannot help, offer to connect a human agent. Never invent prices or availability you don't know — " +
            "ask a clarifying question instead.";

        var persona = industry.ToLowerInvariant() switch
        {
            "healthcare" =>
                " You represent a medical clinic. Help with appointment booking, clinic timings, services, and insurance " +
                "eligibility (e.g. Daman, AXA, Sukoon, Thiqa). Do NOT give medical diagnoses — for symptoms, recommend booking " +
                "a consultation. Be reassuring and respect patient privacy.",
            "retail" =>
                " You represent a retail/e-commerce store. Help with product questions, order tracking, offers, store " +
                "locations, and returns. Encourage purchases and upsell relevant items politely.",
            _ =>
                " You represent a real estate agency. Help with property search (area, budget, bedrooms), prices, payment " +
                "plans, and booking viewings. Qualify the buyer's budget and timeline."
        };

        return baseRules + persona;
    }
}
