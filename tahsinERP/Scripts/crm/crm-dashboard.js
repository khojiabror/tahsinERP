/*
 * CRM Dashboard — ApexCharts grafiklari, DataTable'lar, debitorlik qatorlarini ochish (CRM-D21 / CRM-M03).
 *
 * Grafik ma'lumotlari: <script type="application/json" id="crmDashboardConfig">
 *   { contractsByMonth, ordersByMonth, orderFulfillment, contractStatus, topCustomers }
 * har biri { labels: [...], series: [...] } (raqamlar JSON son sifatida).
 * apexcharts.min.js shu fayldan oldin ulanadi.
 */
(function () {
    'use strict';

    var NO_DATA = '<p class="text-center text-muted">' + (window.erpT ? erpT('Js_Crm_NoData') : 'Ma\'lumot yo\'q') + '</p>';

    function readConfig() {
        var el = document.getElementById('crmDashboardConfig');
        if (!el) return {};
        try {
            return JSON.parse(el.textContent || el.innerText || '{}') || {};
        } catch (e) {
            return {};
        }
    }

    // Bo'sh ro'yxatda avvalgidek [0] — grafik shartlari o'zgarmasin
    function chartData(item) {
        item = item || {};
        var labels = item.labels || [];
        var series = item.series && item.series.length ? item.series : [0];
        return { labels: labels, series: series };
    }

    function anyPositive(series) {
        for (var i = 0; i < series.length; i++) {
            if (series[i] > 0) return true;
        }
        return false;
    }

    function showNoData(selector) {
        document.querySelector(selector).innerHTML = NO_DATA;
    }

    function formatSumma(val) {
        var n = Number(val);
        if (isNaN(n)) return val;
        var parts = n.toFixed(2).split('.');
        parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
        return parts.join(',') + ' ' + (window.erpT ? erpT('Js_Crm_Sum') : 'so\'m');
    }

    function renderCharts(cfg) {
        var contracts = chartData(cfg.contractsByMonth);
        if (contracts.labels.length && contracts.series.length) {
            new ApexCharts(document.querySelector('#contractsByMonthChart'), {
                chart: { type: 'bar', height: 320 },
                series: [{ name: (window.erpT ? erpT('Js_Crm_Contracts') : 'Shartnomalar'), data: contracts.series }],
                xaxis: { categories: contracts.labels },
                plotOptions: { bar: { borderRadius: 4, columnWidth: '60%' } },
                colors: ['#7367F0'],
                dataLabels: { enabled: false }
            }).render();
        } else {
            showNoData('#contractsByMonthChart');
        }

        var orders = chartData(cfg.ordersByMonth);
        if (orders.labels.length && orders.series.length) {
            new ApexCharts(document.querySelector('#ordersByMonthChart'), {
                chart: { type: 'line', height: 320 },
                series: [{ name: (window.erpT ? erpT('Js_Crm_Orders') : 'Buyurtmalar'), data: orders.series }],
                xaxis: { categories: orders.labels },
                stroke: { curve: 'smooth', width: 2 },
                colors: ['#28a745'],
                dataLabels: { enabled: false }
            }).render();
        } else {
            showNoData('#ordersByMonthChart');
        }

        var fulfillment = chartData(cfg.orderFulfillment);
        if (fulfillment.series.length && anyPositive(fulfillment.series)) {
            new ApexCharts(document.querySelector('#orderFulfillmentChart'), {
                chart: { type: 'donut', height: 320 },
                labels: fulfillment.labels,
                series: fulfillment.series,
                colors: ['#28a745', '#ffc107'],
                legend: { position: 'bottom' }
            }).render();
        } else {
            showNoData('#orderFulfillmentChart');
        }

        var status = chartData(cfg.contractStatus);
        if (status.series.length && anyPositive(status.series)) {
            new ApexCharts(document.querySelector('#contractStatusChart'), {
                chart: { type: 'donut', height: 320 },
                labels: status.labels,
                series: status.series,
                colors: ['#dc3545', '#28a745', '#17a2b8'],
                legend: { position: 'bottom' }
            }).render();
        } else {
            showNoData('#contractStatusChart');
        }

        var top = chartData(cfg.topCustomers);
        if (top.labels.length && top.series.length) {
            new ApexCharts(document.querySelector('#topCustomersChart'), {
                chart: { type: 'bar', height: 350, toolbar: { show: false } },
                plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '65%' } },
                series: [{ name: (window.erpT ? erpT('Js_Crm_Amount') : 'Summa'), data: top.series }],
                xaxis: { categories: top.labels },
                dataLabels: {
                    enabled: true,
                    formatter: function (val) { return formatSumma(val); }
                },
                colors: ['#5e72e4']
            }).render();
        } else {
            showNoData('#topCustomersChart');
        }
    }

    function initTables($) {
        if (!$.fn.DataTable) return;
        var crmDtOptions = {
            pageLength: 25,
            lengthMenu: [[25, 50, 100, -1], ['25', '50', '100', (window.erpT ? erpT('Js_Crm_All') : 'Barchasi')]],
            order: [],
            dom: '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6"f>>rtip'
        };
        $('.crm-dashboard-datatable').each(function () {
            var $t = $(this);
            var hasDataRows = $t.find('tbody tr').length > 0 && $t.find('tbody tr td[colspan]').length === 0;
            if (hasDataRows) $t.DataTable(crmDtOptions);
        });
    }

    function initReceivables($) {
        $('#tblCustomerReceivables').on('click', '.receivable-customer-row', function () {
            var customerId = $(this).data('customer');
            var $icon = $(this).find('.receivable-toggle-icon');
            var $children = $('#tblCustomerReceivables').find('.receivable-contract-row[data-parent="' + customerId + '"]');
            var isHidden = $children.first().hasClass('d-none');
            if (isHidden) {
                $children.removeClass('d-none');
                $icon.removeClass('bx-chevron-right').addClass('bx-chevron-down');
            } else {
                $children.addClass('d-none');
                $icon.removeClass('bx-chevron-down').addClass('bx-chevron-right');
            }
        });
    }

    function init() {
        var $ = window.jQuery;

        // ApexCharts yuklanmasa — avvalgidek qolgan qismlar ham ishga tushmaydi
        if (typeof ApexCharts === 'undefined') {
            $('#contractsByMonthChart, #ordersByMonthChart, #orderFulfillmentChart, #contractStatusChart, #topCustomersChart')
                .html('<p class="text-center text-muted">' + (window.erpT ? erpT('Js_Crm_ApexNotLoaded') : 'ApexCharts yuklanmadi') + '</p>');
            return;
        }

        renderCharts(readConfig());
        initTables($);
        initReceivables($);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
