/*
 * Hujjat qatorlari (X[i].Field) indekslarini submit oldidan 0..n-1 ga qayta raqamlaydi.
 *
 * Muammo (SRM-B05): qatorlar ortib boruvchi hisoblagich bilan nomlanadi; o'rtadagi qator
 * o'chirilsa indekslarda bo'shliq qoladi (0,2,3) va MVC default binder birinchi bo'shliqda
 * to'xtaydi — keyingi barcha qatorlar jim yo'qolardi.
 *
 * Foydalanish: formaga data-renumber="OrderProducts" (bir nechta bo'lsa vergul bilan) qo'ying va
 * shu faylni ulang. Handler capture fazasida ishlaydi, shuning uchun sahifadagi jQuery submit
 * handlerlaridan oldin bajariladi. AJAX bilan yuborishda window.renumberIndexedRows(form) ni chaqiring.
 */
(function () {
    'use strict';

    function escapeRegex(value) {
        return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function renumberPrefix(form, prefix) {
        var pattern = new RegExp('^' + escapeRegex(prefix) + '\\[(\\d+)\\]');
        var map = {};
        var next = 0;
        var elements = form.querySelectorAll('[name^="' + prefix + '["]');

        // DOM tartibida: bir qatorning maydonlari ketma-ket keladi, demak birinchi uchragan
        // eski indeks — qator tartibi.
        for (var i = 0; i < elements.length; i++) {
            var el = elements[i];
            var match = el.name.match(pattern);
            if (!match) continue;
            if (!Object.prototype.hasOwnProperty.call(map, match[1])) {
                map[match[1]] = next++;
            }
            el.name = el.name.replace(pattern, prefix + '[' + map[match[1]] + ']');
        }
    }

    function renumberIndexedRows(form, prefixes) {
        if (!form) return;
        var list = prefixes || (form.getAttribute('data-renumber') || '').split(',');
        for (var i = 0; i < list.length; i++) {
            var prefix = (list[i] || '').trim();
            if (prefix) renumberPrefix(form, prefix);
        }
    }

    window.renumberIndexedRows = renumberIndexedRows;

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (form && form.hasAttribute && form.hasAttribute('data-renumber')) {
            renumberIndexedRows(form);
        }
    }, true);
})();
