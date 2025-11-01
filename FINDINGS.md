# 🔍 ServiceSeeking Research Findings

## Summary

ServiceSeeking **does NOT provide a public API** for lead data. All lead information is rendered server-side in HTML.

## Detection Method

✅ **DOM Monitoring** (Content Script)

The Chrome extension must:
1. Monitor the inbox DOM for new lead cards appearing
2. Extract data from HTML using CSS selectors
3. Detect when leads are clicked to access full details

## Key Selectors Discovered

### Inbox List View

| Data Field | Selector | Example Value |
|------------|----------|---------------|
| Lead Card | `[id^="matched-lead-card-"]` | `matched-lead-card-5181166` |
| Lead ID | Extract from card ID | `5181166` |
| Customer Name | `.text-sm:first-of-type` | `Priank` |
| Job Type | `.text-sm.font-semibold:first-of-type` | `General carpentry` |
| Location | `a[href*="google.com/maps"]` | `Aspendale, VIC, 3195` |
| Time Posted | `.text-xs.text-right span` | `10 hours ago` |
| Verified Badge | `svg[width="13"]` present | boolean |

### Lead Detail Modal

| Data Field | Selector | Notes |
|------------|----------|-------|
| Modal | `#lead-details-modal` | Opens when lead clicked |
| Job ID | `.text-base.mb-5 span.font-semibold` | Same as lead ID |
| Customer Name | `.text-lg.font-normal` | Full name |
| Description | `.text-base.font-normal.mb-5` | Full job description |
| Budget | First `.flex.items-end` group | "No Set Budget" or amount |
| Timing | Second `.flex.items-end` group | "Next couple of weeks" |

### ⚠️ Critical Finding: Phone Number

**Customer phone number is NOT visible** in either:
- Inbox list view
- Lead detail modal

Phone number is only revealed when clicking **"Contact Customer"** button, which likely:
1. Charges the tradie for the lead
2. Opens another modal or page with contact details
3. Requires additional inspection

## Recommended Approach

### Phase 1: Basic Lead Detection (Implemented)
- Monitor inbox for new lead cards
- Extract: Lead ID, Customer Name, Job Type, Location
- Send to backend WITHOUT phone number
- Backend stores lead data

### Phase 2: Phone Number Extraction (Future)
Options:
1. **Auto-click "Contact Customer"** - Risky, costs money
2. **Wait for manual click** - Safer, user clicks when ready
3. **Use notification system** - Alert tradie, they call manually

## Implementation Files

- `tools/FOUND_SELECTORS.js` - Complete selector configuration
- `tools/SELECTOR_GUIDE.md` - Step-by-step guide
- `tools/selectors-template.js` - Template for future updates

## Next Steps

1. ✅ Update Chrome extension with actual selectors
2. ✅ Implement MutationObserver for real-time detection
3. ⚠️ Test lead detection in ServiceSeeking inbox
4. ⏳ Decide on phone number extraction strategy
5. ⏳ Deploy to Azure and test end-to-end

## Testing

Test script provided in `FOUND_SELECTORS.js`:

```javascript
// In ServiceSeeking inbox, open DevTools console:
// 1. Paste FOUND_SELECTORS.js
// 2. Run:
getAllLeadsFromInbox();  // See all current leads

monitorForNewLeads((lead) => {
  console.log('NEW LEAD:', lead);
});
```

## Cost Implications

Since phone numbers require clicking "Contact Customer" (which likely costs money):

**Option A: Free Mode**
- Detect new leads
- Send notification to tradie
- Tradie manually clicks to get phone
- No automated calling

**Option B: Paid Mode** (Original Plan)
- Auto-click "Contact Customer"
- Extract phone number
- Trigger ACS call to tradie
- Fully automated

**Recommendation:** Start with Option A, add Option B later with user opt-in.

---

**Date:** 2025-11-02
**Status:** ✅ Selectors confirmed and documented
**Files Updated:** Chrome extension ready for selector integration
