/*
 * DataTables Excel/PDF tugmalari uchun yupqa adapter (audit dublikat #16). Asosiy implementatsiya — Core'ning
 * ~/assets/js/dt-defaults.js (ikkala layout'da yuklanadi): tahsinDt.exportButtons, o'zbekcha til, PDF standarti.
 * Bu fayl faqat 35 ta sahifa ishlatadigan qo'shimcha parametrlarni tahsinDt tugmalari ustiga qo'yadi —
 * tugma/til konfiguratsiyasi bu yerda qayta yozilmaydi.
 *
 *   $('#t').DataTable({
 *       dom: 'lBfrtip',
 *       buttons: erpDataTable.buttons({ fileName: 'Buyruqlar', pdfTitle: "Buyruqlar ro'yxati", columns: ':not(:last-child)' })
 *   });
 *
 * buttons(options):
 *   fileName  — fayl nomi prefiksi (_yyyyMMdd_HHmmss qo'shiladi); berilmasa DataTables standarti (sahifa sarlavhasi);
 *   title     — Excel va PDF ichidagi sarlavha;
 *   pdfTitle  — faqat PDF sarlavhasi; berilsa PDF albom (landscape) va shrift 9/10;
 *   landscape — PDF har doim albom (dt-defaults ustunlar soniga qarab tanlaydi);
 *   columns   — eksport ustunlari (standart ':visible'; amallar ustunisiz: ':not(:last-child)');
 *   pdf       — false bo'lsa faqat Excel;
 *   small     — tugmalar btn-sm;
 *   excel / pdfOptions — tugmaga qo'shimcha xossalar (aniq holatlar uchun).
 */
(function (window) {
    'use strict';

    function extend(target, source) {
        if (source) {
            Object.keys(source).forEach(function (k) { target[k] = source[k]; });
        }
        return target;
    }

    /** dt-defaults'dagi umumiy PDF customize'ni chaqirib, keyin sahifaga xos sozlamani qo'llaydi. */
    function withDefaultPdf(after) {
        return function (doc, config, api) {
            var base = window.jQuery.fn.dataTable.ext.buttons.pdfHtml5.customize;
            if (typeof base === 'function') {
                base.call(this, doc, config, api);
            }
            after(doc);
        };
    }

    function buttons(options) {
        options = options || {};
        var list = window.tahsinDt.exportButtons(options.fileName, {
            title: options.title,
            small: !!options.small,
            columns: options.columns,
            pdf: options.pdf
        });
        var excel = list[0];
        var pdf = list[1];

        if (!options.fileName) {
            // Avvalgidek: fayl nomi sahifa/tugma sarlavhasidan
            delete excel.filename;
            if (pdf) delete pdf.filename;
        }
        if (!options.title) {
            delete excel.title;
            if (pdf) delete pdf.title;
        }

        if (pdf) {
            if (options.pdfTitle) {
                pdf.title = options.pdfTitle;
                pdf.orientation = 'landscape';
                pdf.customize = withDefaultPdf(function (doc) {
                    doc.pageOrientation = 'landscape';
                    doc.defaultStyle.fontSize = 9;
                    doc.styles.tableHeader.fontSize = 10;
                });
            } else if (options.landscape) {
                pdf.orientation = 'landscape';
                pdf.customize = withDefaultPdf(function (doc) { doc.pageOrientation = 'landscape'; });
            }
            extend(pdf, options.pdfOptions);
        }
        extend(excel, options.excel);
        return list;
    }

    // Til matnlari dt-defaults'da DataTables standarti sifatida o'rnatilgan — sahifalarga alohida berish shart emas
    window.erpDataTable = { buttons: buttons };
})(window);
