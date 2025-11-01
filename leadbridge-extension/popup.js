// LeadBridge AU - Popup UI Controller

document.addEventListener('DOMContentLoaded', async () => {
  // Elements
  const tradiePhoneInput = document.getElementById('tradiePhone');
  const toggleMonitoring = document.getElementById('toggleMonitoring');
  const saveBtn = document.getElementById('saveBtn');
  const testBtn = document.getElementById('testBtn');
  const statusText = document.getElementById('statusText');
  const statusDot = document.getElementById('statusDot');
  const leadCount = document.getElementById('leadCount');
  const messageDiv = document.getElementById('message');

  // Load current status
  await loadStatus();

  // Event listeners
  saveBtn.addEventListener('click', handleSave);
  testBtn.addEventListener('click', handleTest);
  toggleMonitoring.addEventListener('change', handleToggleChange);

  // Load current settings and status
  async function loadStatus() {
    try {
      const response = await chrome.runtime.sendMessage({ action: 'getStatus' });

      if (response) {
        tradiePhoneInput.value = response.tradiePhone || '';
        toggleMonitoring.checked = response.enabled || false;
        leadCount.textContent = response.leadCount || 0;

        updateStatusDisplay(response.enabled);
      }
    } catch (error) {
      console.error('Error loading status:', error);
      showMessage('Failed to load settings', 'error');
    }
  }

  // Handle save button click
  async function handleSave() {
    const phone = tradiePhoneInput.value.trim();

    // Validate phone number
    if (!phone) {
      showMessage('Please enter your phone number', 'error');
      return;
    }

    if (!isValidAustralianPhone(phone)) {
      showMessage('Please enter a valid Australian phone number', 'error');
      return;
    }

    try {
      // Save phone number
      await chrome.runtime.sendMessage({
        action: 'setTradiePhone',
        phone: phone
      });

      // Save monitoring state
      await chrome.runtime.sendMessage({
        action: 'toggleMonitoring',
        enabled: toggleMonitoring.checked
      });

      showMessage('Settings saved successfully!', 'success');
      updateStatusDisplay(toggleMonitoring.checked);

    } catch (error) {
      console.error('Error saving settings:', error);
      showMessage('Failed to save settings', 'error');
    }
  }

  // Handle toggle change
  async function handleToggleChange() {
    updateStatusDisplay(toggleMonitoring.checked);
  }

  // Handle test button click
  async function handleTest() {
    testBtn.disabled = true;
    testBtn.textContent = 'Testing...';

    try {
      const response = await chrome.runtime.sendMessage({ action: 'testPoll' });

      if (response.success) {
        showMessage('Test poll completed! Check console for details.', 'success');
      } else {
        showMessage(`Test failed: ${response.error}`, 'error');
      }
    } catch (error) {
      console.error('Error testing:', error);
      showMessage('Test connection failed', 'error');
    } finally {
      testBtn.disabled = false;
      testBtn.textContent = 'Test Connection';
    }
  }

  // Update status display
  function updateStatusDisplay(enabled) {
    if (enabled) {
      statusText.textContent = 'Active';
      statusDot.classList.add('active');
    } else {
      statusText.textContent = 'Disabled';
      statusDot.classList.remove('active');
    }
  }

  // Show message to user
  function showMessage(text, type) {
    messageDiv.textContent = text;
    messageDiv.className = `message ${type}`;
    messageDiv.classList.remove('hidden');

    setTimeout(() => {
      messageDiv.classList.add('hidden');
    }, 4000);
  }

  // Validate Australian phone number
  function isValidAustralianPhone(phone) {
    // Remove spaces and common formatting
    const cleaned = phone.replace(/[\s\-\(\)]/g, '');

    // Check for valid Australian mobile/landline formats
    // Mobile: +614XXXXXXXX or 04XXXXXXXX
    // Landline: +61XXXXXXXXX or 0XXXXXXXXX
    const mobilePattern = /^(\+?61|0)4\d{8}$/;
    const landlinePattern = /^(\+?61|0)[2-9]\d{8}$/;

    return mobilePattern.test(cleaned) || landlinePattern.test(cleaned);
  }
});
