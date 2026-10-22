/*
 * Tasks Dashboard grafiklari (ApexCharts). Ma'lumot sahifadagi
 * <script type="application/json" id="taskDashData"> blokidan olinadi — server uni bir marta
 * Json.Encode qiladi (avval 470 qatorli Razor-in-JS edi — MNT-02, TSK-B08).
 */
(function (window, document) {
    'use strict';

    // Joriy tildagi matn (window.erpT, _I18nScript); yuklanmagan bo'lsa — o'zbekcha
    function t(key, fallback, args) {
        return window.erpT ? window.erpT(key, args) : fallback;
    }

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
            message(selector, t('Js_Task_ErrorPrefix', 'Xatolik:') + ' ' + String(e.message).replace(/</g, '&lt;'), 'text-danger');
        }
    }

    function statusChart(d) {
        if (!d.statusSeries.length) return message('#statusPieChart', t('Js_Task_NoData', "Ma'lumot mavjud emas"));
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
            message('#priorityBarChart', t('Js_Task_NoData', "Ma'lumot mavjud emas"));
            message('#priorityStackedChart', t('Js_Task_NoData', "Ma'lumot mavjud emas"));
            return;
        }
        var xaxis = { categories: d.priorityCategories, labels: { style: { colors: d.priorityColors } } };

        render('#priorityBarChart', {
            chart: { type: 'bar', height: 350 },
            plotOptions: { bar: { horizontal: false, columnWidth: '55%', endingShape: 'rounded' } },
            dataLabels: { enabled: false },
            stroke: { show: true, width: 2, colors: ['transparent'] },
            series: [{ name: t('Js_Task_Total', 'Jami'), data: d.priorityTotal }, { name: t('Js_Task_Done', 'Bajarildi'), data: d.priorityDone }],
            xaxis: xaxis,
            yaxis: { title: { text: t('Js_Task_TaskCount', 'Vazifalar soni') } },
            fill: { opacity: 1 },
            colors: d.priorityColors,
            legend: { position: 'top' }
        });

        render('#priorityStackedChart', {
            chart: { type: 'bar', height: 400, stacked: true, stackType: '100%' },
            plotOptions: { bar: { horizontal: false } },
            series: [
                { name: t('Js_Task_Todo', 'Rejada'), data: d.priorityTodo },
                { name: t('Js_Task_InProgress', 'Jarayonda'), data: d.priorityInProgress },
                { name: t('Js_Task_Done', 'Bajarildi'), data: d.priorityDone }
            ],
            xaxis: xaxis,
            colors: ['#6C757D', '#FFC107', '#28A745'],
            legend: { position: 'top' },
            fill: { opacity: 1 }
        });
    }

    function overdueChart(d) {
        if (!d.overdueCounts.length) return message('#overdueTasksChart', t('Js_Task_NoOverdue', "Muddatidan o'tgan vazifalar yo'q"));
        render('#overdueTasksChart', {
            chart: { type: 'bar', height: 400 },
            plotOptions: { bar: { horizontal: true, borderRadius: 4 } },
            dataLabels: { enabled: true },
            series: [{ name: t('Js_Task_OverdueTasks', "Muddatidan o'tgan vazifalar"), data: d.overdueCounts }],
            xaxis: { categories: d.overdueProjects },
            colors: ['#DC3545'],
            legend: { position: 'top' },
            fill: { opacity: 1 }
        });
    }

    function employeeChart(d) {
        if (!d.employeeNames.length) return message('#employeeRatingChart', t('Js_Task_NoRating', "Xodimlar reytingi ma'lumotlari yo'q"));

        var onTime = d.employeeOnTime, overdue = d.employeeOverdue;
        var hasOnTime = onTime.some(function (v) { return v > 0; });

        render('#employeeRatingChart', {
            chart: { type: 'bar', height: d.employeeNames.length * 50 + 100, toolbar: { show: true } },
            plotOptions: { bar: { horizontal: true, borderRadius: 4, dataLabels: { position: 'top' } } },
            dataLabels: {
                enabled: true,
                formatter: function (val, opts) {
                    return hasOnTime
                        ? t('Js_Task_OnTimeN', onTime[opts.dataPointIndex] + ' ta muddatida bajarilgan', [onTime[opts.dataPointIndex]])
                        : t('Js_Task_OverdueN', overdue[opts.dataPointIndex] + " ta muddati o'tgan", [overdue[opts.dataPointIndex]]);
                },
                offsetX: 0,
                offsetY: -5,
                style: { fontSize: '12px', colors: ['#333'] }
            },
            series: [{
                name: hasOnTime ? t('Js_Task_OnTime', 'Muddatida bajarilgan') : t('Js_Task_Overdue', "Muddati o'tgan"),
                data: hasOnTime ? onTime : overdue
            }],
            xaxis: {
                categories: d.employeeNames,
                title: { text: hasOnTime ? t('Js_Task_OnTimeCount', 'Muddatida bajarilgan vazifalar soni') : t('Js_Task_OverdueCount', "Muddati o'tgan vazifalar soni") }
            },
            yaxis: { title: { text: t('Js_Task_Employees', 'Xodimlar') } },
            colors: hasOnTime ? ['#28A745'] : ['#DC3545'],
            legend: { position: 'top' },
            fill: { opacity: 1 },
            tooltip: {
                y: {
                    formatter: function (val, opts) {
                        return hasOnTime
                            ? t('Js_Task_OnTimeTip', 'Muddatida bajarilgan: ' + val + ' ta', [val]) + '<br/>' +
                              t('Js_Task_OverdueTip', "Muddati o'tgan: " + overdue[opts.dataPointIndex] + ' ta', [overdue[opts.dataPointIndex]])
                            : t('Js_Task_OverdueTip', "Muddati o'tgan: " + val + ' ta', [val]);
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
                .forEach(function (s) { message(s, t('Js_Task_ApexMissing', 'ApexCharts kutubxonasi yuklanmagan!'), 'text-danger'); });
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
