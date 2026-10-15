/**
 * Device Identifier Script
 * Generates and stores a unique device identifier to be used as MAC address replacement
 * This identifier is stored in localStorage and sent to server with requests
 */
(function() {
    'use strict';

    // Generate a unique device identifier
    function generateDeviceId() {
        var canvas = document.createElement('canvas');
        var ctx = canvas.getContext('2d');
        ctx.textBaseline = 'top';
        ctx.font = '14px "Arial"';
        ctx.textBaseline = 'alphabetic';
        ctx.fillStyle = '#f60';
        ctx.fillRect(125, 1, 62, 20);
        ctx.fillStyle = '#069';
        ctx.fillText('Device fingerprint', 2, 15);
        ctx.fillStyle = 'rgba(102, 204, 0, 0.7)';
        ctx.fillText('Device fingerprint', 4, 17);
        
        var fingerprint = canvas.toDataURL();
        
        // Combine with other browser characteristics
        var components = [
            navigator.userAgent,
            navigator.language,
            screen.width + 'x' + screen.height,
            new Date().getTimezoneOffset(),
            fingerprint,
            navigator.platform,
            navigator.cookieEnabled ? '1' : '0',
            screen.colorDepth
        ];
        
        // Create a hash-like identifier
        var combined = components.join('|');
        var hash = 0;
        for (var i = 0; i < combined.length; i++) {
            var char = combined.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32bit integer
        }
        
        // Convert to hex and format like MAC address
        var hexHash = Math.abs(hash).toString(16);
        // Pad to 12 characters (padStart polyfill for older browsers)
        while (hexHash.length < 12) {
            hexHash = '0' + hexHash;
        }
        hexHash = hexHash.substring(0, 12);
        
        var macLike = [];
        for (var j = 0; j < 6; j++) {
            macLike.push(hexHash.substring(j * 2, (j * 2) + 2));
        }
        
        return macLike.join(':').toUpperCase();
    }

    // Get or create device identifier
    function getDeviceId() {
        var deviceId = localStorage.getItem('tahsinERP_deviceId');
        
        if (!deviceId) {
            deviceId = generateDeviceId();
            localStorage.setItem('tahsinERP_deviceId', deviceId);
        }
        
        return deviceId;
    }

    // Send device identifier to server
    function sendDeviceIdToServer() {
        try {
            var deviceId = getDeviceId();
            
            if (!deviceId) {
                console.error('Device ID is empty, cannot send to server');
                return;
            }
            
            // Send via AJAX to store in session/cookie
            if (typeof jQuery !== 'undefined') {
                $.ajax({
                    url: '/Account/SetDeviceId',
                    type: 'POST',
                    data: { deviceId: deviceId },
                    timeout: 10000,
                    dataType: 'json',
                    success: function(response) {
                        if (response && response.success) {
                            console.log('Device ID sent to server successfully:', deviceId);
                        } else {
                            console.warn('Device ID send failed:', response ? response.message : 'Unknown error');
                            // Retry after delay
                            setTimeout(sendDeviceIdToServer, 3000);
                        }
                    },
                    error: function(xhr, status, error) {
                        console.warn('Device ID send error:', status, error);
                        // Retry after delay
                        setTimeout(sendDeviceIdToServer, 3000);
                    }
                });
            } else {
                // Fallback: use XMLHttpRequest if jQuery is not available
                try {
                    var xhr = new XMLHttpRequest();
                    xhr.open('POST', '/Account/SetDeviceId', true);
                    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
                    var csrf = document.querySelector('input[name="__RequestVerificationToken"]');
                    if (csrf) xhr.setRequestHeader('RequestVerificationToken', csrf.value);
                    xhr.timeout = 10000;
                    xhr.onreadystatechange = function() {
                        if (xhr.readyState === 4) {
                            if (xhr.status === 200) {
                                try {
                                    var response = JSON.parse(xhr.responseText);
                                    if (response && response.success) {
                                        console.log('Device ID sent to server successfully:', deviceId);
                                    } else {
                                        console.warn('Device ID send failed:', response ? response.message : 'Unknown error');
                                        setTimeout(sendDeviceIdToServer, 3000);
                                    }
                                } catch (e) {
                                    console.log('Device ID sent to server (response parse failed):', deviceId);
                                }
                            } else {
                                console.warn('Device ID send failed with status:', xhr.status);
                                setTimeout(sendDeviceIdToServer, 3000);
                            }
                        }
                    };
                    xhr.ontimeout = function() {
                        console.warn('Device ID send timeout');
                        setTimeout(sendDeviceIdToServer, 3000);
                    };
                    xhr.send('deviceId=' + encodeURIComponent(deviceId));
                } catch (e) {
                    console.error('Device ID send exception:', e);
                    // If all fails, retry later
                    setTimeout(sendDeviceIdToServer, 3000);
                }
            }
        } catch (e) {
            console.error('Device ID send error:', e);
            // If error occurs, retry after delay
            setTimeout(sendDeviceIdToServer, 3000);
        }
    }

    // Send device ID immediately and repeatedly to ensure it's stored
    function initializeDeviceId() {
        // Send immediately using XMLHttpRequest (works without jQuery)
        sendDeviceIdToServer();
        
        // Send again after 100ms to ensure it goes through
        setTimeout(sendDeviceIdToServer, 100);
        
        // Send again after 500ms
        setTimeout(sendDeviceIdToServer, 500);
        
        // Send again after 1 second
        setTimeout(sendDeviceIdToServer, 1000);
        
        // Also send after jQuery is loaded (if available)
        if (typeof jQuery !== 'undefined') {
            sendDeviceIdToServer();
        } else {
            // Wait for jQuery and send again
            var checkJQuery = setInterval(function() {
                if (typeof jQuery !== 'undefined') {
                    clearInterval(checkJQuery);
                    sendDeviceIdToServer();
                }
            }, 100);
            
            // Stop checking after 5 seconds
            setTimeout(function() {
                clearInterval(checkJQuery);
            }, 5000);
        }
        
        // Send on page visibility change (when user returns to tab)
        document.addEventListener('visibilitychange', function() {
            if (!document.hidden) {
                sendDeviceIdToServer();
            }
        });
        
        // Send before page unload (to ensure it's sent)
        window.addEventListener('beforeunload', function() {
            // Use synchronous request for beforeunload
            try {
                var deviceId = getDeviceId();
                if (deviceId) {
                    var xhr = new XMLHttpRequest();
                    xhr.open('POST', '/Account/SetDeviceId', false); // synchronous
                    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
                    var csrf = document.querySelector('input[name="__RequestVerificationToken"]');
                    if (csrf) xhr.setRequestHeader('RequestVerificationToken', csrf.value);
                    xhr.send('deviceId=' + encodeURIComponent(deviceId));
                }
            } catch (e) {
                // Ignore errors on beforeunload
            }
        });
        
        // Intercept form submissions and AJAX requests to send device ID
        // This ensures device ID is sent before any action that might log
        var originalSubmit = HTMLFormElement.prototype.submit;
        HTMLFormElement.prototype.submit = function() {
            sendDeviceIdToServer();
            return originalSubmit.apply(this, arguments);
        };
        
        // Intercept jQuery AJAX if available
        if (typeof jQuery !== 'undefined') {
            var originalAjax = jQuery.ajax;
            jQuery.ajax = function(options) {
                sendDeviceIdToServer();
                return originalAjax.apply(this, arguments);
            };
        }
    }

    // Start initialization immediately - multiple times to ensure it runs
    function startInitialization() {
        initializeDeviceId();
    }
    
    // Try multiple times to ensure initialization
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startInitialization);
    } else {
        startInitialization();
    }
    
    // Also try immediately if document is already loaded
    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        startInitialization();
    }
    
    // One more time after a short delay
    setTimeout(startInitialization, 50);

    // Expose function globally for manual calls if needed
    window.getTahsinERPDeviceId = getDeviceId;
})();
