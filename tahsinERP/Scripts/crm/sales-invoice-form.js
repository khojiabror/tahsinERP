/*
 * Sotuv invoysi formasi (Create / Edit) uchun umumiy skript (CRM-D19 / CRM-M03).
 *
 * Razor qiymatlari (URL'lar, model qiymatlari) view'dagi JSON blokdan o'qiladi:
 *   <script type="application/json" id="salesInvoiceFormConfig">{...}</script>
 *
 * Kalitlar:
 *   formId              — forma id (submit oldidan qatorlarni qayta raqamlash uchun)
 *   urls                — contracts, orders, price, currency, orderProducts (ixtiyoriy)
 *   resetCurrency       — mijoz/shartnoma ro'yxati yangilanganda valyutani tozalash (Create)
 *   stockHints          — mahsulot ostida "Mavjud: N" ko'rsatmasi (Create)
 *   initialOrderId      — boshlang'ich buyurtma; qatorlar undan to'ldiriladi (Create)
 *   hasPostedLines      — xato bilan qaytgan formada qatorlar bor, buyurtmadan to'ldirilmaydi
 */
(function () {
    'use strict';

    function readConfig() {
        var el = document.getElementById('salesInvoiceFormConfig');
        if (!el) return null;
        try {
            return JSON.parse(el.textContent || el.innerText || '{}');
        } catch (e) {
            return null;
        }
    }

    function resetSelect(id) {
        var sel = document.getElementById(id);
        if (sel) sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Select') : 'Tanlang') + '</option>';
    }

    function resetCurrencyValue() {
        var cur = document.getElementById('CurrencyID');
        if (cur) cur.value = '';
    }

    function init() {
        var cfg = readConfig();
        if (!cfg) return;
        var urls = cfg.urls || {};
        var lineIndex = document.querySelectorAll('#invoiceLinesTable tbody tr').length;
        var productStockMap = {};

        function loadContracts(customerId) {
            var sel = document.getElementById('ContractID');
            if (!sel) return;
            sel.disabled = true;
            sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Loading') : 'Yuklanmoqda...') + '</option>';
            fetch(urls.contracts + '?customerId=' + customerId)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Select') : 'Tanlang') + '</option>';
                    if (data && data.length) {
                        data.forEach(function (c) {
                            var opt = document.createElement('option');
                            opt.value = c.ID;
                            opt.textContent = c.ContractNo;
                            sel.appendChild(opt);
                        });
                    }
                    sel.disabled = false;
                    resetSelect('OrderID');
                    if (cfg.resetCurrency) resetCurrencyValue();
                })
                .catch(function () {
                    sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Crm_Error') : 'Xatolik') + '</option>';
                    sel.disabled = false;
                });
        }

        function loadOrders(contractId) {
            var sel = document.getElementById('OrderID');
            if (!sel) return;
            sel.disabled = true;
            sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Loading') : 'Yuklanmoqda...') + '</option>';
            fetch(urls.orders + '?contractId=' + contractId)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Select') : 'Tanlang') + '</option>';
                    if (data && data.length) {
                        data.forEach(function (o) {
                            var opt = document.createElement('option');
                            opt.value = o.ID;
                            opt.textContent = o.OrderNo;
                            sel.appendChild(opt);
                        });
                    }
                    sel.disabled = false;
                })
                .catch(function () {
                    sel.innerHTML = '<option value="">' + (window.erpT ? erpT('Js_Crm_Error') : 'Xatolik') + '</option>';
                    sel.disabled = false;
                });
            fetch(urls.currency + '?contractId=' + contractId)
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    var curSel = document.getElementById('CurrencyID');
                    if (curSel && d && d.CurrencyID) curSel.value = String(d.CurrencyID);
                });
        }

        function loadPriceForProduct(contractId, productId, priceInput, vatInput) {
            if (!contractId || !productId || !priceInput) return;
            fetch(urls.price + '?contractId=' + contractId + '&productId=' + productId)
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (d && d.PiecePrice != null) priceInput.value = parseFloat(d.PiecePrice).toFixed(2);
                    if (vatInput && d && d.VatRate != null) vatInput.value = parseFloat(d.VatRate).toFixed(2);
                    calculateTotals();
                });
        }

        var contractEl = document.getElementById('ContractID');
        var custEl = document.getElementById('CustomerID');
        if (custEl) {
            custEl.addEventListener('change', function () {
                var v = this.value;
                if (v) loadContracts(v);
                else {
                    resetSelect('ContractID');
                    resetSelect('OrderID');
                    if (cfg.resetCurrency) resetCurrencyValue();
                }
            });
        }
        if (contractEl) {
            contractEl.addEventListener('change', function () {
                var v = this.value;
                if (v) loadOrders(v);
                else resetSelect('OrderID');
            });
        }

        function parseAvailableFromText(text) {
            var match = text.match(/Mavjud:\s*([0-9]+(?:\.[0-9]+)?)/i);
            return match ? match[1] : null;
        }

        // Qoldiq mahsulot ro'yxati matnidan ("... Mavjud: N") olinadi
        function buildStockMap() {
            productStockMap = {};
            if (!cfg.stockHints) return;
            var template = document.getElementById('productSelectTemplate');
            if (!template) return;
            for (var i = 0; i < template.options.length; i++) {
                var opt = template.options[i];
                if (!opt.value) continue;
                var available = parseAvailableFromText(opt.textContent);
                if (available !== null) productStockMap[opt.value] = available;
            }
        }

        function updateStockHint(row) {
            if (!cfg.stockHints || !row) return;
            var sel = row.querySelector('.product-select');
            var hint = row.querySelector('.available-stock-hint');
            if (!sel || !hint) return;
            var available = productStockMap[sel.value];
            hint.textContent = available ? (window.erpT ? erpT('Js_Crm_Available', [available]) : ('Mavjud: ' + available)) : '';
        }

        function calculateTotals() {
            var totalAmount = 0, totalVAT = 0;
            var rows = document.querySelectorAll('#invoiceLinesTable tbody tr');
            for (var i = 0; i < rows.length; i++) {
                var r = rows[i];
                var qInp = r.querySelector('.qty-input'), pInp = r.querySelector('.price-input'), vInp = r.querySelector('.vat-input');
                var qty = parseFloat(qInp ? qInp.value : 0) || 0;
                var price = parseFloat(pInp ? pInp.value : 0) || 0;
                var vatRate = parseFloat(vInp ? vInp.value : 0) || 0;
                var lineAmount = qty * price;
                totalAmount += lineAmount;
                totalVAT += lineAmount * vatRate / 100;
            }
            var totalEl = document.getElementById('totalAmount');
            var vatEl = document.getElementById('totalVAT');
            var withVatEl = document.getElementById('totalWithVAT');
            if (totalEl) totalEl.textContent = totalAmount.toFixed(2);
            if (vatEl) vatEl.textContent = totalVAT.toFixed(2);
            if (withVatEl) withVatEl.textContent = (totalAmount + totalVAT).toFixed(2);
        }

        function addListeners(root) {
            (root || document).querySelectorAll('.qty-input, .price-input, .vat-input').forEach(function (el) {
                el.addEventListener('input', calculateTotals);
            });
        }

        var addBtn = document.getElementById('addLine');
        if (addBtn) {
            addBtn.addEventListener('click', function () {
                var table = document.getElementById('invoiceLinesTable');
                var productSelect = document.getElementById('productSelectTemplate');
                if (!table || !productSelect) return;

                var newSelect = productSelect.cloneNode(true);
                newSelect.name = 'lines[' + lineIndex + '].ProductID';
                newSelect.removeAttribute('id');
                newSelect.removeAttribute('disabled');
                newSelect.classList.remove('d-none');
                newSelect.required = true;
                newSelect.value = '';

                var newRow = document.createElement('tr');
                var td1 = document.createElement('td');
                td1.appendChild(newSelect);
                if (cfg.stockHints) {
                    var small = document.createElement('small');
                    small.className = 'text-muted available-stock-hint';
                    td1.appendChild(small);
                }
                newRow.appendChild(td1);

                ['Description', 'Qty', 'UnitPrice', 'VATRate'].forEach(function (name, idx) {
                    var inp = document.createElement('input');
                    inp.className = 'form-control' + (idx === 1 ? ' qty-input' : idx === 2 ? ' price-input' : idx === 3 ? ' vat-input' : '');
                    inp.type = idx === 0 ? 'text' : 'number';
                    inp.name = 'lines[' + lineIndex + '].' + name;
                    // Narx va QQS shartnomadan keladi — faqat o'qish uchun
                    if (idx >= 1) { inp.step = '0.01'; inp.min = '0'; inp.required = true; if (idx === 2 || idx === 3) inp.readOnly = true; }
                    if (idx === 3) { inp.max = '100'; inp.value = '0'; }
                    var td = document.createElement('td');
                    td.appendChild(inp);
                    newRow.appendChild(td);
                });

                var tdBtn = document.createElement('td');
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'btn btn-sm btn-danger remove-line';
                btn.textContent = (window.erpT ? erpT('Js_Crm_Delete') : 'O\'chirish');
                // O'chirish document darajasidagi delegat orqali (avval ikki marta tasdiq so'ralardi — CRM-B20)
                tdBtn.appendChild(btn);
                newRow.appendChild(tdBtn);

                table.querySelector('tbody').appendChild(newRow);

                addListeners(newRow);
                if (cfg.stockHints) {
                    newSelect.addEventListener('change', function () { updateStockHint(newRow); });
                    updateStockHint(newRow);
                }
                lineIndex++;
            });
        }

        document.addEventListener('click', function (e) {
            if (e.target && e.target.classList && e.target.classList.contains('remove-line')) {
                if (confirm((window.erpT ? erpT('Js_Crm_ConfirmDeleteRow') : 'Ushbu qatorni o\'chirishni tasdiqlaysizmi?'))) {
                    var tr = e.target.closest('tr');
                    if (tr) { tr.remove(); calculateTotals(); }
                }
            }
        });

        document.addEventListener('change', function (e) {
            if (e.target && e.target.classList && e.target.classList.contains('product-select')) {
                var tr = e.target.closest('tr');
                if (tr) {
                    updateStockHint(tr);
                    var contractId = contractEl ? contractEl.value : '';
                    var productId = e.target.value;
                    var priceInp = tr.querySelector('.price-input');
                    var vatInp = tr.querySelector('.vat-input');
                    if (contractId && productId && priceInp) loadPriceForProduct(contractId, productId, priceInp, vatInp);
                }
            }
        });

        // Ombor almashsa — mahsulot ro'yxati (va "Mavjud") shu ombor qoldig'idan qayta yuklanadi; tanlovlar saqlanadi
        var whEl = document.getElementById('WarehouseID');
        if (whEl && urls.warehouseProducts) {
            whEl.addEventListener('change', function () {
                if (!this.value) return;
                var url = urls.warehouseProducts + '?warehouseId=' + encodeURIComponent(this.value) +
                    (cfg.invoiceId ? '&invoiceId=' + cfg.invoiceId : '');
                fetch(url)
                    .then(function (r) { return r.json(); })
                    .then(function (items) {
                        var placeholder = window.erpT ? erpT('Js_Select') : 'Tanlang';
                        document.querySelectorAll('select.product-select').forEach(function (sel) {
                            var current = sel.value;
                            sel.innerHTML = '';
                            sel.appendChild(new Option(placeholder, ''));
                            (items || []).forEach(function (it) { sel.appendChild(new Option(it.Text, it.Value)); });
                            sel.value = current;
                            if (sel.value !== current) sel.value = '';   // bu omborda yo'q mahsulot — tanlov tozalanadi
                        });
                        buildStockMap();
                        document.querySelectorAll('#invoiceLinesTable tbody tr').forEach(function (r) { updateStockHint(r); });
                    });
            });
        }

        addListeners();
        calculateTotals();
        buildStockMap();
        updateStockHint(document.querySelector('#invoiceLinesTable tbody tr:first-child'));

        // Buyurtmadan qatorlarni to'ldirish; xato bilan qaytgan formadagi qatorlar qayta to'ldirilmaydi
        if (urls.orderProducts && cfg.initialOrderId && !cfg.hasPostedLines) {
            fetch(urls.orderProducts + '?orderId=' + cfg.initialOrderId)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data || data.length === 0) return;
                    var tbody = document.getElementById('invoiceLinesTable').querySelector('tbody');
                    var first = tbody.querySelector('tr');
                    for (var i = 0; i < data.length; i++) {
                        var row = (i === 0) ? first : first.cloneNode(true);
                        var sel = row.querySelector('.product-select');
                        var qtyInp = row.querySelector('.qty-input');
                        var priceInp = row.querySelector('.price-input');
                        var vatInp = row.querySelector('.vat-input');
                        if (sel) sel.value = data[i].ProductID;
                        if (qtyInp) qtyInp.value = data[i].Qty;
                        if (priceInp) priceInp.value = data[i].UnitPrice;
                        if (vatInp) vatInp.value = (data[i].VATRate != null ? parseFloat(data[i].VATRate) : 0).toFixed(2);
                        if (i > 0) {
                            renameRow(row, i);
                            tbody.appendChild(row);
                        }
                    }
                    lineIndex = data.length;
                    addListeners();
                    calculateTotals();
                    buildStockMap();
                    document.querySelectorAll('#invoiceLinesTable tbody tr').forEach(function (r) { updateStockHint(r); });
                });
        }

        function renameRow(row, index) {
            row.querySelectorAll('[name]').forEach(function (inp) {
                if (inp.name && inp.name.indexOf('lines[0]') === 0)
                    inp.name = inp.name.replace('lines[0]', 'lines[' + index + ']');
            });
        }

        // O'rtadagi qator o'chirilsa indekslarda bo'shliq qoladi va MVC binder undan keyingi
        // qatorlarni jimgina tashlab yuboradi — yuborishdan oldin 0..n-1 qilib qayta raqamlaymiz (CRM-B08)
        var form = cfg.formId ? document.getElementById(cfg.formId) : null;
        if (form) {
            form.addEventListener('submit', function () {
                var rows = document.querySelectorAll('#invoiceLinesTable tbody tr');
                for (var i = 0; i < rows.length; i++) {
                    rows[i].querySelectorAll('[name^="lines["]').forEach(function (el) {
                        el.name = el.name.replace(/^lines\[\d+\]/, 'lines[' + i + ']');
                    });
                }
            });
        }

        var vs = document.getElementById('validationSummary');
        if (vs && vs.textContent.trim()) vs.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
