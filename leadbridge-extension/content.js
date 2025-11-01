// LeadBridge AU - Content Script
// Monitors ServiceSeeking inbox for new leads in real-time using MutationObserver

const SELECTORS = {
  container: '#scrollable-matched .matched-leads',
  leadCard: '[id^="matched-lead-card-"]',
  customerName: '.text-sm:first-of-type',
  jobType: '.text-sm.font-semibold:first-of-type',
  location: 'a[href*="google.com/maps"]',
  timeAgo: '.text-xs.text-right span',
  verifiedBadge: 'svg[width="13"][height="13"]',

  // Modal selectors for detailed lead info
  modal: {
    container: '#lead-details-modal',
    customerName: '.text-lg.font-normal',
    jobTitle: '.text-xl.font-semibold',
    description: '.text-base.font-normal.mb-5',
    budget: '.flex.items-end.space-x-1\\.5:first-of-type .text-base.font-semibold',
    timing: '.flex.items-end.space-x-1\\.5:nth-of-type(2) .text-base.font-semibold',
    locationLink: 'a[href*="google.com/maps"]'
  }
};

let observer = null;
let seenLeadIds = new Set();

// Initialize content script
console.log('LeadBridge AU content script loaded');

// Extract lead data from a card element
function extractLeadFromCard(card) {
  try {
    // Extract lead ID from card's id attribute
    const cardId = card.getAttribute('id');
    const leadId = cardId ? cardId.replace('matched-lead-card-', '') : null;

    if (!leadId) return null;

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

    return {
      leadId: leadId,
      customerName: customerName,
      customerPhone: '', // ⚠️ Phone NOT visible without clicking "Contact Customer"
      jobType: jobType,
      location: location,
      timeAgo: timeAgo,
      isVerified: isVerified,
      timestamp: new Date().toISOString()
    };
  } catch (err) {
    console.error('Error extracting lead from card:', err);
    return null;
  }
}

// Get all currently visible leads
function getAllVisibleLeads() {
  const leadCards = document.querySelectorAll(SELECTORS.leadCard);
  const leads = [];

  leadCards.forEach(card => {
    const lead = extractLeadFromCard(card);
    if (lead && lead.leadId) {
      leads.push(lead);
      seenLeadIds.add(lead.leadId);
    }
  });

  return leads;
}

// Auto-click first lead to get full details
async function autoClickFirstLead() {
  try {
    const leadCards = document.querySelectorAll(SELECTORS.leadCard);

    if (leadCards.length === 0) {
      console.log('No leads to click');
      return null;
    }

    // Click the FIRST lead
    const firstLead = leadCards[0];
    const leadId = firstLead.getAttribute('id').replace('matched-lead-card-', '');

    console.log(`🖱️ Auto-clicking first lead: ${leadId}`);
    firstLead.click();

    // Wait for modal to appear
    const modalData = await waitForModalAndExtractData();

    if (modalData) {
      console.log('✅ Modal data extracted:', modalData);
      return modalData;
    }

    return null;
  } catch (error) {
    console.error('Error auto-clicking lead:', error);
    return null;
  }
}

// Wait for modal to appear and extract full lead details
function waitForModalAndExtractData() {
  return new Promise((resolve) => {
    let attempts = 0;
    const maxAttempts = 20; // 2 seconds max wait

    const checkModal = setInterval(() => {
      attempts++;

      const modal = document.querySelector(SELECTORS.modal.container);

      if (modal && !modal.classList.contains('hidden')) {
        clearInterval(checkModal);

        // Extract full details from modal
        const leadData = extractDetailsFromModal(modal);
        resolve(leadData);
      }

      if (attempts >= maxAttempts) {
        clearInterval(checkModal);
        console.warn('Modal did not appear within timeout');
        resolve(null);
      }
    }, 100); // Check every 100ms
  });
}

// Extract full lead details from modal
function extractDetailsFromModal(modal) {
  try {
    // Customer name
    const customerNameEl = modal.querySelector(SELECTORS.modal.customerName);
    const customerName = customerNameEl ? customerNameEl.textContent.trim() : 'Unknown';

    // Job title (includes type and location)
    const jobTitleEl = modal.querySelector(SELECTORS.modal.jobTitle);
    const jobTitle = jobTitleEl ? jobTitleEl.textContent.trim() : '';

    // Parse job type and location from title
    // Example: "General carpentry in Aspendale, VIC, 3195, 18kms"
    let jobType = 'General Service';
    let location = '';

    if (jobTitle.includes(' in ')) {
      const parts = jobTitle.split(' in ');
      jobType = parts[0].trim();
      location = parts[1].trim();
    }

    // Description
    const descriptionEl = modal.querySelector(SELECTORS.modal.description);
    const description = descriptionEl ? descriptionEl.textContent.trim() : '';

    // Budget
    const budgetEl = modal.querySelector(SELECTORS.modal.budget);
    const budget = budgetEl ? budgetEl.textContent.trim() : 'Not specified';

    // Timing
    const timingEl = modal.querySelector(SELECTORS.modal.timing);
    const timing = timingEl ? timingEl.textContent.trim() : 'Not specified';

    // Lead ID from URL or modal
    const leadIdMatch = window.location.href.match(/lead[/-](\d+)/);
    const leadId = leadIdMatch ? leadIdMatch[1] : Date.now().toString();

    return {
      leadId,
      customerName,
      jobType,
      location,
      description,
      budget,
      timing,
      timestamp: new Date().toISOString()
    };
  } catch (error) {
    console.error('Error extracting modal data:', error);
    return null;
  }
}

// Handle new lead detected
async function handleNewLead(lead) {
  console.log('🆕 NEW LEAD DETECTED:', lead);

  // Auto-click first lead to get full details
  console.log('⏳ Auto-clicking first lead to extract full details...');
  const fullDetails = await autoClickFirstLead();

  if (fullDetails) {
    console.log('✅ Full lead details extracted from modal');

    // Merge basic lead data with full details
    const completeLead = {
      ...lead,
      ...fullDetails
    };

    // Send to background script
    chrome.runtime.sendMessage({
      action: 'newLeadDetected',
      lead: completeLead
    }).then(response => {
      console.log('Lead sent to background:', response);
    }).catch(error => {
      console.error('Error sending lead to background:', error);
    });
  } else {
    console.error('❌ Failed to extract full lead details');

    // Still send basic lead info
    chrome.runtime.sendMessage({
      action: 'newLeadDetected',
      lead: lead
    }).then(response => {
      console.log('Lead sent to background (basic info only):', response);
    }).catch(error => {
      console.error('Error sending lead to background:', error);
    });
  }
}

// Start monitoring for new leads
function startMonitoring() {
  const container = document.querySelector(SELECTORS.container);

  if (!container) {
    console.warn('ServiceSeeking inbox container not found. Will retry...');
    // Retry after 2 seconds in case page is still loading
    setTimeout(startMonitoring, 2000);
    return;
  }

  console.log('✅ Found inbox container, starting lead monitoring');

  // Get all existing leads to mark as seen
  const existingLeads = getAllVisibleLeads();
  console.log(`Marked ${existingLeads.length} existing lead(s) as seen`);

  // Create MutationObserver to watch for new lead cards
  observer = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      mutation.addedNodes.forEach((node) => {
        // Check if added node is a lead card
        if (node.nodeType === 1 && node.matches && node.matches(SELECTORS.leadCard)) {
          const lead = extractLeadFromCard(node);

          if (lead && lead.leadId && !seenLeadIds.has(lead.leadId)) {
            seenLeadIds.add(lead.leadId);
            handleNewLead(lead);
          }
        }
      });
    });
  });

  // Start observing
  observer.observe(container, {
    childList: true,
    subtree: true
  });

  console.log('✅ MutationObserver active - monitoring for new leads');
}

// Stop monitoring
function stopMonitoring() {
  if (observer) {
    observer.disconnect();
    observer = null;
    console.log('❌ Lead monitoring stopped');
  }
}

// Listen for messages from background script
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.action === 'getLeads') {
    const leads = getAllVisibleLeads();
    sendResponse({ leads: leads });
    return true;
  }

  if (message.action === 'startMonitoring') {
    startMonitoring();
    sendResponse({ success: true });
    return true;
  }

  if (message.action === 'stopMonitoring') {
    stopMonitoring();
    sendResponse({ success: true });
    return true;
  }
});

// Auto-start monitoring when page loads
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', startMonitoring);
} else {
  startMonitoring();
}
