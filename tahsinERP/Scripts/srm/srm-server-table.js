/*
 * SRM ombor ro'yxatlari uchun server-side DataTables (Incomes, Expenses).
 * Sahifalash, saralash va qidiruv serverda (SQL) — brauzerga butun jadval yuklanmaydi.
 * Filtr formasi (ombor, status, sana) har so'rovga qo'shiladi; Excel tugmasi filtr bo'yicha
 * BARCHA yozuvlarni server eksport amali orqali yuklaydi.
 *
 *   srmServerTable('#incomesTable', {
 *       dataUrl: '/Incomes/IndexData', exportUrl: '/Incomes/ExportExcel',
 *       filterForm: '#incomesFilter', order: [[5, 'desc']], columns: [...]
 *   });
 */
(function (window) {
    'use strict';

    window.srmEsc = function (value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };

    window.srmServerTable = function (selector, options) {
        var $ = window.jQuery;
        var $form = $(options.filterForm);

        function filterParams() {
            var params = {};
            $.each($form.serializeArray(), function (_, f) { params[f.name] = f.value; });
            return params;
        }

        var table;
        var buttons = [];
        if (options.exportUrl) {
            buttons.push({
                text: '<i class="bx bxs-file-excel"></i> Excel',
                titleAttr: window.erpT ? erpT('Js_Srm_ExportFilteredExcel') : "Filtr bo'yicha barcha yozuvlarni Excelga yuklash",
                className: 'btn btn-success mr-1',
                action: function () {
                    var params = filterParams();
                    params.q = table ? table.search() : '';
                    window.location.href = options.exportUrl + '?' + $.param(params);
                }
            });
        }

        table = $(selector).DataTable({
            processing: true,
            serverSide: true,
            searchDelay: 400,
            pageLength: 25,
            lengthMenu: [[10, 25, 50, 100, 500], [10, 25, 50, 100, 500]],
            order: options.order || [[0, 'desc']],
            scrollX: true,
            dom: buttons.length ? 'lBfrtip' : 'lfrtip',
            buttons: buttons,
            ajax: {
                url: options.dataUrl,
                type: 'GET',
                // Server faqat draw/start/length/search/order[0] ni o'qiydi (DataTablesPageRequest). DataTables'ning
                // to'liq so'rovi (columns[i][...]) 9+ ustunda IIS query string chegarasidan (2048) oshib 404 berardi.
                data: function (d) {
                    var p = { draw: d.draw, start: d.start, length: d.length, 'search[value]': d.search ? d.search.value : '' };
                    if (d.order && d.order.length) { p['order[0][column]'] = d.order[0].column; p['order[0][dir]'] = d.order[0].dir; }
                    return $.extend(p, filterParams());
                },
                error: function (xhr) {
                    var msg = xhr.status === 403
                        ? (window.erpT ? erpT('Js_Srm_NoAccess') : "Ruxsat yo'q.")
                        : (window.erpT ? erpT('Js_Srm_ListLoadError') : "Ro'yxatni yuklashda xatolik.");
                    $(selector).find('tbody').html('<tr><td colspan="20" class="text-danger">' + msg + '</td></tr>');
                }
            },
            columns: options.columns
            // Til matnlari — layout'dagi umumiy DataTables standarti (joriy tilda)
        });

        return table;
    };
})(window);
