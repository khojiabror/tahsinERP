/*
 * Tasks Dashboard grafiklari (ApexCharts). Ma'lumot sahifadagi
 * <script type="application/json" id="taskDashData"> blokidan olinadi — server uni bir marta
 * Json.Encode qiladi (avval 470 qatorli Razor-in-JS edi — MNT-02, TSK-B08).
 */
(function (window, document) {
    'use strict';

    function message(selector, text, cls) {
        var el = document.querySelector(selector);
        if (el) el.innerHTML = '<p class="text-center ' + (cls || 'text-muted') + '">' + text + '</p>';
    }

    function render(selector, options) {
        var el = document.querySelector(selector);
        if (!el) return;
        try {
            new window.ApexCharts(el, options).render();
        } catch (e) {
            message(selector, 'Xatolik: ' + String(e.message).replace(/</g, '&lt;'), 'text-danger');
        }
    }

    function statusChart(d) {
        if (!d.statusSeries.length) return message('#statusPieChart', "Ma'lumot mavjud emas");
        render('#statusPieChart', {
            chart: { type: 'pie', height: 350 },
            labels: d.statusLabels,
            series: d.statusSeries,
            colors: d.statusColors,
            legend: { position: 'bottom' },
            responsive: [{ breakpoint: 480, options: { chart: { width: 200 }, legend: { position: 'bottom' } } }]
        });
    }

    function priorityCharts(d) {
        if (!d.priorityCategories.length) {
            message('#priorityBarChart', "Ma'lumot mavjud emas");
            message('#priorityStackedChart', "Ma'lumot mavjud emas");
            return;
        }
        var xaxis = { categories: d.priorityCategories, labels: { style: { colors: d.priorityColors } } };

        render('#priorityBarChart', {
            chart: { type: 'bar', height: 350 },
            plotOptions: { bar: { horizontal: false, columnWidth: '55%', endingShape: 'rounded' } },
            dataLabels: { enabled: false },
            stroke: { show: true, width: 2, colors: ['transparent'] },
            series: [{ name: 'Jami', data: d.priorityTotal }, { name: 'Bajarildi', data: d.priorityDone }],
            xaxis: xaxis,
            yaxis: { title: { text: 'Vazifalar soni' } },
            fill: { opacity: 1 },
            colors: d.priorityColors,
            legend: { position: 'top' }
        });

        render('#priorityStackedChart', {
            chart: { type: 'bar', height: 400, stacked: true, stackType: '100%' },
            plotOptions: { bar: { horizontal: false } },
            series: [
                { name: 'Rejada', data: d.priorityTodo },
                { name: 'Jarayonda', data: d.priorityInProgress },
                { name: 'Bajarildi', data: d.priorityDone }
            ],
            xaxis: xaxis,
            colors: ['#6C757D', '#FFC107', '#28A745'],
            legend: { position: 'top' },
            fill: { opacity: 1 }
        });
    }

    function overdueChart(d) {
        if (!d.overdueCounts.length) return message('#overdueTasksChart', "Muddatidan o'tgan vazifalar yo'q");
        render('#overdueTasksChart', {
            chart: { type: 'bar', height: 400 },
            plotOptions: { bar: { horizontal: true, borderRadius: 4 } },
            dataLabels: { enabled: true },
            series: [{ name: "Muddatidan o'tgan vazifalar", data: d.overdueCounts }],
            xaxis: { categories: d.overdueProjects },
            colors: ['#DC3545'],
            legend: { position: 'top' },
            fill: { opacity: 1 }
        });
    }

    function employeeChart(d) {
        if (!d.employeeNames.length) return message('#employeeRatingChart', "Xodimlar reytingi ma'lumotlari yo'q");

        var onTime = d.employeeOnTime, overdue = d.employeeOverdue;
        var hasOnTime = onTime.some(function (v) { return v > 0; });

        render('#employeeRatingChart', {
            chart: { type: 'bar', height: d.employeeNames.length * 50 + 100, toolbar: { show: true } },
            plotOptions: { bar: { horizontal: true, borderRadius: 4, dataLabels: { position: 'top' } } },
            dataLabels: {
                enabled: true,
                formatter: function (val, opts) {
                    return hasOnTime
                        ? onTime[opts.dataPointIndex] + ' ta muddatida bajarilgan'
                        : overdue[opts.dataPointIndex] + " ta muddati o'tgan";
                },
                offsetX: 0,
                offsetY: -5,
                style: { fontSize: '12px', colors: ['#333'] }
            },
            series: [{
                name: hasOnTime ? 'Muddatida bajarilgan' : "Muddati o'tgan",
                data: hasOnTime ? onTime : overdue
            }],
            xaxis: {
                categories: d.employeeNames,
                title: { text: hasOnTime ? 'Muddatida bajarilgan vazifalar soni' : "Muddati o'tgan vazifalar soni" }
            },
            yaxis: { title: { text: 'Xodimlar' } },
            colors: hasOnTime ? ['#28A745'] : ['#DC3545'],
            legend: { position: 'top' },
            fill: { opacity: 1 },
            tooltip: {
                y: {
                    formatter: function (val, opts) {
                        return hasOnTime
                            ? 'Muddatida bajarilgan: ' + val + " ta<br/>Muddati o'tgan: " + overdue[opts.dataPointIndex] + ' ta'
                            : "Muddati o'tgan: " + val + ' ta';
                    }
                }
            }
        });
    }

    function init() {
        var dataEl = document.getElementById('taskDashData');
        if (!dataEl) return;
        if (typeof window.ApexCharts === 'undefined') {
            ['#statusPieChart', '#priorityBarChart', '#priorityStackedChart', '#overdueTasksChart', '#employeeRatingChart']
                .forEach(function (s) { message(s, 'ApexCharts kutubxonasi yuklanmagan!', 'text-danger'); });
            return;
        }
        var d = JSON.parse(dataEl.textContent);
        statusChart(d);
        priorityCharts(d);
        overdueChart(d);
        employeeChart(d);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window, document);
