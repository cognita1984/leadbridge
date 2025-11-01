# 🧪 LeadBridge AU - Local Testing Guide

This guide will help you test the Chrome extension locally before deploying to Azure.

---

## Prerequisites

- Google Chrome browser
- ServiceSeeking account with access to the inbox
- Active leads in your ServiceSeeking inbox (for testing detection)

---

## Step 1: Load the Extension in Chrome

### 1.1 Open Chrome Extensions Page

1. Open Google Chrome
2. Navigate to `chrome://extensions/`
3. Enable **Developer mode** (toggle in top-right corner)

### 1.2 Load Unpacked Extension

1. Click **"Load unpacked"** button
2. Navigate to your project folder: `C:\leadbridge\leadbridge-extension\`
3. Select the `leadbridge-extension` folder
4. Click **"Select Folder"**

### 1.3 Verify Installation

You should see:
- **LeadBridge AU** extension card
- Extension ID (random string)
- Status: **Enabled**
- ⚠️ Warning about icons (expected - icons not created yet)

---

## Step 2: Configure Extension Settings

### 2.1 Open Extension Popup

1. Click the **Extensions icon** (puzzle piece) in Chrome toolbar
2. Click **LeadBridge AU**
3. The purple gradient popup should appear

### 2.2 Enter Settings

1. **Tradie Phone Number:**
   - Enter your phone number in international format
   - Example: `+61412345678`
   - This is where ACS will call when a new lead is detected

2. **Enable Monitoring:**
   - Toggle the switch to **ON** (green)
   - Badge should show "ON" on the extension icon

3. **Click "Save Settings"**
   - You should see "Settings saved!" message

---

## Step 3: Test Lead Detection

### 3.1 Open ServiceSeeking Inbox

1. In the same Chrome window (with extension loaded):
2. Navigate to `https://www.serviceseeking.com.au/`
3. Log in to your account
4. Go to your **Inbox** or **Matched Leads** page
5. Ensure you have at least 1 lead visible

### 3.2 Monitor Console Logs

1. Press **F12** to open Chrome DevTools
2. Go to the **Console** tab
3. Look for these messages:
   ```
   LeadBridge AU content script loaded
   ✅ Found inbox container, starting lead monitoring
   Marked X existing lead(s) as seen
   ✅ MutationObserver active - monitoring for new leads
   ```

If you see these messages, the content script is working! ✅

---

## Step 4: Test Real-Time Lead Detection

Since we're testing locally and new leads might not arrive immediately, we'll simulate detection:

### 4.1 Test Existing Lead Extraction

In the DevTools Console, run:

```javascript
// Get all current leads
chrome.runtime.sendMessage({ action: 'testPoll' }, (response) => {
  console.log('Test poll result:', response);
});
```

Expected output:
- Background script attempts to fetch leads from ServiceSeeking tab
- Extracts lead data using our selectors
- Logs lead information

### 4.2 Manual Lead Data Extraction

In the DevTools Console, run this to see what data is being extracted:

```javascript
// Extract all visible leads
const leadCards = document.querySelectorAll('[id^="matched-lead-card-"]');
console.log(`Found ${leadCards.length} lead card(s)`);

leadCards.forEach((card, index) => {
  const leadId = card.getAttribute('id').replace('matched-lead-card-', '');
  const customerName = card.querySelector('.text-sm:first-of-type')?.textContent?.trim();
  const jobType = card.querySelector('.text-sm.font-semibold:first-of-type')?.textContent?.trim();
  const location = card.querySelector('a[href*="google.com/maps"]')?.textContent?.trim();

  console.log(`Lead ${index + 1}:`, {
    leadId,
    customerName,
    jobType,
    location
  });
});
```

Expected output:
- List of all visible leads with extracted data
- Verify data accuracy by comparing with what you see on screen

---

## Step 5: Test Backend Integration (Optional)

⚠️ **Note:** This requires the Azure backend to be deployed first.

### 5.1 Update API Endpoint

If you've deployed the backend, update `background.js` line 6:

```javascript
API_ENDPOINT: 'https://YOUR-FUNCTION-APP.azurewebsites.net/api/newlead'
```

Replace `YOUR-FUNCTION-APP` with your actual Azure Function App name.

### 5.2 Reload Extension

1. Go to `chrome://extensions/`
2. Click **Reload** button on LeadBridge AU card

### 5.3 Trigger Test Poll

1. Open extension popup
2. Click **"Test Poll Now"** button (if available)
3. Check DevTools Console for backend response logs

---

## Step 6: Monitor for New Leads

### 6.1 Real-Time Detection Test

If you have access to create test leads or wait for new leads:

1. Keep Chrome DevTools Console open
2. Keep ServiceSeeking inbox tab active
3. When a new lead appears, you should see:
   ```
   🆕 NEW LEAD DETECTED: {leadId: "...", customerName: "...", ...}
   Lead sent to background: {success: true}
   🆕 Processing new lead from content script: ...
   ```

4. Extension badge should briefly show "NEW" (green background)
5. Browser notification should appear with lead details

### 6.2 Background Script Logs

To see background script logs:

1. Go to `chrome://extensions/`
2. Find **LeadBridge AU**
3. Click **"service worker"** link (under "Inspect views")
4. A new DevTools window opens showing background script logs

---

## Troubleshooting

### Extension Not Loading

**Problem:** Extension fails to load or shows errors

**Solutions:**
1. Ensure all files are in `leadbridge-extension/` folder
2. Check manifest.json syntax (must be valid JSON)
3. Refresh extension: `chrome://extensions/` → Reload button

---

### No Console Messages

**Problem:** No logs appear when visiting ServiceSeeking

**Solutions:**
1. Verify you're on `*.serviceseeking.com.au` domain
2. Check content script is loaded: DevTools → Sources → Content scripts
3. Reload the page (Ctrl+R / Cmd+R)

---

### Leads Not Detected

**Problem:** Leads visible but not being detected

**Solutions:**
1. Check if inbox container exists:
   ```javascript
   document.querySelector('#scrollable-matched .matched-leads')
   ```
   Should return an element, not `null`

2. Check if lead cards exist:
   ```javascript
   document.querySelectorAll('[id^="matched-lead-card-"]').length
   ```
   Should return number of visible leads

3. Verify monitoring is enabled:
   - Open extension popup
   - Check toggle is ON (green)

---

### Backend Connection Failed

**Problem:** Leads detected but not sent to backend

**Solutions:**
1. Check backend endpoint URL in `background.js`
2. Verify backend is deployed and running
3. Check CORS is enabled on backend
4. Check Network tab in DevTools for failed requests

---

## Expected Behavior Summary

✅ **When extension loads:**
- Extension icon appears in toolbar
- Popup opens with settings UI
- No errors in console

✅ **When visiting ServiceSeeking inbox:**
- Content script logs appear in console
- MutationObserver starts monitoring
- Existing leads are marked as seen

✅ **When new lead appears:**
- Console logs "🆕 NEW LEAD DETECTED"
- Badge shows "NEW" briefly
- Browser notification appears
- Lead sent to background script
- Background script sends to backend (if configured)

---

## Next Steps After Testing

Once local testing is successful:

1. ✅ **Verify Selectors Work** - Leads are correctly extracted
2. ⏳ **Deploy Backend** - Follow `DEPLOYMENT.md`
3. ⏳ **Update Extension** - Set correct API endpoint
4. ⏳ **Configure ACS** - Set up phone number and callback URL
5. ⏳ **Test End-to-End** - New lead → Call tradie → Bridge to customer

---

## Test Checklist

- [ ] Extension loads without errors
- [ ] Popup UI displays correctly
- [ ] Settings are saved and persisted
- [ ] Content script loads on ServiceSeeking pages
- [ ] Existing leads are detected and counted
- [ ] Lead data is accurately extracted
- [ ] MutationObserver is active and monitoring
- [ ] New leads trigger detection (if testable)
- [ ] Browser notifications appear (if enabled)
- [ ] Backend receives lead data (if deployed)

---

## Known Limitations (Phase 1)

⚠️ **Customer Phone Numbers:**
- NOT available without clicking "Contact Customer" button
- Extension sends leads WITHOUT phone numbers
- Decision needed on automation strategy (see `FINDINGS.md`)

⚠️ **Extension Icons:**
- Placeholder icons (browser may show warnings)
- Create actual 16px, 48px, 128px PNG icons

⚠️ **Backend Not Deployed:**
- Leads will be detected but API calls will fail
- Deploy backend to test full flow

---

**Last Updated:** 2025-11-02
**Status:** Ready for local testing
**Version:** 1.0.0
