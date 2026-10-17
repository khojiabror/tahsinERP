/*
 * Ishbay grid (GridCreate / GridEdit uchun umumiy) — HRM-D14, HRM-P06, HRM-B14, HRM-S04.
 *
 *  - Spravochniklar (xodim, mahsulot, operatsiya) va oy tariflari sahifaga BIR MARTA JSON sifatida beriladi;
 *    avval har qatorda to'liq <select> HTML'i va har qator/klavishada GetTariff AJAX so'rovi bor edi.
 *  - Tarif mijoz tomonida hisoblanadi: Services/HR/PieceRateTariffResolver bilan bir xil qoida
 *    (lavozimga xos tarif, bo'lmasa umumiy; sana oralig'ida; eng so'nggi EffectiveFrom, keyin eng katta ID).
 *  - Nomlar DOM API (textContent) orqali qo'yiladi — Html.Raw bilan qurilgan option satrlari olib tashlandi (XSS).
 *  - Qator nomlari "Rows.Index" usulida: o'rtadagi qator o'chirilsa ham keyingilari yo'qolmaydi.
 */
(function () {
    'use strict';

    function el(tag, attrs, text) {
        var e = document.createElement(tag);
        if (attrs) {
            Object.keys(attrs).forEach(function (k) {
                if (attrs[k] !== null && attrs[k] !== undefined) e.setAttribute(k, attrs[k]);
            });
        }
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }

    function buildSelectTemplate(items) {
        var sel = el('select');
        sel.appendChild(el('option', { value: '' }, 'Tanlang'));
        items.forEach(function (it) {
            sel.appendChild(el('option', { value: String(it.id) }, it.name));
        });
        return sel;
    }

    function PieceRateGrid(options) {
        this.data = options.data;
        this.tbody = document.querySelector(options.tbody);
        this.allowRemoveExisting = !!options.allowRemoveExisting;
        this.renderExistingActions = options.renderExistingActions || null;
        this.nextKey = 0;

        this.positionByEmployee = {};
        var self = this;
        this.data.employees.forEach(function (e) { self.positionByEmployee[e.id] = e.positionId; });

        this.tariffsByKey = {};
        this.data.tariffs.forEach(function (t) {
            var key = t.p + ':' + t.o;
            (self.tariffsByKey[key] = self.tariffsByKey[key] || []).push(t);
        });
        Object.keys(this.tariffsByKey).forEach(function (k) {
            self.tariffsByKey[k].sort(function (a, b) {
                if (a.from !== b.from) return a.from < b.from ? 1 : -1;
                return b.id - a.id;
            });
        });

        this.templates = {
            emp: buildSelectTemplate(this.data.employees),
            product: buildSelectTemplate(this.data.products),
            operation: buildSelectTemplate(this.data.operations)
        };

        this.tbody.addEventListener('click', function (e) {
            var btn = e.target.closest ? e.target.closest('.row-remove') : null;
            if (!btn) return;
            e.preventDefault();
            var tr = btn.closest('tr');
            if (tr) tr.parentNode.removeChild(tr);
        });
    }

    PieceRateGrid.prototype.resolveRate = function (productId, operationId, employeeId, workDate) {
        var list = this.tariffsByKey[productId + ':' + operationId];
        if (!list || !workDate) return null;
        var positionId = employeeId ? this.positionByEmployee[employeeId] : null;
        var general = null;
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            if (t.from > workDate) continue;
            if (t.to && t.to < workDate) continue;
            if (positionId && t.pos === positionId) return t.rate;
            if (t.pos === null && general === null) {
                general = t.rate;
                if (!positionId) return general;
            }
        }
        return general;
    };

    PieceRateGrid.prototype.recalcRow = function (tr) {
        var pid = tr.querySelector('.product').value;
        var oid = tr.querySelector('.operation').value;
        var eid = tr.querySelector('.emp').value;
        var wdate = tr.querySelector('.work-date').value;
        var qty = parseFloat(tr.querySelector('.qty').value || '0') || 0;
        var rateInput = tr.querySelector('.rate');
        var amountInput = tr.querySelector('.amount');
        if (!pid || !oid) { rateInput.value = ''; amountInput.value = ''; return; }
        var rate = this.resolveRate(parseInt(pid, 10), parseInt(oid, 10), eid ? parseInt(eid, 10) : null, wdate);
        rate = rate || 0;
        rateInput.value = rate.toFixed(2);
        amountInput.value = (rate * qty).toFixed(2);
    };

    PieceRateGrid.prototype.makeSelect = function (kind, name, value) {
        var sel = this.templates[kind].cloneNode(true);
        sel.name = name;
        sel.className = 'form-control ' + kind;
        if (value !== null && value !== undefined) sel.value = String(value);
        return sel;
    };

    /** row: { entryId, employeeId, productId, operationId, workDate ('yyyy-MM-dd'), quantity } */
    PieceRateGrid.prototype.addRow = function (row) {
        row = row || {};
        var key = 'r' + (this.nextKey++);
        var prefix = 'Rows[' + key + '].';
        var tr = el('tr');

        var tdEmp = el('td', { 'class': 'freeze-emp' });
        tdEmp.appendChild(el('input', { type: 'hidden', name: 'Rows.Index', value: key }));
        tdEmp.appendChild(el('input', { type: 'hidden', name: prefix + 'EntryID', value: row.entryId || '' }));
        tdEmp.appendChild(this.makeSelect('emp', prefix + 'EmployeeID', row.employeeId));
        tr.appendChild(tdEmp);

        var tdProduct = el('td');
        tdProduct.appendChild(this.makeSelect('product', prefix + 'ProductID', row.productId));
        tr.appendChild(tdProduct);

        var tdOperation = el('td');
        tdOperation.appendChild(this.makeSelect('operation', prefix + 'OperationID', row.operationId));
        tr.appendChild(tdOperation);

        var tdDate = el('td');
        tdDate.appendChild(el('input', { type: 'date', name: prefix + 'WorkDate', 'class': 'form-control work-date', value: row.workDate || '' }));
        tr.appendChild(tdDate);

        var tdQty = el('td');
        tdQty.appendChild(el('input', {
            type: 'number', step: '0.01', min: '0', name: prefix + 'Quantity', 'class': 'form-control qty',
            value: row.quantity !== null && row.quantity !== undefined ? String(row.quantity) : ''
        }));
        tr.appendChild(tdQty);

        var tdRate = el('td');
        tdRate.appendChild(el('input', { type: 'text', 'class': 'form-control rate', readonly: 'readonly' }));
        tr.appendChild(tdRate);

        var tdAmount = el('td');
        tdAmount.appendChild(el('input', { type: 'text', 'class': 'form-control amount', readonly: 'readonly' }));
        tr.appendChild(tdAmount);

        if (this.renderExistingActions !== null) {
            var tdActions = el('td', { 'class': 'text-nowrap text-center align-middle' });
            if (row.entryId) {
                this.renderExistingActions(tdActions, row);
            } else {
                var btn = el('button', { type: 'button', 'class': 'btn btn-sm btn-outline-danger row-remove', title: "Qatorni olib tashlash" });
                btn.appendChild(el('i', { 'class': 'bx bx-trash' }));
                tdActions.appendChild(btn);
            }
            tr.appendChild(tdActions);
        }

        var self = this;
        // Faqat 'change' — avval har klavishda server so'rovi bor edi
        tr.addEventListener('change', function (e) {
            if (e.target && e.target.matches && e.target.matches('.emp,.product,.operation,.qty,.work-date')) self.recalcRow(tr);
        });
        tr.querySelector('.qty').addEventListener('input', function () { self.recalcRow(tr); });

        this.tbody.appendChild(tr);
        this.recalcRow(tr);
        return tr;
    };

    PieceRateGrid.prototype.render = function (rows) {
        var frag = document.createDocumentFragment();
        var original = this.tbody;
        this.tbody = frag;
        var self = this;
        rows.forEach(function (r) { self.addRow(r); });
        this.tbody = original;
        this.tbody.appendChild(frag);
    };

    window.PieceRateGrid = PieceRateGrid;
})();
