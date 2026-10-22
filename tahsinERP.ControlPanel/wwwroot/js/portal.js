// Mijoz portali: "Nusxa olish" tugmalari (CSP: faqat 'self' skriptlar, inline JS yo'q).
// "Nusxa olindi" matni joriy tilda tugmaning data-copied atributidan olinadi (Razor: L.T("Ptl_Copied")).
document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
        var el = document.getElementById(btn.getAttribute('data-copy'));
        if (!el) return;
        var text = el.value !== undefined ? el.value : el.textContent;
        var copied = btn.getAttribute('data-copied') || 'Nusxa olindi ✓';
        var done = function () { var t = btn.textContent; btn.textContent = copied; setTimeout(function () { btn.textContent = t; }, 2000); };
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(done);
        } else if (el.select) {
            el.select(); document.execCommand('copy'); done();
        }
    });
});
