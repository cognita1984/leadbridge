// ServiceSeeking Lead Selectors
// Fill this in as you inspect the page

const SELECTORS = {
  // Main lead container (the parent element of each lead)
  leadContainer: '.FILL_THIS_IN',  // e.g., '.lead-item', '[data-lead-id]', '.inbox-row'

  // Lead ID (the unique identifier for this lead)
  leadId: {
    selector: '.FILL_THIS_IN',     // e.g., '.lead-id', '.job-id'
    attribute: null,                // If ID is in attribute: 'data-lead-id', 'data-id', etc.
  },

  // Customer information
  customerName: '.FILL_THIS_IN',    // e.g., '.customer-name', '.requester-name'
  customerPhone: '.FILL_THIS_IN',   // e.g., '.phone-number', '.contact-phone' (if visible)

  // Job details
  jobType: '.FILL_THIS_IN',         // e.g., '.job-type', '.service-category', '.job-title'
  location: '.FILL_THIS_IN',        // e.g., '.location', '.suburb', '.postcode'

  // Optional fields (fill if available)
  jobDescription: null,             // e.g., '.description', '.job-details'
  jobDate: null,                    // e.g., '.date', '.timestamp'
  budget: null,                     // e.g., '.budget', '.price'
};

// Example extraction function (test in console)
function extractLeadData(leadElement) {
  const data = {};

  // Lead ID
  if (SELECTORS.leadId.attribute) {
    data.leadId = leadElement.getAttribute(SELECTORS.leadId.attribute);
  } else {
    data.leadId = leadElement.querySelector(SELECTORS.leadId.selector)?.textContent?.trim();
  }

  // Customer Name
  data.customerName = leadElement.querySelector(SELECTORS.customerName)?.textContent?.trim() || 'Unknown';

  // Customer Phone
  data.customerPhone = leadElement.querySelector(SELECTORS.customerPhone)?.textContent?.trim() || '';

  // Job Type
  data.jobType = leadElement.querySelector(SELECTORS.jobType)?.textContent?.trim() || 'General Service';

  // Location
  data.location = leadElement.querySelector(SELECTORS.location)?.textContent?.trim() || '';

  return data;
}

// Test in console:
// 1. Copy this entire file
// 2. Paste in Chrome DevTools Console (F12)
// 3. Update the SELECTORS object with your findings
// 4. Run: extractLeadData(document.querySelector(SELECTORS.leadContainer))
// 5. Check if data is extracted correctly
