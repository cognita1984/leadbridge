// ✅ CONFIRMED SELECTORS FOR SERVICESEEKING
// Based on actual HTML inspection

const SERVICESEEKING_SELECTORS = {
  // ==================================
  // INBOX LIST VIEW (for detecting NEW leads)
  // ==================================
  inbox: {
    // Container for all leads
    container: '#scrollable-matched .matched-leads',

    // Individual lead cards
    leadCard: '[id^="matched-lead-card-"]',  // Matches: id="matched-lead-card-5181166"

    // Extract lead ID from the card's id attribute
    // Example: "matched-lead-card-5181166" → extract "5181166"
    getLeadId: (cardElement) => {
      const id = cardElement.getAttribute('id');
      return id ? id.replace('matched-lead-card-', '') : null;
    },

    // Inside each lead card:
    customerName: '.text-sm:first-of-type',  // First .text-sm contains name

    jobType: '.text-sm.font-semibold:first-of-type',  // Contains "General carpentry in"

    location: {
      link: 'a[href*="google.com/maps"]',  // Google Maps link
      text: 'a[href*="google.com/maps"]',  // Text: "Aspendale, VIC, 3195"
    },

    timeAgo: '.text-xs.text-right span',  // "10 hours ago"

    // Customer initial avatar (background color might indicate status)
    avatarInitial: '.bg-blue-tones-100',

    // Verified badge (SVG checkmark)
    hasVerifiedBadge: (cardElement) => {
      return cardElement.querySelector('svg[width="13"][height="13"]') !== null;
    },
  },

  // ==================================
  // LEAD DETAIL MODAL (when lead is clicked)
  // ==================================
  modal: {
    // Modal container
    container: '#lead-details-modal',

    // Check if modal is visible
    isVisible: (modalElement) => {
      return modalElement && !modalElement.classList.contains('hidden');
    },

    // Lead/Job ID
    leadId: {
      // Text before the ID says "Job ID: "
      selector: '.text-base.mb-5 span.font-semibold',  // Contains "5181166"
    },

    // Customer name
    customerName: '.text-lg.font-normal',  // "Priank"

    // Job title (includes type and location)
    jobTitle: '.text-xl.font-semibold',  // "General carpentry in Aspendale, VIC, 3195, 18kms"

    // Job description
    description: '.text-base.font-normal.mb-5',

    // Budget
    budget: '.flex.items-end.space-x-1\\.5:first-of-type .text-base.font-semibold',

    // Timing
    timing: '.flex.items-end.space-x-1\\.5:nth-of-type(2) .text-base.font-semibold',

    // Location link
    locationLink: 'a[href*="google.com/maps"]',

    // Buttons
    contactButton: 'button.btn-secondary:contains("Contact Customer")',
    discardButton: 'button.btn-outline-secondary:contains("Discard Lead")',

    // Attached images
    attachedImages: 'a[href*="uploads2.serviceseeking.com.au"] img',
  },
};

// ==================================
// HELPER FUNCTIONS
// ==================================

/**
 * Extract clean lead data from inbox card
 */
function extractLeadFromCard(cardElement) {
  const selectors = SERVICESEEKING_SELECTORS.inbox;

  // Lead ID
  const leadId = selectors.getLeadId(cardElement);

  // Customer Name
  const customerNameEl = cardElement.querySelector(selectors.customerName);
  const customerName = customerNameEl ? customerNameEl.textContent.trim() : 'Unknown';

  // Job Type (remove " in" suffix)
  const jobTypeEl = cardElement.querySelector(selectors.jobType);
  let jobType = jobTypeEl ? jobTypeEl.textContent.trim() : '';
  jobType = jobType.replace(/ in$/, ''); // Remove trailing " in"

  // Location
  const locationEl = cardElement.querySelector(selectors.location.link);
  const location = locationEl ? locationEl.textContent.trim() : '';

  // Time ago
  const timeEl = cardElement.querySelector(selectors.timeAgo);
  const timeAgo = timeEl ? timeEl.textContent.trim() : '';

  // Is verified?
  const isVerified = selectors.hasVerifiedBadge(cardElement);

  return {
    leadId,
    customerName,
    jobType,
    location,
    timeAgo,
    isVerified,
    timestamp: new Date().toISOString(),
    // Note: Phone number NOT available in list view
    customerPhone: '', // Will need to click to get this
  };
}

/**
 * Get all visible leads from inbox
 */
function getAllLeadsFromInbox() {
  const selectors = SERVICESEEKING_SELECTORS.inbox;
  const leadCards = document.querySelectorAll(selectors.leadCard);

  const leads = [];
  leadCards.forEach(card => {
    try {
      const leadData = extractLeadFromCard(card);
      if (leadData.leadId) {
        leads.push(leadData);
      }
    } catch (error) {
      console.error('Error extracting lead:', error);
    }
  });

  return leads;
}

/**
 * Monitor for NEW leads appearing in inbox
 */
function monitorForNewLeads(callback) {
  const selectors = SERVICESEEKING_SELECTORS.inbox;
  const container = document.querySelector(selectors.container);

  if (!container) {
    console.warn('ServiceSeeking inbox container not found');
    return null;
  }

  // Track seen lead IDs
  const seenLeadIds = new Set(
    Array.from(document.querySelectorAll(selectors.leadCard))
      .map(card => selectors.getLeadId(card))
      .filter(Boolean)
  );

  // Set up MutationObserver to watch for new lead cards
  const observer = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      mutation.addedNodes.forEach((node) => {
        if (node.nodeType === 1 && node.matches && node.matches(selectors.leadCard)) {
          const leadId = selectors.getLeadId(node);

          if (leadId && !seenLeadIds.has(leadId)) {
            seenLeadIds.add(leadId);

            // Extract lead data and call callback
            const leadData = extractLeadFromCard(node);
            console.log('🆕 NEW LEAD DETECTED:', leadData);
            callback(leadData);
          }
        }
      });
    });
  });

  // Start observing
  observer.observe(container, {
    childList: true,
    subtree: true,
  });

  console.log('✅ Lead monitoring started');

  return observer;
}

// ==================================
// EXPORT FOR CHROME EXTENSION
// ==================================
if (typeof module !== 'undefined' && module.exports) {
  module.exports = {
    SERVICESEEKING_SELECTORS,
    extractLeadFromCard,
    getAllLeadsFromInbox,
    monitorForNewLeads,
  };
}

// ==================================
// TEST IN CONSOLE
// ==================================
// Copy and paste this entire file into Chrome DevTools console
// Then run:
//
// // Get all current leads
// console.log('All leads:', getAllLeadsFromInbox());
//
// // Monitor for new leads
// monitorForNewLeads((newLead) => {
//   console.log('NEW LEAD CALLBACK:', newLead);
//   // Here you would send to backend
// });
