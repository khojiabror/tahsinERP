/*
 * Harakatsizlikda avtomatik chiqish (AccountController.IdleTimeoutMinutes, standart 30 daqiqa).
 * - Faollik (sichqoncha, klaviatura, aylantirish, teginish) barcha tablar va to'liq ekran iframe'i uchun umumiy:
 *   oxirgi faollik vaqti localStorage'da ("erp-last-activity").
 * - Foydalanuvchi ishlayotgan bo'lsa, lekin so'rov yubormasa (uzun forma) — server seansi KeepAlive bilan uzaytiriladi.
 * - Oxirgi 60 soniyada ogohlantirish; vaqt tugasa — Account/Logout.
 * Server tomonda chipta ham 30 daqiqa sliding — brauzer yopilgan bo'lsa ham seans tugaydi.
 */
(function () {
    'use strict';

    var script = document.currentScript;
    if (!script) return;
    var idleMs = (parseInt(script.getAttribute('data-idle-minutes'), 10) || 30) * 60 * 1000;
    var keepAliveUrl = script.getAttribute('data-keepalive-url');
    var logoutUrl = script.getAttribute('data-logout-url') || '/Account/Logout';
    var warningText = script.getAttribute('data-warning') || '{0}';
    var stayText = script.getAttribute('data-stay') || 'OK';

    var KEY = 'erp-last-activity';
    var WARN_MS = 60 * 1000;
    var PING_MS = 5 * 60 * 1000;          // faol foydalanuvchi uchun serverga ko'pi bilan shuncha oraliqda
    var memoryLast = Date.now();
    var lastPing = Date.now();
    var lastWrite = 0;
    var warnBox = null;
    var loggingOut = false;

    function readLast() {
        try {
            var v = parseInt(window.localStorage.getItem(KEY), 10);
            if (v && v > memoryLast) memoryLast = v;
        } catch (e) { /* localStorage yo'q — faqat shu tab */ }
        return memoryLast;
    }

    function writeLast(now) {
        memoryLast = now;
        if (now - lastWrite < 5000) return;  // har harakatda yozmaymiz
        lastWrite = now;
        try { window.localStorage.setItem(KEY, String(now)); } catch (e) { }
    }

    function ping() {
        if (!keepAliveUrl) return;
        lastPing = Date.now();
        try {
            fetch(keepAliveUrl, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (r) { if (r.redirected || r.status === 401) logout(); })
                .catch(function () { });
        } catch (e) { }
    }

    function activity() {
        var now = Date.now();
        writeLast(now);
        hideWarning();
        if (now - lastPing > PING_MS) ping();
    }

    function hideWarning() {
        if (warnBox) { warnBox.parentNode && warnBox.parentNode.removeChild(warnBox); warnBox = null; }
    }

    function showWarning(secondsLeft) {
        if (!warnBox) {
            warnBox = document.createElement('div');
            warnBox.setAttribute('role', 'alert');
            warnBox.style.cssText = 'position:fixed;right:16px;bottom:16px;z-index:2147483646;max-width:360px;padding:14px 16px;' +
                'border-radius:10px;background:#fff7e6;color:#5c3d00;border:1px solid #f0c36d;box-shadow:0 6px 24px rgba(0,0,0,.18);font-size:14px;';
            var text = document.createElement('div');
            text.className = 'erp-idle-text';
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-sm btn-primary mt-50';
            btn.textContent = stayText;
            btn.addEventListener('click', function () { activity(); ping(); });
            warnBox.appendChild(text);
            warnBox.appendChild(btn);
            document.body.appendChild(warnBox);
        }
        warnBox.querySelector('.erp-idle-text').textContent = warningText.replace('{0}', String(secondsLeft));
    }

    function logout() {
        if (loggingOut) return;
        loggingOut = true;
        try { window.localStorage.removeItem(KEY); } catch (e) { }
        // To'liq ekran iframe ichida bo'lsak — butun oynani chiqaramiz
        try { (window.top || window).location.href = logoutUrl; } catch (e) { window.location.href = logoutUrl; }
    }

    ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart', 'wheel'].forEach(function (ev) {
        window.addEventListener(ev, activity, { passive: true, capture: true });
    });
    window.addEventListener('storage', function (e) { if (e.key === KEY) { readLast(); hideWarning(); } });

    writeLast(Date.now());
    setInterval(function () {
        var left = idleMs - (Date.now() - readLast());
        if (left <= 0) logout();
        else if (left <= WARN_MS) showWarning(Math.ceil(left / 1000));
        else hideWarning();
    }, 1000);
})();
