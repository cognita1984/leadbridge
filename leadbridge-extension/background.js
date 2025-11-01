// LeadBridge AU - Background Service Worker
// Polls ServiceSeeking for new leads and sends to Azure backend

const CONFIG = {
  POLL_INTERVAL_MINUTES: 1,
  API_ENDPOINT: 'https://leadbridgefunc.azurewebsites.net/api/newlead', // Update after deployment
  STORAGE_KEY_LEADS: 'seenLeadIds',
  STORAGE_KEY_ENABLED: 'monitoringEnabled',
  STORAGE_KEY_TRADIE_PHONE: 'tradiePhone'
};

// ✅ CONFIRMED SELECTORS FOR SERVICESEEKING
// Based on actual HTML inspection - see tools/FOUND_SELECTORS.js
const SERVICESEEKING_SELECTORS = {
  inbox: {
    container: '#scrollable-matched .matched-leads',
    leadCard: '[id^="matched-lead-card-"]',  // Matches: id="matched-lead-card-5181166"
    customerName: '.text-sm:first-of-type',
    jobType: '.text-sm.font-semibold:first-of-type',
    location: 'a[href*="google.com/maps"]',
    timeAgo: '.text-xs.text-right span',
    verifiedBadge: 'svg[width="13"][height="13"]'
  }
};

// Initialize extension
chrome.runtime.onInstalled.addListener(() => {
  console.log('LeadBridge AU installed');

  // Set default values
  chrome.storage.local.set({
    [CONFIG.STORAGE_KEY_LEADS]: [],
    [CONFIG.STORAGE_KEY_ENABLED]: false,
    [CONFIG.STORAGE_KEY_TRADIE_PHONE]: ''
  });

  // Create polling alarm
  chrome.alarms.create('pollLeads', {
    periodInMinutes: CONFIG.POLL_INTERVAL_MINUTES
  });
});

// Listen for alarm to poll leads
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'pollLeads') {
    checkForNewLeads();
  }
});

// Main function to check for new leads
async function checkForNewLeads() {
  try {
    // Check if monitoring is enabled
    const { monitoringEnabled } = await chrome.storage.local.get(CONFIG.STORAGE_KEY_ENABLED);

    if (!monitoringEnabled) {
      console.log('Monitoring is disabled');
      return;
    }

    console.log('Polling for new leads...');

    // Fetch leads from ServiceSeeking
    // Note: This assumes ServiceSeeking has an API endpoint. In reality, you may need to
    // scrape the webpage or use content scripts to detect new leads
    const leads = await fetchLeads();

    if (leads && leads.length > 0) {
      await processNewLeads(leads);
    }

  } catch (error) {
    console.error('Error checking for new leads:', error);
    updateBadge('!', '#ff0000');
  }
}

// Fetch leads from ServiceSeeking
async function fetchLeads() {
  // ServiceSeeking does NOT have a public API
  // Must scrape leads from DOM using content scripts
  return await fetchLeadsFromContentScript();
}

// Alternative: Fetch leads by querying content script on ServiceSeeking tabs
async function fetchLeadsFromContentScript() {
  try {
    const tabs = await chrome.tabs.query({
      url: 'https://*.serviceseeking.com.au/*'
    });

    if (tabs.length === 0) {
      console.log('No ServiceSeeking tabs open');
      return [];
    }

    // Execute content script to extract lead data
    const results = await chrome.scripting.executeScript({
      target: { tabId: tabs[0].id },
      func: extractLeadsFromPage
    });

    return results[0]?.result || [];

  } catch (error) {
    console.error('Error fetching leads from content script:', error);
    return [];
  }
}

// Function injected into ServiceSeeking page to extract lead data
function extractLeadsFromPage() {
  // This function runs in the context of the ServiceSeeking page
  // Using confirmed selectors from tools/FOUND_SELECTORS.js

  const SELECTORS = {
    leadCard: '[id^="matched-lead-card-"]',
    customerName: '.text-sm:first-of-type',
    jobType: '.text-sm.font-semibold:first-of-type',
    location: 'a[href*="google.com/maps"]',
    timeAgo: '.text-xs.text-right span',
    verifiedBadge: 'svg[width="13"][height="13"]'
  };

  const leads = [];
  const leadCards = document.querySelectorAll(SELECTORS.leadCard);

  leadCards.forEach(card => {
    try {
      // Extract lead ID from card's id attribute: "matched-lead-card-5181166" -> "5181166"
      const cardId = card.getAttribute('id');
      const leadId = cardId ? cardId.replace('matched-lead-card-', '') : null;

      if (!leadId) return;

      // Customer Name
      const customerNameEl = card.querySelector(SELECTORS.customerName);
      const customerName = customerNameEl ? customerNameEl.textContent.trim() : 'Unknown';

      // Job Type (remove trailing " in" if present)
      const jobTypeEl = card.querySelector(SELECTORS.jobType);
      let jobType = jobTypeEl ? jobTypeEl.textContent.trim() : 'General Service';
      jobType = jobType.replace(/ in$/, '');

      // Location
      const locationEl = card.querySelector(SELECTORS.location);
      const location = locationEl ? locationEl.textContent.trim() : '';

      // Time ago
      const timeEl = card.querySelector(SELECTORS.timeAgo);
      const timeAgo = timeEl ? timeEl.textContent.trim() : '';

      // Is verified?
      const isVerified = card.querySelector(SELECTORS.verifiedBadge) !== null;

      const lead = {
        leadId: leadId,
        customerName: customerName,
        customerPhone: '', // ⚠️ Phone NOT visible without clicking "Contact Customer"
        jobType: jobType,
        location: location,
        timeAgo: timeAgo,
        isVerified: isVerified,
        timestamp: new Date().toISOString()
      };

      leads.push(lead);
    } catch (err) {
      console.error('Error extracting lead from card:', err);
    }
  });

  return leads;
}

// Process new leads
async function processNewLeads(leads) {
  const { seenLeadIds, tradiePhone } = await chrome.storage.local.get([
    CONFIG.STORAGE_KEY_LEADS,
    CONFIG.STORAGE_KEY_TRADIE_PHONE
  ]);

  const seenIds = seenLeadIds || [];
  const newLeads = leads.filter(lead => !seenIds.includes(lead.leadId));

  if (newLeads.length === 0) {
    console.log('No new leads found');
    return;
  }

  console.log(`Found ${newLeads.length} new lead(s)`);
  updateBadge(newLeads.length.toString(), '#00ff00');

  // Send each new lead to backend
  for (const lead of newLeads) {
    await sendLeadToBackend(lead, tradiePhone);
    seenIds.push(lead.leadId);
  }

  // Update seen leads
  await chrome.storage.local.set({
    [CONFIG.STORAGE_KEY_LEADS]: seenIds.slice(-100) // Keep last 100 to avoid bloat
  });
}

// Send lead to Azure backend
async function sendLeadToBackend(lead, tradiePhone) {
  try {
    console.log('Sending lead to backend:', lead.leadId);

    const payload = {
      leadId: lead.leadId,
      customerName: lead.customerName,
      customerPhone: lead.customerPhone,
      jobType: lead.jobType,
      location: lead.location,
      tradiePhone: tradiePhone, // Configured in popup
      timestamp: lead.timestamp || new Date().toISOString()
    };

    const response = await fetch(CONFIG.API_ENDPOINT, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      throw new Error(`Backend responded with ${response.status}`);
    }

    const result = await response.json();
    console.log('Backend response:', result);

    // Show notification
    chrome.notifications.create({
      type: 'basic',
      iconUrl: 'icons/icon48.png',
      title: 'LeadBridge AU',
      message: `New lead detected: ${lead.jobType} in ${lead.location}. Calling tradie...`
    });

  } catch (error) {
    console.error('Error sending lead to backend:', error);
    throw error;
  }
}

// Update extension badge
function updateBadge(text, color) {
  chrome.action.setBadgeText({ text });
  chrome.action.setBadgeBackgroundColor({ color });
}

// Message handler for popup communication
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.action === 'getStatus') {
    chrome.storage.local.get([
      CONFIG.STORAGE_KEY_ENABLED,
      CONFIG.STORAGE_KEY_TRADIE_PHONE,
      CONFIG.STORAGE_KEY_LEADS
    ]).then(data => {
      sendResponse({
        enabled: data[CONFIG.STORAGE_KEY_ENABLED] || false,
        tradiePhone: data[CONFIG.STORAGE_KEY_TRADIE_PHONE] || '',
        leadCount: (data[CONFIG.STORAGE_KEY_LEADS] || []).length
      });
    });
    return true; // Keep channel open for async response
  }

  if (message.action === 'toggleMonitoring') {
    chrome.storage.local.set({
      [CONFIG.STORAGE_KEY_ENABLED]: message.enabled
    }).then(() => {
      console.log(`Monitoring ${message.enabled ? 'enabled' : 'disabled'}`);
      updateBadge(message.enabled ? 'ON' : 'OFF', message.enabled ? '#00ff00' : '#999999');
      sendResponse({ success: true });
    });
    return true;
  }

  if (message.action === 'setTradiePhone') {
    chrome.storage.local.set({
      [CONFIG.STORAGE_KEY_TRADIE_PHONE]: message.phone
    }).then(() => {
      console.log('Tradie phone updated:', message.phone);
      sendResponse({ success: true });
    });
    return true;
  }

  if (message.action === 'testPoll') {
    checkForNewLeads().then(() => {
      sendResponse({ success: true });
    }).catch(error => {
      sendResponse({ success: false, error: error.message });
    });
    return true;
  }

  if (message.action === 'newLeadDetected') {
    // Handle new lead detected by content script
    handleNewLeadFromContentScript(message.lead).then(() => {
      sendResponse({ success: true });
    }).catch(error => {
      console.error('Error handling new lead:', error);
      sendResponse({ success: false, error: error.message });
    });
    return true;
  }
});

// Handle new lead detected by content script
async function handleNewLeadFromContentScript(lead) {
  try {
    // Check if monitoring is enabled
    const { monitoringEnabled, seenLeadIds, tradiePhone } = await chrome.storage.local.get([
      CONFIG.STORAGE_KEY_ENABLED,
      CONFIG.STORAGE_KEY_LEADS,
      CONFIG.STORAGE_KEY_TRADIE_PHONE
    ]);

    if (!monitoringEnabled) {
      console.log('Monitoring disabled - ignoring new lead');
      return;
    }

    const seenIds = seenLeadIds || [];

    // Check if already seen
    if (seenIds.includes(lead.leadId)) {
      console.log('Lead already seen:', lead.leadId);
      return;
    }

    console.log('🆕 Processing new lead from content script:', lead.leadId);

    // Send to backend
    await sendLeadToBackend(lead, tradiePhone);

    // Mark as seen
    seenIds.push(lead.leadId);
    await chrome.storage.local.set({
      [CONFIG.STORAGE_KEY_LEADS]: seenIds.slice(-100) // Keep last 100
    });

    // Update badge
    updateBadge('NEW', '#00ff00');
    setTimeout(() => updateBadge('', ''), 5000); // Clear after 5 seconds

  } catch (error) {
    console.error('Error handling new lead from content script:', error);
    updateBadge('!', '#ff0000');
  }
}
