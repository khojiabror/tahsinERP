/*
 * DataTables umumiy sozlamalari (#16 / SRM-D15). Ikkala layout'da DataTables'dan keyin,
 * sahifa skriptlaridan oldin yuklanadi.
 *  - O'zbekcha til matnlari standart (sahifa o'z "language"ini bersa, o'shanisi ishlaydi).
 *  - PDF eksport: A4, ustunlar ko'p bo'lsa landscape, ustunlar teng kenglikda.
 *  - tahsinDt.exportButtons('Fayl_nomi') — view'lardagi copy-paste Excel/PDF tugmalari o'rniga:
 *      $('#t').DataTable({ dom: 'lBfrtip', buttons: tahsinDt.exportButtons('Valyutalar') });
 */
(function ($) {
    if (!($ && $.fn && $.fn.dataTable)) {
        return;
    }

    var language = {
        search: "Qidirish:",
        lengthMenu: "_MENU_ ta ko'rsatish",
        info: "_TOTAL_ ta yozuvdan _START_ dan _END_ gacha",
        infoEmpty: "Yozuvlar yo'q",
        infoFiltered: "(jami _MAX_ ta yozuvdan filtrlangan)",
        processing: "Yuklanmoqda...",
        paginate: { first: "Birinchi", last: "Oxirgi", next: "Keyingi", previous: "Oldingi" },
        zeroRecords: "Ma'lumot topilmadi"
    };
    $.extend(true, $.fn.dataTable.defaults, { language: language });

    function stripTags(data) {
        return (data == null ? '' : data.toString()).replace(/<[^>]*>/g, '').trim();
    }

    function stamp() {
        var d = new Date(), p = function (n) { return String(n).padStart(2, '0'); };
        return d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate()) + '_' + p(d.getHours()) + p(d.getMinutes()) + p(d.getSeconds());
    }

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

    window.tahsinDt = {
        language: language,
        stripTags: stripTags,
        /** Standart Excel + PDF tugmalari. opts: { title, small, columns, pdf: false } */
        exportButtons: function (fileName, opts) {
            opts = opts || {};
            var size = opts.small === false ? '' : ' btn-sm';
            var exportOptions = { columns: opts.columns || ':visible', format: { body: stripTags } };
            var name = function () { return (fileName || 'export') + '_' + stamp(); };
            var list = [{
                extend: 'excelHtml5',
                text: '<i class="bx bxs-file-excel"></i> Excel',
                titleAttr: 'Excelga yuklash',
                className: 'btn btn-success mr-1' + size,
                filename: name,
                title: opts.title || null,
                exportOptions: exportOptions
            }];
            if (opts.pdf !== false) {
                list.push({
                    extend: 'pdfHtml5',
                    text: '<i class="bx bxs-file-pdf"></i> PDF',
                    titleAttr: 'PDFga yuklash',
                    className: 'btn btn-danger' + size,
                    filename: name,
                    title: opts.title || '',
                    exportOptions: exportOptions
                });
            }
            return list;
        }
    };
})(window.jQuery);
