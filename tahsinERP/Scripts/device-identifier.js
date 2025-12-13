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
            
            // Send via AJAX to store in session/cookie
            if (typeof jQuery !== 'undefined') {
                $.ajax({
                    url: '/Account/SetDeviceId',
                    type: 'POST',
                    data: { deviceId: deviceId },
                    timeout: 5000,
                    success: function(response) {
                        if (response && response.success) {
                            console.log('Device ID sent to server:', deviceId);
                        }
                    },
                    error: function(xhr, status, error) {
                        // Silently fail - not critical, but retry after delay
                        setTimeout(sendDeviceIdToServer, 2000);
                    }
                });
            } else {
                // Fallback: use XMLHttpRequest if jQuery is not available
                try {
                    var xhr = new XMLHttpRequest();
                    xhr.open('POST', '/Account/SetDeviceId', true);
                    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
                    xhr.onreadystatechange = function() {
                        if (xhr.readyState === 4) {
                            if (xhr.status === 200) {
                                console.log('Device ID sent to server:', deviceId);
                            }
                        }
                    };
                    xhr.send('deviceId=' + encodeURIComponent(deviceId));
                } catch (e) {
                    // If all fails, retry later
                    setTimeout(sendDeviceIdToServer, 2000);
                }
            }
        } catch (e) {
            // If error occurs, retry after delay
            setTimeout(sendDeviceIdToServer, 2000);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', sendDeviceIdToServer);
    } else {
        sendDeviceIdToServer();
    }

    // Also send on page visibility change (when user returns to tab)
    document.addEventListener('visibilitychange', function() {
        if (!document.hidden) {
            sendDeviceIdToServer();
        }
    });

    // Expose function globally for manual calls if needed
    window.getTahsinERPDeviceId = getDeviceId;
})();
