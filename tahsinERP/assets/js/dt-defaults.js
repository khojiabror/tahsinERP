/*
 * DataTables umumiy sozlamalari (#16 / SRM-D15). Ikkala layout'da DataTables'dan keyin,
 * sahifa skriptlaridan oldin yuklanadi.
 *  - O'zbekcha til matnlari standart (sahifa o'z "language"ini bersa, o'shanisi ishlaydi).
 *  - PDF eksport: A4, ustunlar ko'p bo'lsa landscape, ustunlar teng kenglikda.
 *  - Eksport tugmalari: Scripts/app/erp-datatable.js — erpDataTable.buttons({ fileName: 'Valyutalar' })
 *    (u ham layout'da shu fayldan oldin yuklanadi).
 */
(function ($) {
    if (!($ && $.fn && $.fn.dataTable)) {
        return;
    }

    var language = $.extend({}, window.erpDataTable ? window.erpDataTable.language : {}, { processing: window.erpT ? window.erpT("Js_Loading") : "Yuklanmoqda..." });
    $.extend(true, $.fn.dataTable.defaults, { language: language });

    var buttons = $.fn.dataTable.ext.buttons;
    if (buttons && buttons.pdfHtml5) {
        var pdf = buttons.pdfHtml5;
        pdf.pageSize = 'A4';
        pdf.orientation = 'portrait';
        pdf.customize = function (doc, config, dtApi) {
            try {
                var api = dtApi || (this && this.api ? this.api() : null);
                var visibleColumns = api && api.columns ? api.columns(':visible').count() : 0;
                doc.pageOrientation = visibleColumns > 7 ? 'landscape' : 'portrait';
                doc.pageSize = 'A4';
                doc.pageMargins = [15, 15, 15, 15];

                var baseFont = visibleColumns > 10 ? 7 : 8;
                doc.defaultStyle = doc.defaultStyle || {};
                doc.styles = doc.styles || {};
                doc.styles.tableHeader = doc.styles.tableHeader || {};
                doc.defaultStyle.fontSize = baseFont;
                doc.styles.tableHeader.fontSize = baseFont + 1;
                doc.styles.tableHeader.fillColor = '#f2f2f2';
                doc.styles.tableHeader.color = '#000000';

                for (var i = 0; i < (doc.content || []).length; i++) {
                    var c = doc.content[i];
                    if (c && c.table && c.table.body && c.table.body[0]) {
                        c.table.widths = Array(c.table.body[0].length).fill('*');
                        break;
                    }
                }
            } catch (e) {
                // Xatolik bo'lsa — standart pdfHtml5 xulqi qoladi
                if (window.console && console.warn) console.warn('PDF export customize error', e);
            }
        };
    }

})(window.jQuery);
