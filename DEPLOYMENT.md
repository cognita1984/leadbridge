# LeadBridge AU - Deployment Guide

Complete step-by-step guide to deploy LeadBridge to Azure using GitHub Actions.

## Prerequisites Checklist

- [ ] Azure subscription (with $1,000 credits)
- [ ] Existing Azure Communication Services (ACS) resource with phone number
- [ ] GitHub account
- [ ] Chrome browser
- [ ] Git installed locally

## Step 1: Prepare Azure Credentials

### 1.1 Get Your Subscription ID

```bash
az login
az account list --output table
# Note your subscription ID
```

### 1.2 Create Service Principal for GitHub Actions

```bash
# Replace {subscription-id} with your actual subscription ID
az ad sp create-for-rbac \
  --name "LeadBridge-GitHub-Actions" \
  --role contributor \
  --scopes /subscriptions/{subscription-id} \
  --sdk-auth
```

Copy the entire JSON output (starts with `{` and ends with `}`). You'll need this for GitHub secrets.

Example output:
```json
{
  "clientId": "...",
  "clientSecret": "...",
  "subscriptionId": "...",
  "tenantId": "...",
  "activeDirectoryEndpointUrl": "...",
  "resourceManagerEndpointUrl": "...",
  "activeDirectoryGraphResourceId": "...",
  "sqlManagementEndpointUrl": "...",
  "galleryEndpointUrl": "...",
  "managementEndpointUrl": "..."
}
```

### 1.3 Get ACS Connection String and Phone Number

From your existing ACS resource:

```bash
# List ACS resources
az communication list --output table

# Get connection string (replace with your resource name and group)
az communication list-key \
  --name YOUR_ACS_RESOURCE_NAME \
  --resource-group YOUR_RESOURCE_GROUP
```

Note down:
- Connection string (format: `endpoint=https://...;accesskey=...`)
- Phone number (format: `+61XXXXXXXXXX`)

## Step 2: Set Up GitHub Repository

### 2.1 Create GitHub Repository

1. Go to https://github.com/new
2. Create a new repository named `leadbridge`
3. Make it private or public (your choice)
4. DO NOT initialize with README (we already have one)

### 2.2 Configure GitHub Secrets

1. Go to your repository → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add these three secrets:

| Secret Name | Value | Example |
|-------------|-------|---------|
| `AZURE_CREDENTIALS` | JSON from Step 1.2 | `{ "clientId": "...", ... }` |
| `ACS_CONNECTION_STRING` | From Step 1.3 | `endpoint=https://...;accesskey=...` |
| `ACS_PHONE_NUMBER` | Your ACS phone | `+61412345678` |

## Step 3: Push Code to GitHub

```bash
# Navigate to your project directory
cd C:\leadbridge

# Initialize git repository
git init

# Add all files
git add .

# Commit
git commit -m "Initial commit: LeadBridge AU"

# Add remote (replace with your GitHub username)
git remote add origin https://github.com/cognita1984/leadbridge.git

# Rename branch to main
git branch -M main

# Push to GitHub
git push -u origin main
```

## Step 4: Monitor Deployment

1. Go to your GitHub repository
2. Click "Actions" tab
3. You should see "Deploy LeadBridge to Azure" workflow running
4. Click on the workflow to see progress

The workflow will:
- ✅ Create resource group `leadbridge`
- ✅ Deploy infrastructure (Storage, Functions, App Insights)
- ✅ Build and deploy .NET backend
- ✅ Package Chrome extension

Expected duration: ~5-10 minutes

## Step 5: Verify Deployment

### 5.1 Check Azure Resources

```bash
# List all resources in the group
az resource list \
  --resource-group leadbridge \
  --output table
```

You should see:
- Storage account
- Function App
- App Service Plan
- Application Insights
- Log Analytics Workspace

### 5.2 Test Function App

```bash
# Get Function App URL
az functionapp show \
  --name leadbridgefunc-prod \
  --resource-group leadbridge \
  --query defaultHostName \
  --output tsv
```

Test health endpoint:
```bash
curl https://leadbridgefunc-prod.azurewebsites.net/api/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-11-02T...",
  "version": "1.0.0"
}
```

## Step 6: Install Chrome Extension

### 6.1 Download Extension

1. Go to GitHub → Actions → Latest workflow run
2. Scroll to "Artifacts" section
3. Download "chrome-extension" artifact
4. Extract the ZIP file

### 6.2 Update API Endpoint (if not auto-updated)

Edit `background.js`:
```javascript
const CONFIG = {
  API_ENDPOINT: 'https://leadbridgefunc-prod.azurewebsites.net/api/newlead'
};
```

### 6.3 Load Extension in Chrome

1. Open Chrome
2. Navigate to `chrome://extensions/`
3. Enable "Developer mode" (toggle in top right)
4. Click "Load unpacked"
5. Select the extracted `leadbridge-extension` folder

### 6.4 Configure Extension

1. Log into ServiceSeeking in another tab
2. Click LeadBridge icon in Chrome toolbar
3. Enter your tradie phone number (Australian format)
4. Toggle "Enable Monitoring" ON
5. Click "Save Settings"

## Step 7: Test End-to-End Flow

### 7.1 Manual Test

1. Click "Test Connection" in extension popup
2. Check Chrome console (F12) for logs
3. Check Azure Function logs:

```bash
az functionapp logs tail \
  --name leadbridgefunc-prod \
  --resource-group leadbridge
```

### 7.2 Simulate Lead

You can test the API directly:

```bash
curl -X POST https://leadbridgefunc-prod.azurewebsites.net/api/newlead \
  -H "Content-Type: application/json" \
  -d '{
    "leadId": "test-123",
    "customerName": "John Smith",
    "customerPhone": "+61412345678",
    "jobType": "Blocked drain",
    "location": "Glen Waverley",
    "tradiePhone": "+61400000000",
    "timestamp": "2025-11-02T10:00:00Z"
  }'
```

Expected:
- You should receive a call on the tradie phone
- Press "1" to proceed (or "2" to skip)
- Call details logged in Azure Table Storage

## Step 8: Monitor and Debug

### 8.1 View Application Insights

```bash
# Open in Azure Portal
az portal insights --resource-group leadbridge
```

Or go to: https://portal.azure.com → leadbridge-insights-prod

### 8.2 View Table Storage Data

```bash
# Install Azure Storage Explorer or use Azure Portal
# Navigate to: Storage Account → Tables → "leads" / "callevents"
```

### 8.3 Check Function Logs

```bash
# Live tail
az functionapp logs tail \
  --name leadbridgefunc-prod \
  --resource-group leadbridge

# Or view in Azure Portal → Function App → Log Stream
```

## Troubleshooting

### Issue: Workflow fails on "Deploy Bicep Template"

**Solution:**
- Ensure `AZURE_CREDENTIALS` secret is correctly formatted JSON
- Verify subscription ID in service principal has correct permissions
- Check Azure CLI version is up to date

### Issue: Function App deployment succeeds but calls fail

**Solution:**
1. Verify ACS secrets are set:
   ```bash
   az functionapp config appsettings list \
     --name leadbridgefunc-prod \
     --resource-group leadbridge
   ```
2. Check for `ACS_CONNECTION_STRING` and `ACS_PHONE_NUMBER`
3. Verify ACS phone number has outbound calling enabled

### Issue: Extension can't detect leads

**Solution:**
- ServiceSeeking may not have a public API
- You'll need to update `extractLeadsFromPage()` function to match their HTML structure
- Inspect ServiceSeeking page HTML and update CSS selectors accordingly

### Issue: CORS errors in extension

**Solution:**
- Ensure Function App has CORS enabled for `*` (already configured in Bicep)
- Check browser console for specific error
- Verify `Access-Control-Allow-Origin` header in function response

## Updating the Application

### Update Backend Code

```bash
# Make changes to backend
cd leadbridge-backend

# Commit and push
git add .
git commit -m "Update: description of changes"
git push origin main
```

GitHub Actions will automatically redeploy.

### Update Extension

1. Make changes to extension files
2. Increment version in `manifest.json`
3. Push to GitHub
4. Download new artifact from Actions
5. Reload extension in `chrome://extensions/`

## Cost Monitoring

### Set Up Budget Alert

```bash
az consumption budget create \
  --budget-name "leadbridge-monthly" \
  --amount 100 \
  --category cost \
  --time-grain monthly \
  --resource-group leadbridge
```

### View Current Costs

```bash
az consumption usage list \
  --resource-group leadbridge \
  --output table
```

## Security Best Practices

1. ✅ **Never commit secrets** - Use GitHub Secrets
2. ✅ **Use HTTPS only** - Already enforced in Function App
3. ✅ **Rotate ACS keys** - Periodically rotate and update in GitHub
4. ✅ **Monitor access** - Enable Application Insights alerts
5. ✅ **Restrict CORS** - Update to specific domain when ready for production

## Next Steps

- [ ] Set up production environment with separate resource group
- [ ] Configure custom domain for Function App
- [ ] Add API authentication (Azure AD or API keys)
- [ ] Set up monitoring alerts in Application Insights
- [ ] Publish Chrome extension to Chrome Web Store
- [ ] Configure Stripe for billing

## Support

If you encounter issues:

1. Check GitHub Actions workflow logs
2. Review Azure Function App logs
3. Check Application Insights for exceptions
4. Open an issue on GitHub repository

## Rollback

If you need to rollback:

```bash
# Delete resource group (nuclear option)
az group delete --name leadbridge --yes

# Re-run deployment workflow in GitHub Actions
```

---

🎉 **Congratulations!** You've successfully deployed LeadBridge AU to Azure.
