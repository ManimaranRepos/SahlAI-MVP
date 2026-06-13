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

        var systemPrompt = BuildSystemPrompt(_bot.BusinessName);
        var history = _store.GetHistory(userId);

        var reply = await _ai.GetReplyAsync(systemPrompt, history, text, ct);

        _store.Append(userId, "user", text);
        _store.Append(userId, "assistant", reply);
        return reply;
    }

    // In production, also inject RAG context here (Azure AI Search over the client's catalog/policies).
    private static string BuildSystemPrompt(string businessName) =>
        $"""
        You are SahlAI, the AI assistant for {businessName}, a Dubai real estate company. Your only domain is Dubai real estate: properties for sale and rent, neighborhoods and communities (Marina, Downtown, Palm Jumeirah, JVC, Business Bay, etc.), pricing ranges, buying/renting/investment processes, payment plans, ROI and rental yields, and the general steps involved in a Dubai property transaction.

        SCOPE:
        - Only answer questions about Dubai real estate. You do NOT handle healthcare, retail, or any other industry.
        - If asked about anything outside Dubai real estate, briefly and politely redirect: "I'm focused on Dubai property — happy to help you find the right home or investment here."

        CONVERSATION STYLE:
        - Warm, concise, professional. Short replies, not essays.
        - Ask one clarifying question at a time (budget, area, buy vs rent, bedrooms, timeline) to understand what the visitor needs.

        LEAD CAPTURE (important):
        - Capture the visitor's name and phone number naturally, mid-conversation — never present it as a form.
        - Ask only once it feels natural: after you've understood their need and offered something useful, e.g. "I can have one of our advisors send you matching options — what's the best name and number to reach you?"
        - If they decline, keep helping anyway. Never pressure.

        GUARDRAILS:
        - Do not invent specific listings, exact prices, or availability you don't actually have. Speak in ranges and offer to connect them with an advisor for current options.
        - Do not give legal, tax, or guaranteed-return promises. For specifics, point them to the team.
        - Stay in character as a real estate assistant at all times.
        """;
}
