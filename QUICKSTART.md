# LeadBridge AU - Quick Start Guide

## Project Status ✅

All components have been scaffolded and are ready for deployment!

### What's Been Built

1. **Chrome Extension** ✅
   - Manifest v3 configuration
   - Background service worker with polling logic
   - Popup UI with settings
   - Lead detection (placeholder selectors - needs ServiceSeeking analysis)

2. **Azure Backend** ✅
   - .NET 8 Azure Functions
   - HTTP endpoints for lead processing
   - ACS Call Automation integration
   - Table Storage for leads and call events
   - Application Insights telemetry

3. **Infrastructure as Code** ✅
   - Bicep template for Azure resources
   - GitHub Actions CI/CD pipeline
   - Automated deployment workflow

4. **Testing Tools** ✅
   - Playwright script to study ServiceSeeking

## Next Steps

### Step 1: Study ServiceSeeking (NOW)

You mentioned you want to study ServiceSeeking to understand the HTTP calls and page structure. Let's do that now:

```bash
cd tools
npm run study-serviceseeking
```

This will:
- Open a Chromium browser
- Navigate to ServiceSeeking
- Monitor all network traffic
- Capture page HTML structure
- Detect potential lead elements

**Please login to ServiceSeeking when the browser opens, then navigate to your leads page.**

Press Ctrl+C when done to save all logs to `tools/output/`.

### Step 2: Update Extension Based on Findings

After studying ServiceSeeking, update `leadbridge-extension/background.js`:

1. If ServiceSeeking has an API:
   - Update `CONFIG.SERVICE_SEEKING_API` with the actual endpoint
   - Update lead parsing logic

2. If using content script scraping:
   - Update CSS selectors in `extractLeadsFromPage()` function
   - Based on what we find in the HTML

### Step 3: Configure Azure Credentials

Before deploying, you need to set up:

1. **Create Azure Service Principal**:
   ```bash
   az login
   az ad sp create-for-rbac \
     --name "LeadBridge-GitHub-Actions" \
     --role contributor \
     --scopes /subscriptions/{your-subscription-id} \
     --sdk-auth
   ```

2. **Get ACS Connection String**:
   ```bash
   az communication list-key \
     --name YOUR_ACS_RESOURCE \
     --resource-group YOUR_RESOURCE_GROUP
   ```

3. **Add GitHub Secrets**:
   - Go to GitHub repo → Settings → Secrets
   - Add:
     - `AZURE_CREDENTIALS` (JSON from step 1)
     - `ACS_CONNECTION_STRING` (from step 2)
     - `ACS_PHONE_NUMBER` (your ACS phone number)

### Step 4: Push to GitHub and Deploy

```bash
# Check status
git status

# Push to GitHub (triggers deployment)
git push -u origin main
```

GitHub Actions will automatically:
- Create Azure resource group "leadbridge"
- Deploy all infrastructure
- Build and deploy the function app
- Package the Chrome extension

### Step 5: Install Chrome Extension

1. Download extension artifact from GitHub Actions
2. Extract ZIP
3. Load in Chrome: `chrome://extensions/` → "Load unpacked"
4. Configure with your tradie phone number
5. Enable monitoring

## Current File Structure

```
leadbridge/
├── .github/workflows/
│   └── deploy.yml              # CI/CD pipeline
├── infra/
│   └── main.bicep              # Azure infrastructure
├── leadbridge-backend/
│   ├── LeadBridge.csproj       # .NET project
│   ├── Program.cs              # Function host
│   ├── HttpTriggerLead.cs      # API endpoints
│   ├── Models/                 # Data models
│   ├── Services/               # ACS service
│   └── Storage/                # Table Storage
├── leadbridge-extension/
│   ├── manifest.json           # Extension config
│   ├── background.js           # Polling logic
│   ├── popup.html              # UI
│   └── popup.js                # UI controller
├── tools/
│   ├── package.json
│   ├── study-serviceseeking.js # Playwright script
│   └── output/                 # Study results (created on run)
├── README.md                   # Full documentation
├── DEPLOYMENT.md               # Detailed deployment guide
└── QUICKSTART.md               # This file
```

## Git Status

Repository is initialized and ready to push:

```bash
# View current status
git status

# View commit
git log --oneline

# Push to GitHub
git push -u origin main
```

## Important Notes

⚠️ **Before Pushing:**
1. Study ServiceSeeking first (see Step 1)
2. Update extension selectors based on findings
3. Configure GitHub secrets
4. Ensure you have ACS credentials ready

⚠️ **Cost Management:**
- All resources use consumption/serverless tiers
- Set up budget alerts in Azure
- Monitor ACS call costs (~$0.10 per bridged call)

⚠️ **Security:**
- Never commit `local.settings.json` with real credentials
- Use GitHub Secrets for sensitive data
- ACS connection string is in Azure Key Vault in production

## Testing Locally

### Backend

```bash
cd leadbridge-backend

# Install .NET 8 SDK if not already installed
# https://dotnet.microsoft.com/download/dotnet/8.0

# Install Azure Functions Core Tools
# https://docs.microsoft.com/azure/azure-functions/functions-run-local

# Run locally
func start
```

### Extension

1. Load unpacked in Chrome
2. Update API endpoint to `http://localhost:7071/api/newlead`
3. Test with manual trigger

## Troubleshooting

**Playwright script won't start:**
```bash
cd tools
npx playwright install chromium
```

**Git push fails:**
- Check GitHub repo exists
- Verify remote URL: `git remote -v`
- Ensure you have push access

**Can't find .NET 8:**
- Download from https://dotnet.microsoft.com/download/dotnet/8.0
- Verify: `dotnet --version`

## Support

- Full docs: `README.md`
- Deployment guide: `DEPLOYMENT.md`
- Issues: GitHub Issues tab

---

🚀 **Ready to go! Start with Step 1 (study ServiceSeeking) when you're ready.**
