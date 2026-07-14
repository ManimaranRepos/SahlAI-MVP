# SahlAI-MVP Deployment Guide

## Live Demo
**🚀 Coming Soon:** Deploy to Azure App Service (UAE North region)

## Quick Start (Local)

```bash
# Clone and setup
git clone https://github.com/ManimaranRepos/SahlAI-MVP.git
cd SahlAI-MVP

# Configure environment
export AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com"
export AzureOpenAI__ApiKey="YOUR-KEY"
export AzureOpenAI__Deployment="gpt-4o"
export WhatsApp__VerifyToken="sahl-verify-token"

# Run
dotnet restore
dotnet run
```

## Azure Deployment (Production)

### Step 1: Create Azure Resources

```bash
# Login to Azure
az login

# Create resource group in UAE North (data residency)
az group create -n sahlai-rg -l uaenorth

# Create App Service Plan (Linux, B1 tier - $10/month)
az appservice plan create \
  -g sahlai-rg \
  -n sahlai-plan \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  -g sahlai-rg \
  -p sahlai-plan \
  -n sahlai-api \
  --runtime "DOTNETCORE:8.0"
```

### Step 2: Configure Secrets

```bash
# Set configuration as app settings (NEVER commit keys)
az webapp config appsettings set \
  -g sahlai-rg \
  -n sahlai-api \
  --settings \
  AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com" \
  AzureOpenAI__ApiKey="YOUR-KEY" \
  AzureOpenAI__Deployment="gpt-4o" \
  AzureOpenAI__ApiVersion="2024-08-01-preview" \
  WhatsApp__AccessToken="YOUR-WHATSAPP-TOKEN" \
  WhatsApp__PhoneNumberId="YOUR-PHONE-ID" \
  WhatsApp__VerifyToken="sahl-verify-token" \
  Bot__Industry="healthcare" \
  Bot__BusinessName="Al Noor Medical Clinic"

# Enable HTTPS only
az webapp update -g sahlai-rg -n sahlai-api --https-only true
```

### Step 3: Deploy Using GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0'
      
      - name: Restore & Build
        run: |
          dotnet restore
          dotnet build --configuration Release
      
      - name: Publish
        run: dotnet publish -c Release -o ${{env.DOTNET_ROOT}}/myapp
      
      - name: Deploy to Azure
        uses: azure/webapps-deploy@v2
        with:
          app-name: 'sahlai-api'
          package: ${{env.DOTNET_ROOT}}/myapp
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
```

### Step 4: Get Publish Profile

```bash
# Download and save as GitHub Secret
az webapp deployment list-publishing-profiles \
  -g sahlai-rg \
  -n sahlai-api \
  --xml > publish-profile.xml

# Copy content to GitHub Secrets as AZURE_WEBAPP_PUBLISH_PROFILE
```

### Step 5: Configure WhatsApp Webhook

1. Get your Azure App URL:
```bash
az webapp show -g sahlai-rg -n sahlai-api --query defaultHostName
# Output: sahlai-api.azurewebsites.net
```

2. In Meta Dashboard → WhatsApp → Configuration → Webhook:
   - **Callback URL:** `https://sahlai-api.azurewebsites.net/webhook`
   - **Verify Token:** `sahl-verify-token`
   - **Subscribe to:** `messages` field

3. Test the webhook (Meta will send a verification)

## Cost Estimate (UAE North)

| Service | Tier | Monthly Cost |
|---------|------|--------------|
| App Service | B1 (Linux) | ~$10 |
| Azure OpenAI | Pay-as-you-go | ~$5-50* |
| Total | | ~$15-60 |

*Depends on API calls; estimate 1000 conversations/month = ~$5-10

## Monitoring & Logs

```bash
# View live logs
az webapp log tail -g sahlai-rg -n sahlai-api

# View Azure Portal
# https://portal.azure.com → Resource Groups → sahlai-rg → sahlai-api
```

## Demo Instructions

Once deployed, test with WhatsApp:

1. **Add test number** in Meta dashboard
2. **Send WhatsApp message**: "I need a checkup, what are your hours?"
3. **Receive AI reply** from the deployed bot

Example conversation:
```
User: What services do you offer?
Bot: Al Noor Medical Clinic offers comprehensive healthcare services including 
     general practice, specialist consultations, diagnostics, and emergency care.
     How can we help you today?
```

## Troubleshooting

**Issue:** Webhook verification fails
```bash
# Check if the endpoint is accessible
curl -i https://sahlai-api.azurewebsites.net/health
# Should return 200 OK
```

**Issue:** Azure OpenAI errors
```bash
# Verify configuration
az webapp config appsettings list -g sahlai-rg -n sahlai-api

# Test API directly
curl -X POST https://sahlai-api.azurewebsites.net/api/chat/test \
  -H "Content-Type: application/json" \
  -d '{"from":"+971500000000","text":"Hello"}'
```

**Issue:** WhatsApp messages not received
- Check Meta dashboard → Logs for webhook errors
- Verify webhook URL is publicly accessible (not localhost)
- Confirm subscription to "messages" field

## Next Steps

1. ✅ Deploy to Azure (this guide)
2. 🔄 Add RAG with Azure AI Search for knowledge base
3. 🔄 Implement persistent storage (SQL Server)
4. 🔄 Add webhook signature verification for security
5. 🔄 Multi-tenant support (serve multiple clients)

---

**Questions?** Check the main [README.md](README.md) or the [SahlAI GitHub repo](https://github.com/ManimaranRepos/SahlAI-MVP)
