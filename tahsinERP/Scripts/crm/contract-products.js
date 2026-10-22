/*
 * Sotuv shartnomasi mahsulot qatorlari (SContract Create / Edit) — CRM-D21 / CRM-M03.
 *
 * Sozlamalar sahifadagi JSON blokdan olinadi:
 *   <script type="application/json" id="contractProductsConfig">{ "mode": "create" | "edit", ... }</script>
 *   create: products, units, initialRows ([{ ProductID, Amount, UnitID, PiecePrice, VatPercent }])
 *   edit:   products, units, startIndex (serverda chizilgan qatorlar soni)
 * products/units: [{ Value: "12", Text: "..." }]
 *
 * Mahsulot tanlovi — Scripts/app/product-select.js (katalog har qatorga yozilmaydi),
 * htmlEscape — Scripts/app/indexed-rows.js. Ikkalasi ham shu fayldan oldin ulanadi.
 */
(function () {
    'use strict';

    function esc(value) {
        return window.htmlEscape ? window.htmlEscape(value) : String(value == null ? '' : value);
    }

    function readConfig() {
        var el = document.getElementById('contractProductsConfig');
        if (!el) return null;
        try {
            return JSON.parse(el.textContent || el.innerText || '{}') || {};
        } catch (e) {
            return null;
        }
    }

    function productOptions(products, selectedId) {
        if (window.ProductSelect) return window.ProductSelect.optionsHtml(products, selectedId);
        var html = '<option value="">' + (window.erpT ? erpT('Js_Crm_SelectDashed') : '---Tanlang---') + '</option>';
        for (var i = 0; i < products.length; i++) {
            var selected = products[i].Value == selectedId ? ' selected' : '';
            html += '<option value="' + esc(products[i].Value) + '"' + selected + '>' + esc(products[i].Text) + '</option>';
        }
        return html;
    }

    function attachProductSelect($, $select, products) {
        if (window.ProductSelect) {
            window.ProductSelect.attach($select, products);
        } else if (typeof $.fn.select2 !== 'undefined') {
            $select.select2();
        }
    }

    function unitOptions(units, selectedId, placeholder) {
        var html = '<option value="">' + placeholder + '</option>';
        for (var i = 0; i < units.length; i++) {
            var selected = (selectedId !== undefined && units[i].Value == selectedId) ? ' selected' : '';
            html += '<option value="' + esc(units[i].Value) + '"' + selected + '>' + esc(units[i].Text) + '</option>';
        }
        return html;
    }

    function rowTotal(quantity, price, vatPercent) {
        var baseTotal = quantity * price;
        return baseTotal + (baseTotal * vatPercent / 100);
    }

    // ---- Create: qatorlar JS'da quriladi, submit oldidan 0..n-1 ga qayta raqamlanadi ----
    function initCreate($, cfg) {
        var products = cfg.products || [];
        var units = cfg.units || [];
        var productRowIndex = 0;
        var $tbody = $('#contractProductsTable tbody');

        if (typeof $.fn.select2 !== 'undefined') {
            $('.select2').select2();
        }

        function updateRowTotal($row) {
            var quantity = parseFloat($row.find('.quantity-input').val()) || 0;
            var price = parseFloat($row.find('.price-input').val()) || 0;
            var vatPercent = parseFloat($row.find('.vat-input').val()) || 0;
            $row.find('.total-with-vat').val(rowTotal(quantity, price, vatPercent).toFixed(2));
        }

        function updateTotal() {
            var total = 0;
            $('.total-with-vat').each(function () {
                total += parseFloat($(this).val()) || 0;
            });
            $('#totalAmount').text(total.toFixed(2));
            $('#Amount').val(Math.round(total));
        }

        function addProductRow(productId, quantity, unitId, price, vatPercent) {
            var i = productRowIndex;
            var row = '<tr data-row-index="' + i + '">' +
                '<td>' +
                    '<select name="ProductList[' + i + '].ProductID" class="form-control product-select" required>' +
                    productOptions(products, productId) +
                    '</select>' +
                    '<input type="hidden" name="ProductList[' + i + '].ID" value="0" />' +
                '</td>' +
                '<td><input type="number" name="ProductList[' + i + '].Amount" class="form-control quantity-input" value="' + esc(quantity || 0) + '" step="0.01" min="0.01" required /></td>' +
                '<td>' +
                    '<select name="ProductList[' + i + '].UnitID" class="form-control unit-select select2" required>' +
                    unitOptions(units, unitId, (window.erpT ? erpT('Js_Crm_SelectDashed') : '---Tanlang---')) +
                    '</select>' +
                '</td>' +
                '<td><input type="number" name="ProductList[' + i + '].PiecePrice" class="form-control price-input" value="' + esc(price || 0) + '" step="0.01" min="0" required /></td>' +
                '<td><input type="number" name="ProductList[' + i + '].VatPercent" class="form-control vat-input" value="' + esc(vatPercent == null ? 12 : vatPercent) + '" step="0.01" min="0" max="100" /></td>' +
                '<td><input type="number" class="form-control total-with-vat" value="0" readonly /></td>' +
                '<td><button type="button" class="btn btn-sm btn-danger remove-row"><i class="bx bx-trash"></i></button></td>' +
                '</tr>';

            var $row = $(row);
            $tbody.append($row);

            attachProductSelect($, $row.find('select.product-select'), products);
            if (typeof $.fn.select2 !== 'undefined') {
                $row.find('select.unit-select.select2').select2();
            }

            productRowIndex++;
            updateRowTotal($row);
            updateTotal();
        }

        function reindexRows() {
            $tbody.find('tr').each(function (index) {
                $(this).attr('data-row-index', index);
                $(this).find('select, input').each(function () {
                    var name = $(this).attr('name');
                    if (name) {
                        $(this).attr('name', name.replace(/\[(\d+)\]/, '[' + index + ']'));
                    }
                });
            });
            productRowIndex = $tbody.find('tr').length;
        }

        // Xato bilan qaytganda kiritilgan qatorlar tiklanadi
        var initialRows = cfg.initialRows || [];
        for (var r = 0; r < initialRows.length; r++) {
            var item = initialRows[r];
            addProductRow(item.ProductID, item.Amount, item.UnitID, item.PiecePrice, item.VatPercent);
        }

        $(document).on('click', '#addRowBtn', function (e) {
            e.preventDefault();
            e.stopPropagation();
            addProductRow(null, 0, null, 0, null);
            return false;
        });

        $(document).on('click', '.remove-row', function () {
            $(this).closest('tr').remove();
            reindexRows();
            updateTotal();
        });

        $(document).on('input', '.quantity-input, .price-input, .vat-input', function () {
            updateRowTotal($(this).closest('tr'));
            updateTotal();
        });

        $('#contractForm').submit(function (e) {
            reindexRows();
            if ($tbody.find('tr').length === 0) {
                e.preventDefault();
                alert((window.erpT ? erpT('Js_Crm_AtLeastOneProduct') : 'Kamida bitta mahsulot qo\'shilishi kerak'));
                return false;
            }
        });
    }

    // ---- Edit: mavjud qatorlar serverda chiziladi; indekslar qayta raqamlanmaydi
    // (server ProductList[i].* ni kalit bo'yicha o'qiydi, bo'shliq muammo emas) ----
    function initEdit($, cfg) {
        var products = cfg.products || [];
        var units = cfg.units || [];
        var productIndex = parseInt(cfg.startIndex, 10) || 0;
        var $tbody = $('#productTableBody');

        function updateRowTotal($row) {
            var price = parseFloat($row.find('.product-price-edit').val()) || 0;
            var amount = parseFloat($row.find('.product-amount-edit').val()) || 0;
            var vatPercent = parseFloat($row.find('.vat-input').val()) || 0;
            $row.find('.total-with-vat').val(rowTotal(amount, price, vatPercent).toFixed(2));
        }

        function calculateTotalAmount() {
            var total = 0;
            $tbody.find('tr').each(function () {
                var $row = $(this);
                updateRowTotal($row);
                total += parseFloat($row.find('.total-with-vat').val()) || 0;
            });
            $('#Amount').val(Math.round(total));
        }

        function addProductRow() {
            var i = productIndex;
            var newRow = '<tr data-index="' + i + '">' +
                '<td>' +
                    '<input type="hidden" name="ProductList[' + i + '].ID" value="0" />' +
                    '<select name="ProductList[' + i + '].ProductID" class="form-control product-select" required>' +
                    productOptions(products, null) +
                    '</select>' +
                '</td>' +
                '<td><input type="number" name="ProductList[' + i + '].Amount" class="form-control product-amount-edit" min="1" required /></td>' +
                '<td>' +
                    '<select name="ProductList[' + i + '].UnitID" class="form-control select2" required>' +
                    unitOptions(units, undefined, (window.erpT ? erpT('Js_Crm_SelectDots') : 'Tanlang...')) +
                    '</select>' +
                '</td>' +
                '<td><input type="number" name="ProductList[' + i + '].PiecePrice" class="form-control product-price-edit" step="0.01" min="0" required /></td>' +
                '<td><input type="number" name="ProductList[' + i + '].VatPercent" class="form-control vat-input" value="12" step="0.01" min="0" max="100" /></td>' +
                '<td><input type="number" class="form-control total-with-vat" value="0" readonly /></td>' +
                '<td>' +
                    '<button type="button" class="btn btn-sm btn-danger remove-product-row">' +
                    '<i class="bx bx-trash"></i>' +
                    '</button>' +
                '</td>' +
                '</tr>';

            var $row = $(newRow);
            $tbody.append($row);
            productIndex++;

            attachProductSelect($, $row.find('select.product-select'), products);
            if (typeof $.fn.select2 !== 'undefined') {
                $row.find('select.select2').select2();
            }

            calculateTotalAmount();
        }

        $(document).on('input change', '.product-price-edit, .product-amount-edit, .vat-input', function () {
            calculateTotalAmount();
        });

        $('#addProductBtn').on('click', function () {
            addProductRow();
        });

        $(document).on('click', '.remove-product-row', function () {
            $(this).closest('tr').remove();
            calculateTotalAmount();
        });

        calculateTotalAmount();
    }

    function start(cfg) {
        // Create sahifasida fayl layout'dagi jQuery'dan oldin ulanadi — kutib turamiz
        if (typeof window.jQuery === 'undefined') {
            setTimeout(function () { start(cfg); }, 100);
            return;
        }
        var $ = window.jQuery;
        $(function () {
            if (cfg.mode === 'edit') {
                initEdit($, cfg);
            } else {
                initCreate($, cfg);
            }
        });
    }

    function boot() {
        var cfg = readConfig();
        if (cfg) start(cfg);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
