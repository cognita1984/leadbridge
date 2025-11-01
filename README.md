# 🧱 LeadBridge AU

Automatically bridge ServiceSeeking leads to tradies in real-time using Chrome extension + Azure backend.

## Overview

LeadBridge AU detects new ServiceSeeking leads and automatically:
1. Calls the tradie (business owner) via Azure Communication Services
2. On keypress confirmation, bridges to the customer
3. Logs all events for analytics and billing

## Architecture

```
[ServiceSeeking] → [Chrome Extension]
         ↓
     [Azure Function API]
         ↓
[ACS Call Automation] → Tradie & Customer
         ↓
 [Azure Table Storage + Application Insights]
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Client | Chrome Extension (Manifest v3) |
| Backend | Azure Functions (.NET 8 isolated) |
| Telephony | Azure Communication Services (Call Automation) |
| Storage | Azure Table Storage |
| Monitoring | Application Insights |
| Deployment | GitHub Actions + Bicep |

## Repository Structure

```
leadbridge/
├── leadbridge-extension/     # Chrome extension
│   ├── manifest.json
│   ├── background.js        # Service worker & lead processing
│   ├── content.js           # Real-time lead monitoring
│   ├── popup.html          # UI
│   └── popup.js
├── leadbridge-backend/      # Azure Functions backend
│   ├── LeadBridge.csproj
│   ├── Program.cs
│   ├── HttpTriggerLead.cs  # API endpoints
│   ├── Models/             # Data models
│   ├── Services/           # ACS service
│   └── Storage/            # Table Storage
├── infra/
│   └── main.bicep          # Azure infrastructure
└── .github/workflows/
    └── deploy.yml          # CI/CD pipeline
```

## Prerequisites

- Azure subscription with credits
- Azure Communication Services (ACS) resource with phone number
- GitHub account
- Chrome browser (for extension)
- .NET 8 SDK (for local development)
- Azure CLI (for local development)

## Quick Start

### Option 1: Test Locally (Recommended First)

1. **Load Extension:** Follow **[TESTING.md](TESTING.md)** for step-by-step local testing guide
2. **Verify Selectors:** Ensure lead detection works with ServiceSeeking
3. **Test Extraction:** Validate customer name, job type, and location are correctly extracted

### Option 2: Deploy to Azure

1. **Deploy Backend:** Follow **[DEPLOYMENT.md](DEPLOYMENT.md)** for Azure deployment
2. **Configure Extension:** Update API endpoint to your Azure Function URL
3. **Test End-to-End:** Verify leads trigger ACS calls to tradie

---

## Deployment

### 1. Fork and Clone Repository

```bash
git clone https://github.com/cognita1984/leadbridge.git
cd leadbridge
```

### 2. Configure GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions

Add the following secrets:

| Secret Name | Description | How to Get |
|-------------|-------------|------------|
| `AZURE_CREDENTIALS` | Azure service principal JSON | See below |
| `ACS_CONNECTION_STRING` | Azure Communication Services connection string | From your existing ACS resource |
| `ACS_PHONE_NUMBER` | ACS phone number | E.g., `+61412345678` |

#### Creating Azure Service Principal

```bash
az login

az ad sp create-for-rbac \
  --name "LeadBridge-GitHub-Actions" \
  --role contributor \
  --scopes /subscriptions/{subscription-id} \
  --sdk-auth
```

Copy the JSON output and paste it as `AZURE_CREDENTIALS` secret.

### 3. Deploy to Azure

Push to main branch to trigger deployment:

```bash
git add .
git commit -m "Initial deployment"
git push origin main
```

The GitHub Actions workflow will:
- Create resource group `leadbridge`
- Provision all Azure resources (Storage, Functions, App Insights)
- Build and deploy the backend
- Package the Chrome extension

### 4. Install Chrome Extension

1. Download the extension artifact from GitHub Actions run
2. Extract `leadbridge-extension.zip`
3. Open Chrome → `chrome://extensions/`
4. Enable "Developer mode"
5. Click "Load unpacked" → select extracted folder

### 5. Configure Extension

1. Log into ServiceSeeking in Chrome
2. Click LeadBridge extension icon
3. Enter your tradie phone number (Australian format)
4. Toggle "Enable Monitoring" ON
5. Click "Save Settings"

## Local Development

### Backend

```bash
cd leadbridge-backend

# Install dependencies
dotnet restore

# Update local.settings.json with your values
cp local.settings.json local.settings.json.user
# Edit local.settings.json.user with your ACS credentials

# Run locally
func start
```

### Extension

1. Load unpacked extension in Chrome
2. Update `CONFIG.API_ENDPOINT` in `background.js` to `http://localhost:7071/api/newlead`
3. Test with ServiceSeeking

## Configuration

### Azure Function App Settings

Set these in Azure Portal → Function App → Configuration:

```
ACS_CONNECTION_STRING=endpoint=https://...
ACS_PHONE_NUMBER=+61XXXXXXXXXX
ACS_CALLBACK_URI=https://leadbridgefunc-prod.azurewebsites.net/api/callback
COGNITIVE_SERVICES_ENDPOINT=https://australiaeast.api.cognitive.microsoft.com/
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;...
```

### Chrome Extension

Update `background.js`:

```javascript
const CONFIG = {
  API_ENDPOINT: 'https://YOUR-FUNCTION-APP.azurewebsites.net/api/newlead'
};
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/newlead` | POST | Receive lead from extension |
| `/api/callback` | POST | ACS callback events |
| `/api/health` | GET | Health check |

## Monitoring

### Application Insights

View telemetry in Azure Portal:
- Function invocations
- Call success/failure rates
- Response times
- Exceptions

### Table Storage

Two tables are created:
- `leads` - All received leads
- `callevents` - Call logs with duration and status

## Cost Estimate

| Resource | Monthly Cost |
|----------|-------------|
| Function App (Consumption) | ~$5 |
| ACS Calls ($0.03/min × 2 legs) | ~$0.10 per call |
| Table Storage | <$1 |
| Application Insights | ~$5 |
| **Total** | **~$15/month** + call volume |

## Troubleshooting

### Extension not detecting leads

1. Check Chrome console (F12) for errors
2. Verify ServiceSeeking page structure hasn't changed
3. Update selectors in `extractLeadsFromPage()`

### Calls not being placed

1. Check Function App logs in Azure Portal
2. Verify ACS credentials are correct
3. Ensure ACS phone number has outbound calling enabled
4. Check Application Insights for exceptions

### Build failures

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Roadmap

- [ ] Add email/SMS fallback
- [ ] Support HiPages and Oneflare
- [ ] React dashboard for analytics
- [ ] Stripe billing integration
- [ ] AI-powered SMS responses
- [ ] Multi-tradie support

## License

MIT

## Support

For issues, open a GitHub issue or check Azure Function logs.

## Credits

Built with:
- Azure Communication Services
- Azure Functions
- Chrome Extensions API
- .NET 8
