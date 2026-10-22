// Kunduzgi/tungi rejim. <head> da sinxron yuklanadi — sahifa "miltillamasin".
// Tanlov localStorage'da ('erp-theme': light | dark; yo'q bo'lsa — tizim sozlamasi, prefers-color-scheme).
// <html data-theme> doim aniq qiymat oladi (light/dark), CSS faqat shunga qaraydi; data-theme-mode — tanlov (auto/light/dark).
(function () {
    var KEY = 'erp-theme';
    var root = document.documentElement;
    var mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

    function mode() {
        try {
            var t = localStorage.getItem(KEY);
            return t === 'dark' || t === 'light' ? t : 'auto';
        } catch (e) { return 'auto'; }
    }

    function resolved() {
        var m = mode();
        return m === 'auto' ? (mq && mq.matches ? 'dark' : 'light') : m;
    }

    function syncUi() {
        var m = mode(), r = resolved();
        var icons = document.querySelectorAll('[data-theme-icon]');
        for (var i = 0; i < icons.length; i++)
            icons[i].className = 'ficon bx ' + (m === 'auto' ? 'bx-adjust' : r === 'dark' ? 'bx-moon' : 'bx-sun');
        var items = document.querySelectorAll('[data-theme-set]');
        for (var j = 0; j < items.length; j++)
            items[j].classList.toggle('active', items[j].getAttribute('data-theme-set') === m);
    }

    function apply() {
        var r = resolved();
        root.setAttribute('data-theme', r);
        root.setAttribute('data-theme-mode', mode());
        // ApexCharts global sozlamalari (grafiklar shu sahifada keyin yaratiladi)
        window.Apex = window.Apex || {};
        window.Apex.chart = window.Apex.chart || {};
        window.Apex.chart.foreColor = r === 'dark' ? '#a9b6cf' : '#475F7B';
        window.Apex.chart.background = 'transparent';
        window.Apex.grid = { borderColor: r === 'dark' ? '#3a4466' : '#dfe3e7' };
        window.Apex.tooltip = { theme: r };
        if (document.body) syncUi();
    }

    apply();
    if (mq) {
        if (mq.addEventListener) mq.addEventListener('change', apply);
        else if (mq.addListener) mq.addListener(apply);
    }

    // Grafiklar uchun joriy asosiy rang (#brand-vars dagi --accent)
    window.accentColor = function () {
        var v = window.getComputedStyle(root).getPropertyValue('--accent');
        return (v && v.trim()) || '#3b5bfd';
    };

    window.erpTheme = {
        mode: mode,
        resolved: resolved,
        set: function (m) {
            try {
                if (m === 'dark' || m === 'light') localStorage.setItem(KEY, m);
                else localStorage.removeItem(KEY);
            } catch (e) { }
            // Almashtirish paytida CSS transition'lar (kartalar 0.3s) o'chiriladi — ranglar bir zumda o'zgarsin
            root.classList.add('theme-switching');
            apply();
            void root.offsetWidth;
            setTimeout(function () { root.classList.remove('theme-switching'); }, 50);
        }
    };

    document.addEventListener('DOMContentLoaded', syncUi);
    document.addEventListener('click', function (e) {
        var el = e.target && e.target.closest ? e.target.closest('[data-theme-set]') : null;
        if (!el) return;
        e.preventDefault();
        window.erpTheme.set(el.getAttribute('data-theme-set'));
    });
})();
