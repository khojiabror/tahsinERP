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

    // Bir martalik kod maydonlari: faqat raqam qoldiriladi.
    document.querySelectorAll('input[autocomplete="one-time-code"]').forEach(function (inp) {
        inp.addEventListener('input', function () {
            var v = inp.value.replace(/\D/g, '').slice(0, 6);
            if (v !== inp.value) inp.value = v;
        });
    });
})();
