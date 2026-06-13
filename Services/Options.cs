namespace SahlAI.Api.Services;

/// <summary>Azure OpenAI connection settings (bind from "AzureOpenAI" config section).</summary>
public class AzureOpenAIOptions
{
    /// <example>https://your-resource.openai.azure.com</example>
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    /// <summary>The deployment name you created in Azure OpenAI Studio (e.g. "gpt-4o").</summary>
    public string Deployment { get; set; } = "gpt-4o";
    public string ApiVersion { get; set; } = "2024-08-01-preview";
    public int MaxTokens { get; set; } = 400;
    public float Temperature { get; set; } = 0.4f;
}

/// <summary>WhatsApp Cloud API settings (bind from "WhatsApp" config section).</summary>
public class WhatsAppOptions
{
    /// <summary>Permanent or temporary access token from Meta for Developers.</summary>
    public string AccessToken { get; set; } = "";
    /// <summary>Phone number ID from the WhatsApp Cloud API dashboard.</summary>
    public string PhoneNumberId { get; set; } = "";
    /// <summary>Token you choose; must match what you enter in the Meta webhook setup.</summary>
    public string VerifyToken { get; set; } = "sahl-verify-token";
    public string GraphApiVersion { get; set; } = "v21.0";
}

/// <summary>Bot behaviour settings (bind from "Bot" config section).</summary>
public class BotOptions
{
    /// <summary>realestate | healthcare | retail — selects the system prompt persona.</summary>
    public string Industry { get; set; } = "realestate";
    public string BusinessName { get; set; } = "Dubai Homes Realty";
    public int HistoryTurns { get; set; } = 10;
}
