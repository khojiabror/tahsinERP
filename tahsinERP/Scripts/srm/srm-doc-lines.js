/*
 * SRM ombor hujjat formalari (Incomes/Expenses Create va Edit) qatorlar jadvalining umumiy yordamchilari.
 * Avval escapeHtml, birlik ro'yxati va jami summa har to'rt formada alohida nusxa edi (audit "Duplicates map").
 *
 *   srmDocLines.escapeHtml(text)
 *   srmDocLines.unitOptions(units, selectedId)   // units: [{ Value, Text }]
 *   srmDocLines.updateTotal()                    // .total-price yig'indisi -> #totalAmount
 */
(function (window) {
    'use strict';

    function escapeHtml(text) {
        return String(text == null ? '' : text)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function unitOptions(units, selectedId) {
        var options = '<option value="">' + (window.erpT ? erpT('Js_Srm_SelectDashes') : '---Tanlang---') + '</option>';
        (units || []).forEach(function (unit) {
            var selected = unit.Value == selectedId ? 'selected' : '';
            options += '<option value="' + escapeHtml(unit.Value) + '" ' + selected + '>' + escapeHtml(unit.Text) + '</option>';
        });
        return options;
    }

    function updateTotal() {
        var $ = window.jQuery;
        var total = 0;
        $('.total-price').each(function () {
            total += parseFloat($(this).val()) || 0;
        });
        $('#totalAmount').text(total.toFixed(2));
    }

    window.srmDocLines = {
        escapeHtml: escapeHtml,
        unitOptions: unitOptions,
        updateTotal: updateTotal
    };
})(window);
