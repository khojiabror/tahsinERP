/*
 * SRM ombor ro'yxatlari (Inventory, IndexDloc, PTST, Warehouse, WarehouseLocations) uchun DataTables'ning
 * umumiy sozlamalari: o'zbekcha matnlar va Excel/PDF tugmalari (avval har sahifada ~60 qatorlik nusxa).
 *
 *   $('#t').DataTable({
 *       language: srmDataTable.language,
 *       dom: 'lBfrtip',
 *       buttons: srmDataTable.buttons({ fileName: 'Ombor_qoldiqlari', pdfTitle: 'Ombor qoldiqlari' })
 *   });
 *
 * buttons(options):
 *   fileName  — fayl nomi prefiksi (oxiriga _yyyyMMdd_HHmmss qo'shiladi); berilmasa DataTables standarti;
 *   pdfTitle  — PDF sarlavhasi; berilsa PDF albom (landscape) va kichik shrift bilan;
 *   small     — tugmalar btn-sm.
 */
(function (window) {
    'use strict';

    function pad(n) { return String(n).padStart(2, '0'); }

    function stamp() {
        var now = new Date();
        return now.getFullYear() + pad(now.getMonth() + 1) + pad(now.getDate()) + '_' +
            pad(now.getHours()) + pad(now.getMinutes()) + pad(now.getSeconds());
    }

    function stripHtml(data) {
        return (data == null ? '' : String(data)).replace(/<[^>]*>/g, '').trim();
    }

    var exportOptions = { columns: ':visible', format: { body: stripHtml } };

    function buttons(options) {
        options = options || {};
        var size = options.small ? ' btn-sm' : '';
        var excel = {
            extend: 'excelHtml5',
            text: '<i class="bx bxs-file-excel"></i> Excel',
            titleAttr: 'Excelga yuklash',
            className: 'btn btn-success' + size + ' mr-1',
            exportOptions: exportOptions
        };
        var pdf = {
            extend: 'pdfHtml5',
            text: '<i class="bx bxs-file-pdf"></i> PDF',
            titleAttr: 'PDFga yuklash',
            className: 'btn btn-danger' + size,
            pageSize: 'A4',
            exportOptions: exportOptions
        };
        if (options.fileName) {
            excel.filename = pdf.filename = function () { return options.fileName + '_' + stamp(); };
        }
        if (options.pdfTitle) {
            pdf.orientation = 'landscape';
            pdf.title = options.pdfTitle;
            pdf.customize = function (doc) {
                doc.defaultStyle.fontSize = 9;
                doc.styles.tableHeader.fontSize = 10;
            };
        }
        return [excel, pdf];
    }

    window.srmDataTable = {
        language: {
            search: 'Qidirish:',
            lengthMenu: "_MENU_ ta ko'rsatish",
            info: '_TOTAL_ ta yozuvdan _START_ dan _END_ gacha',
            infoEmpty: "Yozuvlar yo'q",
            infoFiltered: '(jami _MAX_ ta yozuvdan filtrlangan)',
            paginate: { first: 'Birinchi', last: 'Oxirgi', next: 'Keyingi', previous: 'Oldingi' },
            zeroRecords: "Ma'lumot topilmadi"
        },
        stripHtml: stripHtml,
        buttons: buttons
    };
})(window);
