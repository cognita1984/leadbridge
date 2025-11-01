# 🔍 ServiceSeeking Lead Selector Guide

Follow these steps to find the CSS selectors for lead detection.

## Step 1: Open Chrome DevTools

1. **Go to your ServiceSeeking inbox** (with leads visible)
2. **Press F12** (or right-click → Inspect)
3. **Click the "Select Element" tool** (top-left corner of DevTools, looks like a cursor arrow in a box)
   - OR press `Ctrl+Shift+C` (Windows) / `Cmd+Shift+C` (Mac)

## Step 2: Inspect a Lead Element

1. **Click on a lead in your inbox** (while the element selector is active)
2. DevTools will highlight the HTML element in the Elements tab
3. **Look for the PARENT container** that wraps the entire lead
   - It might be a `<div>`, `<li>`, `<article>`, or `<tr>` element
   - Look for a unique class name or data attribute

## Step 3: Find Identifying Attributes

Look for these patterns in the highlighted element:

### A. Lead Container
```html
<!-- Example patterns to look for: -->
<div class="lead-item" data-lead-id="12345">...</div>
<div class="job-card" data-job-id="12345">...</div>
<li class="inbox-item" id="lead-12345">...</li>
```

**What to note:**
- The **class name** of the lead container (e.g., `lead-item`)
- Any **data attributes** (e.g., `data-lead-id`, `data-job-id`)
- Any **id patterns** (e.g., `lead-12345`)

### B. Lead ID
Look inside the lead container for the unique ID:
```html
<!-- Might be in: -->
<div data-id="12345">
<span class="lead-id">12345</span>
<a href="/lead/12345">
```

### C. Customer Name
```html
<!-- Might be in: -->
<span class="customer-name">John Smith</span>
<div class="requester-name">John Smith</div>
<h3 class="client-name">John Smith</h3>
```

### D. Customer Phone (if visible)
```html
<!-- Might be in: -->
<span class="phone-number">0412 345 678</span>
<div class="contact-phone">0412 345 678</div>
<a href="tel:+61412345678">0412 345 678</a>
```

### E. Job Type / Service
```html
<!-- Might be in: -->
<span class="job-type">Plumbing - Blocked Drain</span>
<div class="service-category">Plumbing</div>
<h2 class="job-title">Blocked Drain</h2>
```

### F. Location
```html
<!-- Might be in: -->
<span class="location">Melbourne, VIC</span>
<div class="suburb">Glen Waverley</div>
<p class="job-location">3150</p>
```

## Step 4: Record Your Findings

Fill in this template with what you find:

```javascript
// LEAD SELECTORS FOUND:

// 1. Lead Container (the parent element containing all lead info)
LEAD_CONTAINER_SELECTOR = '.your-class-here';  // e.g., '.lead-item' or '[data-lead-id]'

// 2. Lead ID
LEAD_ID_SELECTOR = '.your-class-here';         // e.g., '.lead-id' or '[data-lead-id]'
LEAD_ID_ATTRIBUTE = 'data-lead-id';            // If it's in an attribute instead of text

// 3. Customer Name
CUSTOMER_NAME_SELECTOR = '.your-class-here';   // e.g., '.customer-name'

// 4. Customer Phone (if visible)
CUSTOMER_PHONE_SELECTOR = '.your-class-here';  // e.g., '.phone-number'

// 5. Job Type
JOB_TYPE_SELECTOR = '.your-class-here';        // e.g., '.job-type'

// 6. Location
LOCATION_SELECTOR = '.your-class-here';        // e.g., '.location'

// 7. Any other useful fields?
// - Job date/time:
// - Job description:
// - Price/budget:
```

## Step 5: Test Your Selectors

In the DevTools Console tab, test your selectors:

```javascript
// Test lead container
document.querySelectorAll('.your-lead-container-class').length
// Should return the number of visible leads

// Test getting first lead's data
const firstLead = document.querySelector('.your-lead-container-class');
console.log('Lead ID:', firstLead.querySelector('.lead-id-class')?.textContent);
console.log('Customer:', firstLead.querySelector('.customer-name-class')?.textContent);
console.log('Job Type:', firstLead.querySelector('.job-type-class')?.textContent);
```

## Common Patterns to Look For

### Pattern 1: All-in-one data attribute
```html
<div class="lead" data-lead='{"id":"123","customer":"John","phone":"+61..."}'>
```

### Pattern 2: Separate data attributes
```html
<div class="lead" data-id="123" data-customer="John" data-phone="+61...">
```

### Pattern 3: Text content in spans/divs
```html
<div class="lead">
  <span class="id">123</span>
  <span class="customer">John</span>
  <span class="phone">+61...</span>
</div>
```

## Tips

1. **Right-click → Copy → Copy selector** in DevTools to get the exact CSS selector
2. **Look for stable selectors** - avoid randomly generated class names like `css-abc123`
3. **Check multiple leads** to ensure the selector works for all of them
4. **Hover over elements** in DevTools to see what gets highlighted on the page
5. **Expand/collapse elements** to find nested data

## What If Phone Number is Hidden?

If customer phone is not visible in the inbox:
- Click on a lead to open details page
- Inspect where the phone number appears
- We'll need to detect when lead details are opened
- Or trigger a click event to reveal the phone

## Screenshot Locations (Optional)

If you want to share screenshots for me to help:
1. Take a screenshot of the inbox with leads visible
2. Take a screenshot of the DevTools with a lead element highlighted
3. Share the HTML snippet from DevTools (right-click element → Copy → Copy outerHTML)

---

## 🎯 Your Mission

Find and fill in the template above, then paste it back to me!

I'll then update the Chrome extension with the correct selectors.
