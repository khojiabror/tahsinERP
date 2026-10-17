/*
 * Hisobot blokini alohida oynada chop etish (CRM-D22: uchta hisobot view'ida bir xil skript edi).
 *
 * Foydalanish: <button data-print-area="printArea"> — qiymat chop etiladigan elementning id'si.
 * Element topilmasa butun sahifa chop etiladi.
 */
(function () {
    'use strict';

    var PRINT_STYLE =
        'body{font-family:Arial,sans-serif;margin:12px;background:#fff;color:#111}' +
        '.report-table{font-size:10px;width:100%;border-collapse:collapse}' +
        '.report-table th,.report-table td{border:1px solid #333;padding:4px 6px}' +
        '.report-table thead th{background:#f5f5f5}' +
        '.report-signatures .row{page-break-inside:avoid}' +
        '@media print{body{margin:0;padding:8px}}';

    function printArea(el) {
        if (!el) {
            window.print();
            return;
        }
        var w = window.open('', '_blank');
        w.document.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Chop etish</title><style>' +
            PRINT_STYLE + '</style></head><body>' + el.innerHTML + '</body></html>');
        w.document.close();
        w.focus();
        setTimeout(function () { w.print(); w.close(); }, 250);
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest ? e.target.closest('[data-print-area]') : null;
        if (!btn) return;
        e.preventDefault();
        printArea(document.getElementById(btn.getAttribute('data-print-area')));
    });
})();
