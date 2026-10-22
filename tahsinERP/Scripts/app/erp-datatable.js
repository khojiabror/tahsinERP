/*
 * DataTables Excel/PDF eksport tugmalari — butun tizim uchun bitta joy (audit dublikat #16).
 * Avval ~30 ta sahifada har biri ~60 qatorlik nusxa edi (vaqt belgili fayl nomi, HTML'ni tozalash, PDF sozlamalari).
 * Ikkala layout'da dt-defaults.js'dan oldin yuklanadi (sahifaga alohida ulash shart emas); dt-defaults
 * bu yerdagi o'zbekcha matnlarni va umumiy PDF standartini DataTables'ga o'rnatadi (Core).
 *
 *   $('#t').DataTable({
 *       dom: 'lBfrtip',
 *       buttons: erpDataTable.buttons({ fileName: 'Buyruqlar', pdfTitle: "Buyruqlar ro'yxati", columns: ':not(:last-child)' })
 *   });
 *
 * buttons(options):
 *   fileName  — fayl nomi prefiksi (oxiriga _yyyyMMdd_HHmmss qo'shiladi); berilmasa DataTables standarti (sahifa sarlavhasi);
 *   title     — Excel va PDF ichidagi sarlavha (berilmasa standart);
 *   pdfTitle  — faqat PDF sarlavhasi; berilsa PDF albom (landscape) va kichik shrift (9/10) bilan;
 *   landscape — PDF albom (pdfTitle'siz, shrift o'zgarmaydi);
 *   columns   — eksport qilinadigan ustunlar (standart ':visible'; amallar ustunisiz: ':not(:last-child)');
 *   pdf       — false bo'lsa faqat Excel tugmasi;
 *   small     — tugmalar btn-sm;
 *   excel / pdfOptions — tugma sozlamalariga qo'shimcha (aniq holatlar uchun, masalan customize).
 */
(function (window) {
    'use strict';

    function pad(n) { return String(n).padStart(2, '0'); }

    function tr(key, fallback) {
        var t = window.erpI18n && window.erpI18n.t;
        return t && Object.prototype.hasOwnProperty.call(t, key) ? t[key] : fallback;
    }

    /** yyyyMMdd_HHmmss — fayl nomlari uchun. */
    function stamp() {
        var now = new Date();
        return now.getFullYear() + pad(now.getMonth() + 1) + pad(now.getDate()) + '_' +
            pad(now.getHours()) + pad(now.getMinutes()) + pad(now.getSeconds());
    }

    /** Katak HTML'ini (badge, havola) eksport uchun oddiy matnga. */
    function stripHtml(data) {
        return (data == null ? '' : String(data)).replace(/<[^>]*>/g, '').trim();
    }

    function extend(target, source) {
        if (source) {
            Object.keys(source).forEach(function (k) { target[k] = source[k]; });
        }
        return target;
    }

    function buttons(options) {
        options = options || {};
        var size = options.small ? ' btn-sm' : '';
        var exportOptions = { columns: options.columns || ':visible', format: { body: stripHtml } };

        var excel = {
            extend: 'excelHtml5',
            text: '<i class="bx bxs-file-excel"></i> Excel',
            titleAttr: tr('Js_ExcelTitle', 'Excelga yuklash'),
            className: 'btn btn-success' + size + ' mr-1',
            exportOptions: exportOptions
        };
        var pdf = {
            extend: 'pdfHtml5',
            text: '<i class="bx bxs-file-pdf"></i> PDF',
            titleAttr: tr('Js_PdfTitle', 'PDFga yuklash'),
            className: 'btn btn-danger' + size,
            pageSize: 'A4',
            exportOptions: exportOptions
        };

        if (options.fileName) {
            excel.filename = pdf.filename = function () { return options.fileName + '_' + stamp(); };
        }
        if (options.title) {
            excel.title = pdf.title = options.title;
        }
        if (options.pdfTitle || options.landscape) {
            pdf.orientation = 'landscape';
        }
        if (options.pdfTitle) {
            pdf.title = options.pdfTitle;
            pdf.customize = function (doc) {
                doc.defaultStyle.fontSize = 9;
                doc.styles.tableHeader.fontSize = 10;
            };
        }

        extend(excel, options.excel);
        extend(pdf, options.pdfOptions);
        return options.pdf === false ? [excel] : [excel, pdf];
    }

    window.erpDataTable = {
        // dt-defaults.js shu matnlarni DataTables standarti qiladi (sahifalarga berish shart emas)
        // matnlar joriy tilda (_I18nScript.cshtml: window.erpT); u yuklanmagan bo'lsa — o'zbekcha
        language: {
            search: tr('Js_SearchLabel', 'Qidirish:'),
            lengthMenu: tr('Dt_LengthMenu', "_MENU_ ta ko'rsatish"),
            info: tr('Dt_Info', '_TOTAL_ ta yozuvdan _START_ dan _END_ gacha'),
            infoEmpty: tr('Dt_InfoEmpty', "Yozuvlar yo'q"),
            infoFiltered: tr('Dt_InfoFiltered', '(jami _MAX_ ta yozuvdan filtrlangan)'),
            paginate: { first: tr('Dt_First', 'Birinchi'), last: tr('Dt_Last', 'Oxirgi'), next: tr('Dt_Next', 'Keyingi'), previous: tr('Dt_Previous', 'Oldingi') },
            zeroRecords: tr('Dt_ZeroRecords', "Ma'lumot topilmadi")
        },
        stamp: stamp,
        stripHtml: stripHtml,
        buttons: buttons
    };
})(window);
