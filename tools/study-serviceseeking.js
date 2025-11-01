/**
 * ServiceSeeking Study Tool
 *
 * This script opens a Chromium browser with DevTools to study ServiceSeeking:
 * - Network requests (API calls)
 * - HTML structure (for lead detection)
 * - JavaScript events
 *
 * Usage:
 * 1. npm install
 * 2. npm run study-serviceseeking
 * 3. Login to ServiceSeeking manually
 * 4. Navigate to leads page
 * 5. Script will log network calls and page structure
 */

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

async function studyServiceSeeking() {
  console.log('🧱 LeadBridge - ServiceSeeking Study Tool');
  console.log('=========================================\n');

  // Launch browser with DevTools
  const browser = await chromium.launch({
    headless: false,
    devtools: true,
    slowMo: 100
  });

  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
  });

  const page = await context.newPage();

  // Create output directory for logs
  const outputDir = path.join(__dirname, 'output');
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir);
  }

  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const networkLogFile = path.join(outputDir, `network-log-${timestamp}.json`);
  const htmlSnapshotFile = path.join(outputDir, `page-snapshot-${timestamp}.html`);

  const networkLogs = [];

  // Monitor network requests
  page.on('request', request => {
    const requestData = {
      timestamp: new Date().toISOString(),
      type: 'request',
      method: request.method(),
      url: request.url(),
      headers: request.headers(),
      resourceType: request.resourceType()
    };

    // Only log interesting requests
    if (request.url().includes('serviceseeking') ||
        request.url().includes('api') ||
        request.url().includes('leads') ||
        request.url().includes('jobs')) {
      console.log(`📤 ${request.method()} ${request.url()}`);
      networkLogs.push(requestData);
    }
  });

  page.on('response', async response => {
    const responseData = {
      timestamp: new Date().toISOString(),
      type: 'response',
      status: response.status(),
      url: response.url(),
      headers: response.headers()
    };

    // Log API responses
    if (response.url().includes('serviceseeking') ||
        response.url().includes('api') ||
        response.url().includes('leads') ||
        response.url().includes('jobs')) {

      console.log(`📥 ${response.status()} ${response.url()}`);

      // Try to capture response body for JSON APIs
      try {
        const contentType = response.headers()['content-type'] || '';
        if (contentType.includes('application/json')) {
          const body = await response.json();
          responseData.body = body;
          console.log(`   Body preview: ${JSON.stringify(body).substring(0, 200)}...`);
        }
      } catch (e) {
        // Not JSON or can't read body
      }

      networkLogs.push(responseData);
    }
  });

  // Monitor console logs from the page
  page.on('console', msg => {
    const type = msg.type();
    const text = msg.text();

    if (type === 'error' || type === 'warning') {
      console.log(`🔍 Console ${type}: ${text}`);
    }
  });

  // Navigate to ServiceSeeking
  console.log('\n📍 Navigating to ServiceSeeking...\n');
  await page.goto('https://www.serviceseeking.com.au/', {
    waitUntil: 'networkidle'
  });

  console.log('\n✅ Browser opened! Please:');
  console.log('   1. Login to ServiceSeeking');
  console.log('   2. Navigate to your leads/jobs page');
  console.log('   3. Interact with the page normally');
  console.log('   4. Press Ctrl+C in this terminal when done\n');
  console.log('📊 Network activity and page structure will be logged automatically.\n');

  // Periodically capture page structure
  const structureInterval = setInterval(async () => {
    try {
      // Check if we're on a leads page
      const currentUrl = page.url();

      if (currentUrl.includes('lead') ||
          currentUrl.includes('job') ||
          currentUrl.includes('quote') ||
          currentUrl.includes('dashboard')) {

        console.log(`\n🔍 Analyzing page structure: ${currentUrl}`);

        // Capture HTML snapshot
        const html = await page.content();
        fs.writeFileSync(htmlSnapshotFile, html);
        console.log(`   ✅ HTML saved to: ${path.basename(htmlSnapshotFile)}`);

        // Try to detect lead elements
        const leadElements = await page.evaluate(() => {
          // Look for common patterns
          const selectors = [
            '[data-lead-id]',
            '[data-job-id]',
            '.lead-item',
            '.job-item',
            '.quote-item',
            '[class*="lead"]',
            '[class*="job"]',
            '[class*="quote"]'
          ];

          const found = [];

          selectors.forEach(selector => {
            const elements = document.querySelectorAll(selector);
            if (elements.length > 0) {
              found.push({
                selector: selector,
                count: elements.length,
                firstElementHTML: elements[0]?.outerHTML?.substring(0, 500)
              });
            }
          });

          return found;
        });

        if (leadElements.length > 0) {
          console.log('\n   🎯 Potential lead elements found:');
          leadElements.forEach(el => {
            console.log(`      - Selector: ${el.selector}`);
            console.log(`        Count: ${el.count}`);
            console.log(`        Sample: ${el.firstElementHTML?.substring(0, 100)}...\n`);
          });

          // Save to analysis file
          const analysisFile = path.join(outputDir, `lead-analysis-${timestamp}.json`);
          fs.writeFileSync(analysisFile, JSON.stringify(leadElements, null, 2));
        }
      }
    } catch (e) {
      console.error('Error analyzing page:', e.message);
    }
  }, 10000); // Check every 10 seconds

  // Save network logs periodically
  const saveInterval = setInterval(() => {
    if (networkLogs.length > 0) {
      fs.writeFileSync(networkLogFile, JSON.stringify(networkLogs, null, 2));
      console.log(`💾 Network logs saved: ${networkLogs.length} entries`);
    }
  }, 30000); // Save every 30 seconds

  // Handle cleanup on exit
  process.on('SIGINT', async () => {
    console.log('\n\n⏹️  Stopping...');

    clearInterval(structureInterval);
    clearInterval(saveInterval);

    // Final save
    if (networkLogs.length > 0) {
      fs.writeFileSync(networkLogFile, JSON.stringify(networkLogs, null, 2));
      console.log(`\n✅ Final network log saved: ${path.basename(networkLogFile)}`);
    }

    const html = await page.content();
    fs.writeFileSync(htmlSnapshotFile, html);
    console.log(`✅ Final HTML snapshot saved: ${path.basename(htmlSnapshotFile)}`);

    console.log('\n📁 Output files in: ./tools/output/');
    console.log('\n👋 Thanks! Use these logs to update the Chrome extension.\n');

    await browser.close();
    process.exit(0);
  });

  // Keep script running
  await new Promise(() => {});
}

// Run
studyServiceSeeking().catch(console.error);
