// Panel qobig'i: mobil menyu, mavzu almashtirish, kirish ekranidagi soat (CSP: faqat 'self' skriptlar).
(function () {
    var body = document.body;

    document.querySelectorAll('[data-nav-toggle]').forEach(function (b) {
        b.addEventListener('click', function () { body.classList.toggle('nav-open'); });
    });
    document.querySelectorAll('[data-nav-close]').forEach(function (b) {
        b.addEventListener('click', function () { body.classList.remove('nav-open'); });
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') body.classList.remove('nav-open'); });

    document.querySelectorAll('[data-theme-toggle]').forEach(function (b) {
        b.addEventListener('click', function () {
            var root = document.documentElement;
            var cur = root.getAttribute('data-theme') ||
                (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
            var next = cur === 'dark' ? 'light' : 'dark';
            root.setAttribute('data-theme', next);
            try { localStorage.setItem('cp-theme', next); } catch (e) { }
        });
    });

    var clock = document.querySelector('[data-clock]');
    if (clock) {
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        var tick = function () {
            var d = new Date();
            clock.textContent = pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes()) + ':' + pad(d.getUTCSeconds()) + ' UTC';
        };
        tick();
        setInterval(tick, 1000);
    }

    // Xavfli amallar (yangilash, rollback): <form data-confirm="..."> — yuborishdan oldin tasdiq.
    document.querySelectorAll('form[data-confirm]').forEach(function (f) {
        f.addEventListener('submit', function (e) {
            if (!window.confirm(f.getAttribute('data-confirm'))) e.preventDefault();
        });
    });

    // Tanlov o'zgarganda formani yuborish (jadval "Qatorlar" tanlagichi va h.k.).
    document.querySelectorAll('select[data-autosubmit]').forEach(function (s) {
        s.addEventListener('change', function () { if (s.form) s.form.submit(); });
    });

    // Bir martalik kod maydonlari: faqat raqam qoldiriladi.
    document.querySelectorAll('input[autocomplete="one-time-code"]').forEach(function (inp) {
        inp.addEventListener('input', function () {
            var v = inp.value.replace(/\D/g, '').slice(0, 6);
            if (v !== inp.value) inp.value = v;
        });
    });

    // Harakatsizlik taymauti (xodim paneli): data-idle-minutes daqiqa sichqoncha/klaviatura harakati bo'lmasa — chiqish.
    // Oxirgi harakat localStorage'da (bir nechta tab bir-birini hisobga oladi); faol foydalanuvchi uchun server
    // cookie'si /Account/KeepAlive bilan yangilanadi. Server ham shu muddatda cookie'ni o'zi tugatadi (Program.cs).
    var idleMin = parseInt(body.getAttribute('data-idle-minutes'), 10);
    if (idleMin > 0) {
        var LIMIT = idleMin * 60000, WARN = 60000, PING = 60000, KEY = 'cp-last-activity';
        var autoRefresh = !!document.querySelector('meta[http-equiv="refresh"]');
        var now = function () { return Date.now(); };
        var stored = function () { try { return +localStorage.getItem(KEY) || 0; } catch (e) { return 0; } };
        var last = autoRefresh && stored() ? stored() : now(); // avto-yangilanish harakat hisoblanmaydi
        var lastPing = now(), lastMark = 0, done = false, warnEl = null;
        var save = function () { try { localStorage.setItem(KEY, String(last)); } catch (e) { } };
        if (!autoRefresh) save();

        var expire = function () {
            if (done) return;
            done = true;
            var go = function () { location.href = '/Account/Login?expired=true'; };
            var f = document.querySelector('[data-logout-form]');
            if (!f) return go();
            fetch(f.action, { method: 'POST', body: new FormData(f), credentials: 'same-origin', redirect: 'manual' }).then(go, go);
        };
        var ping = function () {
            lastPing = now();
            fetch('/Account/KeepAlive', { credentials: 'same-origin', cache: 'no-store', redirect: 'manual' })
                .then(function (r) { if (r.status !== 204) expire(); }, function () { });
        };
        var activity = function () {
            var t = now();
            if (done || t - lastMark < 3000) return;
            lastMark = t;
            last = t;
            save();
            if (t - lastPing > PING) ping();
            if (warnEl) { warnEl.remove(); warnEl = null; }
        };
        ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart', 'wheel'].forEach(function (ev) {
            document.addEventListener(ev, activity, { passive: true, capture: true });
        });

        var showWarn = function (sec) {
            if (!warnEl) {
                warnEl = document.createElement('div');
                warnEl.className = 'idle-warn';
                warnEl.setAttribute('role', 'alertdialog');
                warnEl.innerHTML = '<b>Seans tugayapti</b><span></span><button type="button">Davom etish</button>';
                warnEl.querySelector('button').addEventListener('click', function () { lastMark = 0; activity(); ping(); });
                body.appendChild(warnEl);
            }
            warnEl.querySelector('span').textContent = 'Harakatsizlik sababli ' + sec + ' soniyadan keyin chiqasiz.';
        };
        setInterval(function () {
            if (done) return;
            var idle = now() - Math.max(last, stored());
            if (idle >= LIMIT) expire();
            else if (idle >= LIMIT - WARN) showWarn(Math.ceil((LIMIT - idle) / 1000));
            else if (warnEl) { warnEl.remove(); warnEl = null; }
        }, 1000);
    }
})();
