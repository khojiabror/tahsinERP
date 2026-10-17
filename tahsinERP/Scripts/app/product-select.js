/*
 * Hujjat qatoridagi mahsulot tanlovi — butun katalog har qatorga <option> qilib yozilmaydi (SRM-P02).
 *
 * Avval har qator uchun P ta mahsulotning hammasi <option> sifatida (serverda yoki JS'da) yaratilardi:
 * 3000 mahsulot x 40 qator = 120 000 <option>, bir necha MB HTML va sekin select2 init.
 * Endi qatorda faqat tanlangan mahsulot <option>'i turadi; ro'yxat select2 orqali sahifadagi
 * yagona JSON massividan qidiruv bilan (50 tadan, sahifalab) ko'rsatiladi.
 *
 * Foydalanish:
 *   var html = ProductSelect.optionsHtml(products, selectedId);   // satr qurishda
 *   ProductSelect.attach($('select.product-select'), products);   // select2 ulash
 * products: [{ Value: "12", Text: "PNo - Nomi" }, ...]
 * htmlEscape — indexed-rows.js dan.
 */
(function () {
    'use strict';

    var PAGE_SIZE = 50;

    function esc(value) {
        return window.htmlEscape ? window.htmlEscape(value) : String(value == null ? '' : value);
    }

    function findText(products, id) {
        if (id === null || id === undefined || id === '') return null;
        var key = String(id);
        for (var i = 0; i < products.length; i++) {
            if (String(products[i].Value) === key) return products[i].Text;
        }
        return null;
    }

    /** Bo'sh variant + (bo'lsa) faqat tanlangan mahsulot. */
    function optionsHtml(products, selectedId, fallbackText) {
        var html = '<option value="">---Tanlang---</option>';
        var text = findText(products || [], selectedId);
        if (text === null && selectedId && fallbackText) text = fallbackText;
        if (text !== null) {
            html += '<option value="' + esc(selectedId) + '" data-pno="' + esc(text) + '" selected>' + esc(text) + '</option>';
        }
        return html;
    }

    function attach($selects, products) {
        var $ = window.jQuery;
        if (!$ || !$.fn.select2) return;
        var list = products || [];

        $selects.each(function () {
            var $select = $(this);
            if ($select.data('productSelectAttached')) return;
            if ($select.hasClass('select2-hidden-accessible')) {
                $select.select2('destroy');
            }

            $select.select2({
                width: '100%',
                placeholder: '---Tanlang---',
                ajax: {
                    delay: 150,
                    transport: function (params, success) {
                        var term = ((params.data && params.data.term) || '').toLowerCase();
                        var page = (params.data && params.data.page) || 1;
                        var filtered = term
                            ? list.filter(function (p) { return String(p.Text).toLowerCase().indexOf(term) >= 0; })
                            : list;
                        var slice = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
                        success({
                            results: slice.map(function (p) { return { id: String(p.Value), text: p.Text }; }),
                            pagination: { more: page * PAGE_SIZE < filtered.length }
                        });
                        return { abort: function () { } };
                    },
                    processResults: function (data) { return data; }
                }
            });

            // Tanlangan variantga data-pno (POrders formalari PNo'ni shundan oladi)
            $select.on('select2:select', function (e) {
                var data = e.params && e.params.data;
                if (data) {
                    $select.find('option[value="' + data.id + '"]').attr('data-pno', data.text);
                }
            });

            $select.data('productSelectAttached', true);
        });
    }

    window.ProductSelect = {
        optionsHtml: optionsHtml,
        attach: attach,
        findText: findText
    };
})();
