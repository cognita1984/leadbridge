// LeadBridge AU - Background Service Worker
// Polls ServiceSeeking for new leads and sends to Azure backend

const CONFIG = {
  POLL_INTERVAL_MINUTES: 1,
  API_ENDPOINT: 'https://leadbridgefunc.azurewebsites.net/api/newlead', // Update after deployment
  SERVICE_SEEKING_API: 'https://api.serviceseeking.com.au/leads', // May need adjustment based on actual API
  STORAGE_KEY_LEADS: 'seenLeadIds',
  STORAGE_KEY_ENABLED: 'monitoringEnabled',
  STORAGE_KEY_TRADIE_PHONE: 'tradiePhone'
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
  try {
    // Option 1: API call (if ServiceSeeking provides an API)
    const response = await fetch(CONFIG.SERVICE_SEEKING_API, {
      credentials: 'include',
      headers: {
        'Accept': 'application/json'
      }
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const data = await response.json();
    return data.leads || [];

  } catch (error) {
    console.error('Error fetching leads:', error);

    // Option 2: Query active tabs for ServiceSeeking pages
    // This is a fallback method using content scripts
    return await fetchLeadsFromContentScript();
  }
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
  // Adjust selectors based on actual ServiceSeeking HTML structure

  const leads = [];
  const leadElements = document.querySelectorAll('.lead-item, [data-lead-id]'); // Adjust selector

  leadElements.forEach(element => {
    try {
      const leadId = element.getAttribute('data-lead-id') ||
                    element.querySelector('[data-lead-id]')?.getAttribute('data-lead-id');

      if (!leadId) return;

      // Extract lead details (adjust selectors as needed)
      const lead = {
        leadId: leadId,
        customerName: element.querySelector('.customer-name')?.textContent?.trim() || 'Unknown',
        customerPhone: element.querySelector('.customer-phone')?.textContent?.trim() || '',
        jobType: element.querySelector('.job-type')?.textContent?.trim() || 'General Service',
        location: element.querySelector('.location')?.textContent?.trim() || '',
        timestamp: new Date().toISOString()
      };

      leads.push(lead);
    } catch (err) {
      console.error('Error extracting lead:', err);
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
});
