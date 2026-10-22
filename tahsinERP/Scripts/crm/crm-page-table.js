/*
 * CRM ro'yxat jadvallari (CRM-D18). Sahifalash serverda (_Pager), DataTables faqat joriy sahifa
 * ichida qidiradi va saralaydi — avval har Index view'da bir xil 30 qatorlik init takrorlanardi.
 *
 * Foydalanish: <table data-crm-table data-lock-last="true" data-searching="false">
 *   data-lock-last  — oxirgi ustun (amallar) saralanmaydi
 *   data-searching  — "false" bo'lsa qidiruv maydoni ko'rsatilmaydi (qidiruv serverdagi formada)
 * Fayl layout skriptlaridan oldin ulanishi mumkin — jQuery/DataTables yuklanguncha kutadi.
 */
(function () {
    'use strict';

    function initTables() {
        if (typeof window.jQuery === 'undefined' || typeof window.jQuery.fn.DataTable === 'undefined') {
            setTimeout(initTables, 100);
            return;
        }
        var $ = window.jQuery;

        $('table[data-crm-table]').each(function () {
            var $table = $(this);
            if ($.fn.DataTable.isDataTable(this)) {
                $table.DataTable().destroy();
            }

            var searching = $table.attr('data-searching') !== 'false';
            // Standart til layout'da (erpDataTable); faqat qidiruv yozuvi — "sahifada" (sahifalash serverda)
            var language = {};
            if (searching) {
                language.search = (window.erpT ? erpT('Js_Crm_SearchOnPage') : 'Sahifada qidirish:');
            }

            $table.DataTable({
                language: language,
                paging: false,
                info: false,
                searching: searching,
                order: [],
                columnDefs: $table.attr('data-lock-last') === 'true' ? [{ orderable: false, targets: -1 }] : [],
                dom: searching ? 'frt' : 'rt',
                scrollX: true
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initTables);
    } else {
        initTables();
    }
})();
