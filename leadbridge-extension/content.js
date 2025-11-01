// LeadBridge AU - Content Script
// Monitors ServiceSeeking inbox for new leads in real-time using MutationObserver

const SELECTORS = {
  container: '#scrollable-matched .matched-leads',
  leadCard: '[id^="matched-lead-card-"]',
  customerName: '.text-sm:first-of-type',
  jobType: '.text-sm.font-semibold:first-of-type',
  location: 'a[href*="google.com/maps"]',
  timeAgo: '.text-xs.text-right span',
  verifiedBadge: 'svg[width="13"][height="13"]'
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

// Handle new lead detected
function handleNewLead(lead) {
  console.log('🆕 NEW LEAD DETECTED:', lead);

  // Send to background script
  chrome.runtime.sendMessage({
    action: 'newLeadDetected',
    lead: lead
  }).then(response => {
    console.log('Lead sent to background:', response);
  }).catch(error => {
    console.error('Error sending lead to background:', error);
  });
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
