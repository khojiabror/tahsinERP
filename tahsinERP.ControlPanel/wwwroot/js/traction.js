// Traction grafiklari (Chart.js 4, MIT — wwwroot/lib/chartjs). Ma'lumot sahifadagi application/json blokidan (CSP: inline skript yo'q).
(function () {
    var el = document.getElementById('traction-data');
    if (!el || typeof Chart === 'undefined') return;
    var d = JSON.parse(el.textContent || '{}');
    var palette = ['#2e7d4f', '#3a6fb0', '#d08a1c', '#8e44ad', '#c0392b', '#16a085', '#7f8c8d'];

    function lines(id, src, stacked) {
        var c = document.getElementById(id);
        if (!c || !src) return;
        var names = Object.keys(src.series || {});
        new Chart(c, {
            type: stacked ? 'bar' : 'line',
            data: {
                labels: src.labels,
                datasets: names.map(function (n, i) {
                    return { label: n, data: src.series[n], borderColor: palette[i % palette.length], backgroundColor: palette[i % palette.length], tension: 0.2, pointRadius: 0 };
                })
            },
            options: { responsive: true, animation: false, scales: { x: { stacked: !!stacked }, y: { stacked: !!stacked, beginAtZero: true, ticks: { precision: 0 } } } }
        });
    }

    lines('chart-active', d.active, false);
    lines('chart-modules', d.modules, true);

    var f = document.getElementById('chart-funnel');
    if (f && d.funnel) {
        new Chart(f, {
            type: 'bar',
            data: { labels: d.funnel.labels, datasets: [{ label: f.getAttribute('data-label') || 'Tenantlar', data: d.funnel.values, backgroundColor: '#3a6fb0' }] },
            options: { indexAxis: 'y', responsive: true, animation: false, plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true, ticks: { precision: 0 } } } }
        });
    }
})();
