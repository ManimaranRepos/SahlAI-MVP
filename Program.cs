using SahlAI.Api;
using SahlAI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration sections -----
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));

// ----- Services -----
builder.Services.AddHttpClient();                                  // IHttpClientFactory
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
builder.Services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
builder.Services.AddSingleton<IWhatsAppService, WhatsAppService>();
builder.Services.AddSingleton<ChatOrchestrator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Health check
app.MapGet("/", () => Results.Ok(new { service = "Sahl AI", status = "running", time = DateTime.UtcNow }));

// Register the WhatsApp webhook endpoints (GET verify + POST receive)
app.MapWhatsAppWebhook();

// Simple test endpoint so you can try the AI without WhatsApp configured.
// POST /api/chat/test  { "from": "test-user", "text": "Book a viewing in Marina" }
app.MapPost("/api/chat/test", async (TestChatRequest req, ChatOrchestrator orchestrator) =>
{
    var reply = await orchestrator.HandleIncomingAsync(req.From ?? "test-user", req.Text ?? "");
    return Results.Ok(new { reply });
});

app.Run();

public record TestChatRequest(string? From, string? Text);
