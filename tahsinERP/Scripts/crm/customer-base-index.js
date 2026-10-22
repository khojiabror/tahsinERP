/*
 * Xaridorlar bazasi (CustomerBase/Index) — filtr, AJAX o'chirish, DataTable (CRM-D21 / CRM-M03).
 *
 * Sozlamalar: <script type="application/json" id="customerBaseIndexConfig">{ indexUrl, deleteUrl }</script>
 * O'chirish tugmasi: <button class="js-delete-customer-base" data-id=".." data-name="..">
 * Filtr: #productTypeFilter (change).
 */
(function () {
    'use strict';

    var config = {};

    function readConfig() {
        var el = document.getElementById('customerBaseIndexConfig');
        if (!el) return {};
        try {
            return JSON.parse(el.textContent || el.innerText || '{}') || {};
        } catch (e) {
            return {};
        }
    }

    function refreshDataTable() {
        var productType = document.getElementById('productTypeFilter').value;
        window.location.href = config.indexUrl + '?productType=' + (productType || '');
    }

    function deleteCustomerBase($, id, name) {
        if (!confirm((window.erpT ? erpT('Js_Crm_ConfirmDeleteNamed', [name]) : '"' + name + '" ni o\'chirishni tasdiqlaysizmi?'))) {
            return;
        }

        var token = $('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: config.deleteUrl,
            type: 'POST',
            data: { id: id, __RequestVerificationToken: token },
            success: function (response) {
                if (response.success) {
                    location.reload();
                } else {
                    alert((window.erpT ? erpT('Js_Crm_ErrorPrefix', [response.message]) : 'Xatolik: ' + response.message));
                }
            },
            error: function () {
                alert((window.erpT ? erpT('Js_Crm_ServerError') : 'Server xatoligi yuz berdi'));
            }
        });
    }

    function initTable($) {
        $('#customerBaseTable').DataTable({
            "pageLength": 25,
            "lengthMenu": [[10, 25, 50, 100, -1], [10, 25, 50, 100, (window.erpT ? erpT('Js_Crm_All') : 'Barchasi')]],
            "order": [[3, "desc"]],
            "columnDefs": [
                { "type": "date", "targets": 3 }
            ],
            "scrollX": true,
            dom: 'lBfrtip',
            buttons: erpDataTable.buttons({ fileName: (window.erpT ? erpT('Js_Crm_Customers') : 'Xaridorlar'), pdfTitle: (window.erpT ? erpT('Js_Crm_Customers') : 'Xaridorlar') })
        });
    }

    function init() {
        var $ = window.jQuery;
        config = readConfig();

        $(document).on('change', '#productTypeFilter', refreshDataTable);

        // Delegatsiya: DataTable sahifalaganda ham ishlaydi
        $(document).on('click', '.js-delete-customer-base', function () {
            var $btn = $(this);
            deleteCustomerBase($, $btn.attr('data-id'), $btn.attr('data-name'));
        });

        initTable($);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
