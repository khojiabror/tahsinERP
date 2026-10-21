// Mavzu (yorug'/qorong'i) — <head> da sinxron yuklanadi, sahifa "miltillamasin".
(function () {
    try {
        var t = localStorage.getItem('cp-theme');
        if (t === 'dark' || t === 'light') document.documentElement.setAttribute('data-theme', t);
    } catch (e) { }
})();
