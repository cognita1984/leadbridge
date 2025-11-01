# LeadBridge AU - Chrome Extension

Automatically detect ServiceSeeking leads and trigger real-time phone bridging.

## Installation

### Development Mode

1. Open Chrome and navigate to `chrome://extensions/`
2. Enable "Developer mode" (toggle in top right)
3. Click "Load unpacked"
4. Select the `leadbridge-extension` folder

### Configuration

1. Click the LeadBridge AU extension icon
2. Enter your tradie phone number (Australian format: +61 4XX XXX XXX)
3. Toggle "Enable Monitoring" to ON
4. Click "Save Settings"

## How It Works

1. **Polling**: Extension checks ServiceSeeking every 60 seconds for new leads
2. **Detection**: When a new lead is found, it's sent to the Azure backend
3. **Bridging**: Backend calls your phone, then bridges to the customer
4. **Logging**: All activity is logged for analytics

## Features

- ✅ Real-time lead detection
- ✅ Automatic polling every minute
- ✅ Phone number validation
- ✅ Status indicator
- ✅ Lead counter
- ✅ Test connection feature

## File Structure

```
leadbridge-extension/
├── manifest.json          # Extension configuration
├── background.js          # Service worker (polling logic)
├── popup.html            # Extension popup UI
├── popup.js              # Popup controller
├── icons/                # Extension icons (16, 48, 128px)
└── README.md             # This file
```

## Configuration

Update the API endpoint in `background.js`:

```javascript
const CONFIG = {
  API_ENDPOINT: 'https://YOUR-FUNCTION-APP.azurewebsites.net/api/newlead'
};
```

## Lead Detection Methods

The extension uses two methods:

1. **API Polling** (preferred): Fetches from ServiceSeeking API
2. **Content Script** (fallback): Extracts leads from open ServiceSeeking tabs

You may need to adjust CSS selectors in `extractLeadsFromPage()` based on ServiceSeeking's HTML structure.

## Permissions

- `storage`: Store seen lead IDs and settings
- `alarms`: Schedule periodic polling
- `scripting`: Extract lead data from pages
- `activeTab`: Access ServiceSeeking tabs
- `cookies`: Authenticate with ServiceSeeking
- `host_permissions`: Access ServiceSeeking.com.au

## Testing

1. Log into ServiceSeeking in Chrome
2. Enable monitoring in the extension
3. Click "Test Connection" to manually trigger a poll
4. Check browser console (F12) for logs

## Notes

- ServiceSeeking may not have a public API; you'll likely need to use content scripts to scrape lead data
- Adjust selectors in `extractLeadsFromPage()` to match actual HTML structure
- The extension requires an active ServiceSeeking session (logged in)

## Support

For issues or questions, check the main project README.
