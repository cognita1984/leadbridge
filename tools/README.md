# LeadBridge Testing Tools

Tools for studying and testing ServiceSeeking integration.

## Setup

```bash
cd tools
npm install
```

This will install Playwright and its dependencies.

## ServiceSeeking Study Tool

This tool opens a Chromium browser and monitors ServiceSeeking to help you understand:
- Network requests (API endpoints)
- HTML structure (for lead detection)
- JavaScript events

### Usage

```bash
npm run study-serviceseeking
```

### What it does

1. Opens Chromium browser with DevTools
2. Navigates to ServiceSeeking.com.au
3. Logs all network requests to/from ServiceSeeking
4. Periodically captures page HTML structure
5. Attempts to detect lead elements on the page
6. Saves all data to `./output/` directory

### Steps

1. Run the script
2. Login to ServiceSeeking in the opened browser
3. Navigate to your leads/jobs page
4. Let it run for a few minutes while you interact with the page
5. Press Ctrl+C to stop and save logs

### Output Files

All files are saved in `./tools/output/`:

- `network-log-TIMESTAMP.json` - All API requests/responses
- `page-snapshot-TIMESTAMP.html` - HTML snapshot of the page
- `lead-analysis-TIMESTAMP.json` - Detected lead elements

### Analyzing Results

After running the tool, review the output files to:

1. **Find API endpoints**: Look in `network-log-*.json` for API calls
   - Check if ServiceSeeking exposes a `/api/leads` endpoint
   - Note the request/response format

2. **Find HTML patterns**: Look in `page-snapshot-*.html`
   - Search for lead/job data in the HTML
   - Identify CSS selectors for lead elements

3. **Update extension**: Use findings to update:
   - `leadbridge-extension/background.js` - Update API endpoint or selectors
   - `extractLeadsFromPage()` function - Update CSS selectors

### Example Analysis

After studying ServiceSeeking, you might find:

```javascript
// Update in background.js
function extractLeadsFromPage() {
  const leadElements = document.querySelectorAll('.actual-lead-class'); // Update this

  leadElements.forEach(element => {
    const leadId = element.getAttribute('data-actual-lead-id'); // Update this
    const customerName = element.querySelector('.actual-customer-name')?.textContent; // Update this
    // ... etc
  });
}
```

## Tips

- Use Chrome DevTools (F12) to inspect elements
- Look for `data-*` attributes on lead elements
- Check Network tab for XHR/Fetch requests
- ServiceSeeking might load leads dynamically via AJAX

## Troubleshooting

**Error: Playwright not installed**
```bash
npx playwright install chromium
```

**Browser doesn't open**
- Check if port 9222 is available
- Try running with `headless: false` (already set)

**No network logs captured**
- Make sure you're logged into ServiceSeeking
- Navigate to the actual leads page
- The script only logs ServiceSeeking-related requests
