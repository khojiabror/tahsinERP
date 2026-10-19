/*
 * SRM kirim (Incomes) va chiqim (Expenses) formalari: Create va Edit'da bir xil bo'lgan qator funksiyalari
 * (audit "Duplicates map" — Create/Edit). View'larda yupqa o'ramalar qoladi, xatti-harakat o'zgarmaydi.
 * Talab: srm-doc-lines.js (escapeHtml) avval yuklangan bo'lishi kerak.
 */
(function (window, $) {
    'use strict';

    var escapeHtml = window.srmDocLines.escapeHtml;

    /**
     * Mahsulot <select>'i uchun variantlar: bo'sh + (bo'lsa) faqat tanlangan mahsulot. Ro'yxat select2 orqali
     * serverdan qidiriladi (/ProductLookup/Search, SRM-P05) — butun katalog sahifaga JSON qilib yozilmaydi.
     */
    function incomeProductOptions(selectedId, pno, name) {
        var options = '<option value="">---Tanlang---</option>';
        if (selectedId) {
            options += '<option value="' + escapeHtml(selectedId) + '" data-name="' + escapeHtml(name || '') +
                '" data-pno="' + escapeHtml(pno || '') + '" selected>' +
                escapeHtml((pno ? pno + ' - ' : '') + (name || '')) +
                '</option>';
        }
        return options;
    }

    function dlocOptions(dlocLocations, selectedId) {
        var options = '<option value="">---DLOC---</option>';
        (dlocLocations || []).forEach(function (loc) {
            var id = loc.ID || loc.id;
            var code = loc.LocationCode || loc.locationCode || '';
            var selected = selectedId && String(selectedId) === String(id) ? 'selected' : '';
            options += '<option value="' + escapeHtml(String(id)) + '" ' + selected + '>' + escapeHtml(code) + '</option>';
        });
        return options;
    }

    function syncIncomeProduct($row) {
        var $selected = $row.find('.product-select option:selected');
        var name = $selected.attr('data-name');
        var pno = $selected.attr('data-pno');

        // Serverdan qidirib tanlangan mahsulot: variant matni "PNo - Nomi"
        if (name === undefined && $selected.val()) {
            var text = $selected.text() || '';
            var sep = text.indexOf(' - ');
            pno = sep >= 0 ? text.substring(0, sep) : text;
            name = sep >= 0 ? text.substring(sep + 3) : '';
        }

        if (!name) {
            name = $row.find('.product-name').val() || $row.find('.product-name-display').val() || '';
        }
        if (!pno) {
            pno = $row.find('.product-pno').val() || '';
        }

        $row.find('.product-name-display').val(name);
        $row.find('.product-name').val(name);
        $row.find('.product-pno').val(pno);
    }

    function recalcIncomeRow($row) {
        var amount = parseFloat($row.find('.amount-input').val()) || 0;
        var price = parseFloat($row.find('.price-input').val()) || 0;
        var vat = parseFloat($row.find('.vat-input').val());
        if (isNaN(vat)) vat = 0;
        vat = Math.max(0, Math.min(100, vat));
        var base = amount * price;
        var total = base * (1 + vat / 100);
        $row.find('.total-price').val(total.toFixed(2));
    }

    function expenseProductOptions(availableProducts, selectedId) {
        var options = '<option value="">---Tanlang---</option>';
        availableProducts.forEach(function (product) {
            var selected = product.ProductID == selectedId ? 'selected' : '';
            options += '<option value="' + escapeHtml(product.ProductID) + '" ' + selected +
                ' data-pno="' + escapeHtml(product.ProductPNo) + '"' +
                ' data-name="' + escapeHtml(product.ProductName) + '"' +
                ' data-stock="' + escapeHtml(product.AvailableStock) + '"' +
                ' data-unitid="' + escapeHtml(product.UnitID) + '"' +
                ' data-unitname="' + escapeHtml(product.UnitName) + '">' +
                escapeHtml(product.ProductPNo + ' - ' + product.ProductName + ' (Mavjud: ' + product.AvailableStock + ')') +
                '</option>';
        });
        return options;
    }

    function syncExpenseProduct($row) {
        var $selected = $row.find('.product-select option:selected');
        var pno = $selected.data('pno') || '';
        var name = $selected.data('name') || '';
        var stock = $selected.data('stock') || 0;
        var unitId = $selected.data('unitid') || '';

        $row.find('.product-name-display').val(name);
        $row.find('.product-name').val(name);
        $row.find('.product-pno').val(pno);
        $row.find('.available-stock').val(stock);
        $row.find('.amount-input').attr('max', stock);

        if (unitId) {
            $row.find('.unit-select').val(unitId).trigger('change');
        }
    }

    function refreshExpenseProductSelects(buildProductOptions, syncProductFields) {
        $('#expenseProductsTable tbody tr').each(function () {
            var $row = $(this);
            var $select = $row.find('.product-select');
            var selectedId = $select.val();

            if ($select.hasClass('select2-hidden-accessible')) {
                $select.select2('destroy');
            }

            $select.html(buildProductOptions(selectedId));

            if (typeof $.fn.select2 !== 'undefined') {
                $select.select2();
            }

            syncProductFields($row);
        });
    }

    window.srmIncomeForm = {
        productOptions: incomeProductOptions,
        dlocOptions: dlocOptions,
        syncProduct: syncIncomeProduct,
        recalcRow: recalcIncomeRow
    };

    window.srmExpenseForm = {
        productOptions: expenseProductOptions,
        syncProduct: syncExpenseProduct,
        refreshProductSelects: refreshExpenseProductSelects
    };
})(window, window.jQuery);
