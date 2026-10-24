/*
 * Distributsiya moduli umumiy yordamchilari.
 *   dstPost(url, data)            — JSON POST (CSRF header'ni Scripts/csrf.js qo'shadi); {success, data, message}
 *   dstNotify(message, isError)   — sahifa tepasidagi xabar
 *   dstServerTable(sel, options)  — server-side DataTables (uz/ru tilida, window.dstI18n)
 *   dstEsc(value)                 — HTML escape
 */
(function (window, $) {
    'use strict';

    var i18n = window.dstI18n || {};

    window.dstEsc = function (value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };

    window.dstNotify = function (message, isError) {
        var $box = $('#dstNotify');
        if (!$box.length) {
            $box = $('<div id="dstNotify" style="position:fixed;top:80px;right:20px;z-index:2000;max-width:420px"></div>').appendTo('body');
        }
        var $a = $('<div class="alert ' + (isError ? 'alert-danger' : 'alert-success') + ' shadow" role="alert"></div>')
            .text(message || (isError ? (i18n.error || 'Xatolik') : (i18n.saved || 'Saqlandi')));
        $box.append($a);
        setTimeout(function () { $a.fadeOut(300, function () { $a.remove(); }); }, isError ? 7000 : 2500);
    };

    window.dstPost = function (url, data) {
        return $.ajax({
            url: url, type: 'POST', data: data, dataType: 'json', traditional: true
        }).then(function (r) {
            if (r && r.success) return r.data;
            window.dstNotify(r && r.message, true);
            return $.Deferred().reject(r).promise();
        }, function (xhr) {
            window.dstNotify(xhr.status === 403 ? (i18n.forbidden || "Ruxsat yo'q") : (i18n.error || 'Xatolik'), true);
            return $.Deferred().reject(xhr).promise();
        });
    };

    window.dstServerTable = function (selector, options) {
        var $form = $(options.filterForm);
        function filterParams() {
            var params = {};
            $.each($form.serializeArray(), function (_, f) { params[f.name] = f.value; });
            return params;
        }
        var table = $(selector).DataTable({
            processing: true,
            serverSide: true,
            searchDelay: 400,
            pageLength: options.pageLength || 25,
            lengthMenu: [[10, 25, 50, 100, 500], [10, 25, 50, 100, 500]],
            order: options.order || [[0, 'asc']],
            scrollX: true,
            ajax: {
                url: options.dataUrl,
                type: 'GET',
                // Server faqat draw/start/length/search/order[0] ni o'qiydi (DataTablesPageRequest). DataTables'ning
                // to'liq so'rovi (columns[i][...]) 9+ ustunda IIS query string chegarasidan (2048) oshib 404 berardi.
                data: function (d) {
                    var p = { draw: d.draw, start: d.start, length: d.length, 'search[value]': d.search ? d.search.value : '' };
                    if (d.order && d.order.length) { p['order[0][column]'] = d.order[0].column; p['order[0][dir]'] = d.order[0].dir; }
                    return $.extend(p, filterParams());
                },
                error: function (xhr) {
                    $(selector).find('tbody').html('<tr><td colspan="30" class="text-danger">' +
                        dstEsc(xhr.status === 403 ? (i18n.forbidden || "Ruxsat yo'q") : (i18n.loadError || 'Xatolik')) + '</td></tr>');
                }
            },
            columns: options.columns,
            language: i18n.table || {}
        });
        $form.on('change', 'select,input', function () { table.ajax.reload(); });
        return table;
    };
})(window, jQuery);
