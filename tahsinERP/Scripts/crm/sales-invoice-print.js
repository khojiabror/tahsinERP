/*
 * Chop etish sahifasi: nusxa sonini tanlash va html2pdf orqali PDF yuklab olish (CRM-D21 / CRM-M03).
 *
 * Belgilash (umumiy, faqat invoysga bog'liq emas):
 *   #copyCountSelect                    — nusxa soni (3 / 6)
 *   .invoice-copy                       — har bir nusxa bloki
 *   #invoice-print-root[data-pdf-filename] — PDFga aylantiriladigan blok va fayl nomi
 *   [data-print-action="print|pdf|close"]  — toolbar tugmalari
 * html2pdf.bundle.min.js shu fayldan oldin ulanadi.
 */
(function () {
    'use strict';

    function getCopyCount() {
        var select = document.getElementById('copyCountSelect');
        return select ? parseInt(select.value || '3', 10) : 3;
    }

    function setCopyCount(count) {
        var copies = document.querySelectorAll('.invoice-copy');
        for (var i = 0; i < copies.length; i++) {
            copies[i].style.display = (i < count) ? 'flex' : 'none';
        }
    }

    function downloadPdf() {
        var container = document.getElementById('invoice-print-root');
        var filename = (container && container.getAttribute('data-pdf-filename')) || 'document.pdf';
        var copyCount = getCopyCount();

        if (typeof html2pdf === 'undefined') {
            alert('PDF kutubxonasi yuklanmadi. Iltimos, qayta urinib koring.');
            return;
        }

        html2pdf().set({
            margin: [4, 4, 4, 4],
            filename: filename,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { scale: 2, useCORS: true, logging: false },
            jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' },
            pagebreak: { mode: ['css', 'legacy'] }
        })
        .from(container)
        .toPdf()
        .get('pdf')
        .then(function (pdf) {
            var totalPages = pdf.internal.getNumberOfPages();
            if (copyCount === 3 && totalPages > 1) {
                // 3 nusxa uchun ba'zan bo'sh sahifa qo'shiladi, oxirgisini o'chiramiz
                pdf.deletePage(totalPages);
            }
        })
        .save();
    }

    function init() {
        var select = document.getElementById('copyCountSelect');
        if (select) {
            setCopyCount(getCopyCount());
            select.addEventListener('change', function () {
                setCopyCount(parseInt(this.value || '3', 10));
            });
        }

        document.addEventListener('click', function (e) {
            var btn = e.target && e.target.closest ? e.target.closest('[data-print-action]') : null;
            if (!btn) return;
            var action = btn.getAttribute('data-print-action');
            if (action === 'print') window.print();
            else if (action === 'pdf') downloadPdf();
            else if (action === 'close') window.close();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
