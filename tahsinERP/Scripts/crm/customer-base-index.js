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
        if (!confirm('"' + name + '" ni o\'chirishni tasdiqlaysizmi?')) {
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
                    alert('Xatolik: ' + response.message);
                }
            },
            error: function () {
                alert('Server xatoligi yuz berdi');
            }
        });
    }

    function initTable($) {
        $('#customerBaseTable').DataTable({
            "language": {
                "search": "Qidirish:",
                "lengthMenu": "_MENU_ ta ko'rsatish",
                "info": "_TOTAL_ ta yozuvdan _START_ dan _END_ gacha",
                "infoEmpty": "Yozuvlar yo'q",
                "infoFiltered": "(jami _MAX_ ta yozuvdan filtrlangan)",
                "paginate": {
                    "first": "Birinchi",
                    "last": "Oxirgi",
                    "next": "Keyingi",
                    "previous": "Oldingi"
                },
                "zeroRecords": "Ma'lumot topilmadi"
            },
            "pageLength": 25,
            "lengthMenu": [[10, 25, 50, 100, -1], [10, 25, 50, 100, "Barchasi"]],
            "order": [[3, "desc"]],
            "columnDefs": [
                { "type": "date", "targets": 3 }
            ],
            "scrollX": true,
            dom: 'lBfrtip',
            buttons: erpDataTable.buttons({ fileName: 'Xaridorlar', pdfTitle: 'Xaridorlar' })
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
