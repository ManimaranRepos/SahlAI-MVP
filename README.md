# Sahl AI — MVP Scaffold (.NET 8 + Azure OpenAI + WhatsApp)

A minimal, **runnable** WhatsApp chatbot backend for Dubai SMEs.
Flow: **Customer → WhatsApp Cloud API → this .NET API → Azure OpenAI → reply back on WhatsApp.**

Dependency-light by design: it calls Azure OpenAI and the WhatsApp Graph API directly over HTTPS (no heavy SDKs), so it's easy to read, audit, and deploy.

---

## Project structure

```
SahlAI-MVP/
├── SahlAI.Api.csproj          # .NET 8 web project
├── Program.cs                 # DI wiring + endpoints (health, /webhook, /api/chat/test)
├── appsettings.json           # config (fill in your keys — see below)
├── Endpoints/
│   └── WhatsAppWebhook.cs      # GET /webhook (verify) + POST /webhook (receive messages)
└── Services/
    ├── Options.cs             # AzureOpenAI / WhatsApp / Bot settings classes
    ├── AzureOpenAIService.cs  # calls Azure OpenAI Chat Completions
    ├── WhatsAppService.cs     # sends WhatsApp text via Graph API
    ├── ConversationStore.cs   # in-memory per-user history (swap for Redis/SQL later)
    └── ChatOrchestrator.cs    # builds industry prompt, ties it all together
```

---

## 1. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An **Azure OpenAI** resource with a deployed model (e.g. `gpt-4o`) — ideally in **UAE North** region for data residency.
- A **Meta / WhatsApp Cloud API** app (free to start): https://developers.facebook.com → create app → add "WhatsApp".

---

## 2. Run it locally (test the AI without WhatsApp first)

```bash
cd SahlAI-MVP
dotnet restore
dotnet run
```

Open Swagger at the printed URL (e.g. `http://localhost:5000/swagger`).

Test the brain directly — no WhatsApp needed:

```bash
curl -X POST http://localhost:5000/api/chat/test \
  -H "Content-Type: application/json" \
  -d '{"from":"+971500000000","text":"I want a 2BR in Dubai Marina, what are the payment plans?"}'
```

> Before adding your Azure key it returns a stub reply, so you can confirm the pipeline runs. Add the key (step 3) to get real AI answers.

---

## 3. Configure Azure OpenAI

Fill `appsettings.json` (or better, use environment variables / user-secrets so keys never hit git):

```json
"AzureOpenAI": {
  "Endpoint": "https://YOUR-RESOURCE.openai.azure.com",
  "ApiKey": "your-key",
  "Deployment": "gpt-4o",
  "ApiVersion": "2024-08-01-preview"
}
```

Using environment variables instead (recommended):

```bash
export AzureOpenAI__Endpoint="https://your-resource.openai.azure.com"
export AzureOpenAI__ApiKey="your-key"
export AzureOpenAI__Deployment="gpt-4o"
```

Pick the business persona:

```json
"Bot": { "Industry": "healthcare", "BusinessName": "Al Noor Medical Clinic" }
```
`Industry` = `realestate` | `healthcare` | `retail`.

---

## 4. Connect WhatsApp Cloud API

1. In the Meta dashboard, get your **Phone Number ID** and a **temporary access token** (later create a permanent token via a System User).
2. Put them in config:
   ```json
   "WhatsApp": {
     "AccessToken": "EAAG...",
     "PhoneNumberId": "1234567890",
     "VerifyToken": "sahl-verify-token"
   }
   ```
3. **Expose your local server** so Meta can reach it (during development):
   ```bash
   # install ngrok, then:
   ngrok http 5000
   ```
   Copy the `https://....ngrok.io` URL.
4. In Meta → WhatsApp → Configuration → **Webhook**:
   - Callback URL: `https://....ngrok.io/webhook`
   - Verify token: `sahl-verify-token` (must match config)
   - Click **Verify and Save** → our `GET /webhook` handles the handshake.
   - Subscribe to the **messages** field.
5. Send a WhatsApp message to your test number → you'll get an AI reply. 🎉

---

## 5. Deploy to Azure (production — UAE region)

```bash
# one-time
az group create -n sahlai-rg -l uaenorth
az appservice plan create -g sahlai-rg -n sahlai-plan --sku B1 --is-linux
az webapp create -g sahlai-rg -p sahlai-plan -n sahlai-api --runtime "DOTNETCORE:8.0"

# set secrets as app settings (never commit keys)
az webapp config appsettings set -g sahlai-rg -n sahlai-api --settings \
  AzureOpenAI__Endpoint="https://...openai.azure.com" \
  AzureOpenAI__ApiKey="..." \
  AzureOpenAI__Deployment="gpt-4o" \
  WhatsApp__AccessToken="..." WhatsApp__PhoneNumberId="..." \
  WhatsApp__VerifyToken="sahl-verify-token" \
  Bot__Industry="healthcare" Bot__BusinessName="Al Noor Medical Clinic"

# publish
dotnet publish -c Release
# then deploy the publish folder (az webapp deploy / VS / GitHub Actions)
```

Point the Meta webhook to `https://sahlai-api.azurewebsites.net/webhook`.

---

## 6. What to build next (production hardening)

- **RAG**: ingest each client's catalog/policies into **Azure AI Search**; inject retrieved context in `ChatOrchestrator.BuildSystemPrompt` so answers are grounded (prices, services).
- **Persistent history & leads**: replace `InMemoryConversationStore` with Redis/SQL Server; write captured leads to the client's **CRM** (HubSpot/Zoho/Salesforce).
- **Webhook signature check**: validate Meta's `X-Hub-Signature-256` header for security.
- **Booking**: integrate a calendar (clinic PMS / agency CRM) to create real appointments.
- **Buttons & media**: handle WhatsApp interactive buttons, images, and templates.
- **Multi-tenant**: one deployment serving many clients, each with its own prompt + knowledge base.

---

## Notes

- This is a starter scaffold — keys are placeholders; nothing is committed.
- Keep customer/health data on **compliant infrastructure (Azure UAE region)**.
- Tested for structure & syntax; run `dotnet build` locally to produce binaries (the build couldn't run in the authoring sandbox due to blocked SDK download).
