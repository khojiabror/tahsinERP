/*
 * Hujjat qatorlari jadvali (line-item editor) — <table class="doc-lines">. Uslublar: assets/css/line-editor.css.
 * Layout'da sahifa skriptlaridan oldin yuklanadi; view'ga faqat jadvalga "doc-lines" klassini qo'shish kifoya.
 *
 *  - Jadvalni .dl-wrap konteyneriga oladi (mavjud .table-responsive shu vazifani bajaradi) — ichki skroll.
 *  - Har katakni ichidagi boshqaruv elementiga qarab tasniflaydi (dl-c-wide/name/sel/unit/num/pct/date/text/chk/act)
 *    va shu klassni sarlavhaga ham beradi; telefon kartochkasi uchun data-label (sarlavha matni) yozadi.
 *  - input[type=number] fokusda bo'lmaganda qiymat ming ajratkich bilan ko'rsatiladi (ustidagi qatlam;
 *    input qiymati va hisob-kitob o'zgarmaydi). jQuery .val(x) bilan yozilgan qiymat ham yangilanadi.
 *  - Qatorlar keyinroq JS bilan qo'shilsa — MutationObserver orqali avtomatik.
 *
 *   erpLineEditor.init(tableOrSelector)   // odatda shart emas: DOM tayyor bo'lganda table.doc-lines topiladi
 *   erpLineEditor.refresh(tableOrSelector)
 */
(function (window, document) {
    'use strict';

    var GROUP = ' '; // tor bo'sh joy: 1 234 567.89
    var WIDE_RE = /product|item|material|goods|tovar|mahsulot|nomenkl|counterparty|customer|supplier|partner|employee|contract/i;
    var UNIT_RE = /unit|uom|birlik|currency|valyuta/i;
    var PCT_RE = /vat|qqs|nds|disc|percent|pct|chegirma/i;
    var TEXT_RE = /comment|note|izoh|descr|remark|reason|izohi/i;
    var NAME_RE = /name|nomi/i;
    // Pul summasi (narx, jami) — kengroq son ustuni. "Amount" ko'p formada miqdor, shuning uchun bu yerda yo'q
    var AMT_RE = /price|narx|total|summa|cost|jami|balance|paid|debt/i;

    function hint(el) {
        // name="IncomeProducts[0].LocationID" -> faqat oxirgi bo'lak (aks holda "Products" har ustunni "tovar" qiladi)
        var name = el.name ? String(el.name).split('.').pop() : '';
        return [name, el.id, el.className, el.getAttribute('data-f'), el.getAttribute('data-type'), el.getAttribute('placeholder')]
            .filter(Boolean).join(' ');
    }

    function visibleControls(td) {
        var list = td.querySelectorAll('input, select, textarea');
        var out = [];
        for (var i = 0; i < list.length; i++) {
            var el = list[i];
            // select2 asl <select> ni yashiradi, lekin u baribir ustun turini belgilaydi — shuning uchun faqat aniq yashirilganlar
            if (el.type === 'hidden' || el.style.display === 'none' || el.classList.contains('d-none')) continue;
            out.push(el);
        }
        return out;
    }

    function classify(td) {
        var ctrls = visibleControls(td);
        if (!ctrls.length) {
            if (td.querySelector('button, .btn, a.btn')) return 'act';
            return '';
        }
        var el = ctrls[0];
        var tag = el.tagName.toLowerCase();
        var h = hint(el);
        if (tag === 'select') {
            if (UNIT_RE.test(h)) return 'unit';
            if (WIDE_RE.test(h)) return 'wide';
            return 'sel';
        }
        if (tag === 'textarea') return 'text';
        var type = (el.getAttribute('type') || 'text').toLowerCase();
        if (type === 'checkbox' || type === 'radio') return 'chk';
        if (type === 'number') {
            if (PCT_RE.test(h) || el.getAttribute('max') === '100') return 'pct';
            if (AMT_RE.test(h) || el.readOnly) return 'amt';
            return 'num';
        }
        if (type === 'date' || type === 'datetime-local' || type === 'month' || type === 'time' || /date|sana/i.test(h)) return 'date';
        if (WIDE_RE.test(h) && !TEXT_RE.test(h)) return (el.readOnly || el.disabled) ? 'name' : 'wide';
        if (NAME_RE.test(h) && (el.readOnly || el.disabled)) return 'name';
        return 'text';
    }

    // Qatordagi har katakning mantiqiy ustun raqami (colspan hisobga olinadi)
    function eachCell(row, fn) {
        var col = 0;
        for (var i = 0; i < row.cells.length; i++) {
            var c = row.cells[i];
            fn(c, col);
            col += c.colSpan || 1;
        }
    }

    function headerCells(table) {
        var map = {};
        var head = table.tHead;
        if (!head || !head.rows.length) return map;
        var row = head.rows[head.rows.length - 1];
        eachCell(row, function (th, col) { if ((th.colSpan || 1) === 1) map[col] = th; });
        return map;
    }

    function labelOf(th) {
        if (!th) return '';
        return (th.textContent || '').replace(/\*/g, '').replace(/\s+/g, ' ').trim();
    }

    var KINDS = ['wide', 'name', 'sel', 'unit', 'num', 'amt', 'pct', 'date', 'text', 'chk', 'act'];

    function setKind(cell, kind) {
        if (cell.getAttribute('data-dl-kind') === kind) return;
        for (var i = 0; i < KINDS.length; i++) cell.classList.remove('dl-c-' + KINDS[i]);
        if (kind) cell.classList.add('dl-c-' + kind);
        cell.setAttribute('data-dl-kind', kind);
    }

    function processTable(table) {
        var heads = headerCells(table);
        var colKinds = {};
        var bodies = table.tBodies;
        for (var b = 0; b < bodies.length; b++) {
            var rows = bodies[b].rows;
            for (var r = 0; r < rows.length; r++) {
                eachCell(rows[r], function (td, col) {
                    var kind = classify(td);
                    setKind(td, kind);
                    td.classList.toggle('dl-freeze', col === 0 && kind === 'wide');
                    if ((td.colSpan || 1) === 1) {
                        if (kind && !colKinds[col]) colKinds[col] = kind;
                        var lbl = labelOf(heads[col]);
                        if (td.getAttribute('data-label') !== lbl) td.setAttribute('data-label', lbl);
                    }
                    if (kind === 'num' || kind === 'amt' || kind === 'pct') bindNumbers(td, table);
                    if (kind === 'wide' || kind === 'name' || kind === 'sel') syncTitles(td);
                });
            }
        }
        for (var col in heads) {
            if (colKinds[col]) setKind(heads[col], colKinds[col]);
            heads[col].classList.toggle('dl-freeze', col === '0' && colKinds[col] === 'wide');
        }
        // tfoot kataklari sarlavha bo'yicha tekislanadi (masalan jami summa o'ngda)
        if (table.tFoot) {
            var frows = table.tFoot.rows;
            for (var f = 0; f < frows.length; f++) {
                eachCell(frows[f], function (td, col) {
                    if ((td.colSpan || 1) === 1 && /^(num|amt|pct)$/.test(colKinds[col] || '')) td.classList.add('dl-c-' + colKinds[col]);
                });
            }
        }
        updateScrollState(table.parentNode);
    }

    // ---- To'liq nom: select / disabled input uchun title (tooltip) ----
    function syncTitles(td) {
        var sel = td.querySelector('select');
        if (sel && sel.selectedIndex >= 0 && sel.options[sel.selectedIndex]) {
            var t = sel.options[sel.selectedIndex].text;
            var rendered = td.querySelector('.select2-selection__rendered');
            if (rendered && t && !rendered.getAttribute('title')) rendered.setAttribute('title', t);
        }
        var inp = td.querySelector('input[type="text"], input:not([type])');
        if (inp && inp.value && inp.title !== inp.value) inp.title = inp.value;
    }

    // ---- Ming ajratkich ----
    function formatNumber(raw) {
        var s = String(raw == null ? '' : raw).trim();
        if (!/^-?\d+(\.\d+)?$/.test(s)) return null;
        var neg = s.charAt(0) === '-';
        if (neg) s = s.slice(1);
        var parts = s.split('.');
        var int = parts[0].replace(/^0+(?=\d)/, '');
        int = int.replace(/\B(?=(\d{3})+(?!\d))/g, GROUP);
        return (neg ? '-' : '') + int + (parts.length > 1 ? '.' + parts[1] : '');
    }

    function placeOverlay(input, span) {
        var cs = window.getComputedStyle(input);
        span.style.left = input.offsetLeft + 'px';
        span.style.top = input.offsetTop + 'px';
        span.style.width = input.offsetWidth + 'px';
        span.style.height = input.offsetHeight + 'px';
        span.style.lineHeight = (input.offsetHeight - (parseFloat(cs.borderTopWidth) || 0) - (parseFloat(cs.borderBottomWidth) || 0)) + 'px';
        span.style.padding = '0 ' + (parseFloat(cs.paddingRight) + (parseFloat(cs.borderRightWidth) || 0)) + 'px 0 ' + (parseFloat(cs.paddingLeft) + (parseFloat(cs.borderLeftWidth) || 0)) + 'px';
        span.style.fontSize = cs.fontSize;
        span.style.fontFamily = cs.fontFamily;
        span.style.fontWeight = cs.fontWeight;
        span.style.color = cs.color;
        span.style.textAlign = cs.textAlign === 'left' || cs.textAlign === 'start' ? 'left' : 'right';
    }

    function refreshNumber(input) {
        var span = input.nextElementSibling;
        if (!span || !span.classList.contains('dl-fmt')) return;
        var txt = formatNumber(input.value);
        // Ajratkich kerak bo'lmasa (kichik son yoki noto'g'ri qiymat) — input o'zi ko'rinadi
        // Yashirin tabdagi jadval (o'lcham 0) — qatlam joylashtirib bo'lmaydi; ko'ringanda ResizeObserver qayta chaqiradi
        if (txt === null || txt === input.value || !input.offsetWidth) {
            input.classList.remove('dl-fmt-on');
            return;
        }
        input.classList.remove('dl-fmt-on');
        placeOverlay(input, span);
        span.textContent = txt;
        input.classList.add('dl-fmt-on');
        input.title = txt;
    }

    function bindNumbers(td, table) {
        if (table.classList.contains('doc-lines-nofmt')) return;
        var inputs = td.querySelectorAll('input[type="number"]');
        for (var i = 0; i < inputs.length; i++) {
            var input = inputs[i];
            // Qator .clone() bilan ko'paytirilgan bo'lsa ham qatlam bitta bo'lsin
            var next = input.nextElementSibling;
            if (!next || !next.classList.contains('dl-fmt')) {
                var span = document.createElement('span');
                span.className = 'dl-fmt';
                span.setAttribute('aria-hidden', 'true');
                input.parentNode.insertBefore(span, input.nextSibling);
            }
            input.setAttribute('data-dl-n', '1');
            td.classList.add('dl-has-fmt');
            refreshNumber(input);
        }
    }

    // Hodisalar jadval darajasida (delegatsiya) — JS bilan qo'shilgan/klonlangan qatorlar ham qamraladi
    function bindTableEvents(table) {
        function onNumber(e) {
            var t = e.target;
            if (t && t.tagName === 'INPUT' && t.getAttribute('data-dl-n')) refreshNumber(t);
        }
        table.addEventListener('input', onNumber);
        table.addEventListener('change', onNumber);
        table.addEventListener('focusout', onNumber);
        table.addEventListener('change', function (e) {
            var t = e.target;
            if (t && t.tagName === 'SELECT') {
                var td = t.closest ? t.closest('td') : null;
                if (td) {
                    var r = td.querySelector('.select2-selection__rendered');
                    if (r) r.removeAttribute('title');
                    syncTitles(td);
                }
            }
        });
    }

    // jQuery .val(x) number input'ga yozganda ham ko'rinish yangilansin
    function hookJQuery() {
        var $ = window.jQuery;
        if (!$ || !$.valHooks || $.valHooks.__erpLineEditor) return;
        var prev = $.valHooks.number;
        $.valHooks.number = {
            get: prev && prev.get ? prev.get : undefined,
            set: function (elem, value, name) {
                var res = prev && prev.set ? prev.set(elem, value, name) : undefined;
                if (res === undefined) elem.value = value;
                if (elem.getAttribute && elem.getAttribute('data-dl-n')) refreshNumber(elem);
                return true;
            }
        };
        if (!$.valHooks.number.get) delete $.valHooks.number.get;
        $.valHooks.__erpLineEditor = true;
    }

    // ---- Skroll holati (amallar ustuni soyasi) ----
    function updateScrollState(wrap) {
        if (!wrap || !wrap.classList || !wrap.classList.contains('dl-wrap')) return;
        var hasX = wrap.scrollWidth > wrap.clientWidth + 1;
        wrap.classList.toggle('dl-scroll-x', hasX);
        wrap.classList.toggle('dl-at-end', !hasX || wrap.scrollLeft + wrap.clientWidth >= wrap.scrollWidth - 1);
        wrap.classList.toggle('dl-scrolled', hasX && wrap.scrollLeft > 0);
    }

    function ensureWrap(table) {
        var parent = table.parentNode;
        var wrap;
        // .table-responsive ichidagi jadval (ba'zan <tr> ichidagi hidden input'lar parser tomonidan jadvaldan oldinga chiqariladi)
        if (parent && parent.classList && (parent.classList.contains('table-responsive') || parent.classList.contains('dl-wrap'))) {
            wrap = parent;
        } else {
            wrap = document.createElement('div');
            parent.insertBefore(wrap, table);
            wrap.appendChild(table);
        }
        wrap.classList.add('dl-wrap');
        if (!table.classList.contains('doc-lines-nocards')) wrap.classList.add('dl-cards');
        wrap.addEventListener('scroll', function () { updateScrollState(wrap); }, { passive: true });
        return wrap;
    }

    var tables = [];

    function init(target) {
        hookJQuery();
        var list = typeof target === 'string' ? document.querySelectorAll(target)
            : target ? [target.jquery ? target[0] : target] : document.querySelectorAll('table.doc-lines');
        for (var i = 0; i < list.length; i++) {
            var table = list[i];
            if (!table || table.__dlInit) continue;
            table.__dlInit = true;
            if (!table.classList.contains('doc-lines')) table.classList.add('doc-lines');
            ensureWrap(table);
            bindTableEvents(table);
            processTable(table);
            observe(table);
            tables.push(table);
        }
    }

    function observe(table) {
        if (!window.MutationObserver) return;
        var pending = false;
        var obs = new MutationObserver(function (muts) {
            // O'zimiz qo'shgan .dl-fmt / atributlar qayta ishga tushirmasin
            var relevant = false;
            for (var i = 0; i < muts.length && !relevant; i++) {
                var m = muts[i];
                if (m.type !== 'childList') continue;
                var nodes = [].slice.call(m.addedNodes).concat([].slice.call(m.removedNodes));
                for (var j = 0; j < nodes.length; j++) {
                    var n = nodes[j];
                    if (n.nodeType === 1 && !(n.classList && n.classList.contains('dl-fmt'))) { relevant = true; break; }
                }
            }
            if (!relevant || pending) return;
            pending = true;
            (window.requestAnimationFrame || setTimeout)(function () {
                pending = false;
                processTable(table);
            });
        });
        obs.observe(table, { childList: true, subtree: true });
        // Tab ochilganda / kenglik o'zgarganda (kartochka rejimi) qatlam joyi qayta hisoblanadi
        if (window.ResizeObserver) {
            var lastW = -1, timer;
            new ResizeObserver(function () {
                var w = table.offsetWidth;
                if (w === lastW) return;
                lastW = w;
                clearTimeout(timer);
                timer = setTimeout(function () { processTable(table); }, 60);
            }).observe(table);
        }
    }

    function refresh(target) {
        var list = target ? [typeof target === 'string' ? document.querySelector(target) : (target.jquery ? target[0] : target)] : tables;
        for (var i = 0; i < list.length; i++) if (list[i]) processTable(list[i]);
    }

    var resizeTimer;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () { refresh(); }, 150);
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { init(); });
    } else {
        init();
    }
    // Sahifa skriptlari DOMContentLoaded'dan keyin klass qo'shsa yoki jadvalni almashtirsa
    window.addEventListener('load', function () { init(); refresh(); });

    window.erpLineEditor = { init: init, refresh: refresh, format: formatNumber };
})(window, document);
