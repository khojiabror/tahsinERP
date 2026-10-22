// tahsinERP — omborchi (Telegram Mini App): shtrix-kod bilan inventarizatsiya va buyurtma yig'ish.
// Server: /app/api/{tenant}/store/... (initData sarlavhada). Sanoq "ko'r" — tizimdagi qoldiq ko'rsatilmaydi.
(function () {
    'use strict';

    var tg = window.Telegram && window.Telegram.WebApp;
    var params = new URLSearchParams(location.search);
    var tenant = params.get('t') || '';
    var api = '/app/api/' + encodeURIComponent(tenant) + '/store';

    var L = {
        uz: {
            counts: 'Inventarizatsiya', orders: "Yig'ish", noCounts: "Ochiq sanoq yo'q (ERP'da yarating).", noOrders: "Yig'iladigan buyurtma yo'q.",
            code: 'Shtrix-kod yoki artikul', counted: 'Sanaldi', of: 'dan', back: '← Ro\'yxatga', notTelegram: 'Bu sahifa Telegram ichida ochiladi.',
            scan: 'Shtrix-kodni kameraga tuting', scanDone: 'Tayyor', notFound: 'Topilmadi: {0}', notInOrder: "Buyurtmada yo'q: {0}",
            tooMuch: 'Ortiqcha: {0}', added: '+{1} {0}', start: "Yig'ishni boshlash", ship: "Jo'natish", shipConfirm: "Yig'ilgan miqdor jo'natiladi. Davom etasizmi?",
            shipped: "Jo'natildi: {0}", nothingPicked: "Hali hech narsa yig'ilmadi.", picked: "Yig'ildi", remaining: 'kerak', error: 'Xatolik',
            scanNoCamera: "Kameraga ruxsat berilmadi yoki kamera yo'q.", scanUnsupported: "Bu qurilmada skaner ishlamaydi — kodni yozing.",
            lines: 'qator', setQty: 'Miqdor (bo\'sh — sanalmagan):', status_Confirmed: 'tasdiqlangan', status_Picking: "yig'ilmoqda", status_PartiallyShipped: "qisman jo'natilgan"
        },
        ru: {
            counts: 'Инвентаризация', orders: 'Сборка', noCounts: 'Нет открытых пересчётов (создайте в ERP).', noOrders: 'Нет заказов для сборки.',
            code: 'Штрих-код или артикул', counted: 'Посчитано', of: 'из', back: '← К списку', notTelegram: 'Эта страница открывается внутри Telegram.',
            scan: 'Наведите камеру на штрих-код', scanDone: 'Готово', notFound: 'Не найдено: {0}', notInOrder: 'Нет в заказе: {0}',
            tooMuch: 'Лишнее: {0}', added: '+{1} {0}', start: 'Начать сборку', ship: 'Отгрузить', shipConfirm: 'Собранное количество будет отгружено. Продолжить?',
            shipped: 'Отгружено: {0}', nothingPicked: 'Ещё ничего не собрано.', picked: 'Собрано', remaining: 'нужно', error: 'Ошибка',
            scanNoCamera: 'Нет доступа к камере или камеры нет.', scanUnsupported: 'Сканер не работает на этом устройстве — введите код.',
            lines: 'строк', setQty: 'Количество (пусто — не посчитано):', status_Confirmed: 'подтверждён', status_Picking: 'собирается', status_PartiallyShipped: 'частично отгружен'
        }
    };
    var lang = 'uz';
    function t(k) { return lang === 'oz' ? window.uzCyrl(L.uz[k] || k) : (L[lang] && L[lang][k]) || L.uz[k] || k; }
    function f(k) { var a = arguments; return t(k).replace(/\{(\d)\}/g, function (m, i) { return a[+i + 1]; }); }

    var $ = function (id) { return document.getElementById(id); };
    var state = { view: 'counts', doc: null, kind: null, picked: {}, busy: false };

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }
    function qty(v) { return v === null || v === undefined ? '—' : (Math.round(v * 1000) / 1000).toString(); }
    var toastTimer;
    function toast(text, isError) {
        var x = $('toast');
        x.textContent = text;
        x.className = 'toast' + (isError ? ' err' : '');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { x.className = 'toast hidden'; }, 3000);
    }
    function haptic(kind) { if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred(kind); }

    function request(method, path, body) {
        var headers = { 'X-Telegram-Init-Data': tg ? tg.initData : '' };
        if (body) headers['Content-Type'] = 'application/json';
        return fetch(api + path, { method: method, headers: headers, body: body ? JSON.stringify(body) : undefined })
            .then(function (r) {
                return r.text().then(function (txt) {
                    var data = null;
                    try { data = txt ? JSON.parse(txt) : null; } catch (e) { data = null; }
                    if (!r.ok) throw new Error((data && data.error) || (t('error') + ' ' + r.status));
                    return data;
                });
            });
    }

    // ---------- ro'yxatlar ----------

    function show(view) {
        state.view = view;
        $('viewCounts').classList.toggle('hidden', view !== 'counts');
        $('viewOrders').classList.toggle('hidden', view !== 'orders');
        $('viewDoc').classList.toggle('hidden', view !== 'doc');
        $('tabs').classList.toggle('hidden', view === 'doc');
        Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (b) { b.classList.toggle('active', b.dataset.view === view); });
        if (tg && tg.BackButton) { if (view === 'doc') tg.BackButton.show(); else tg.BackButton.hide(); }
        updateMainButton();
    }

    function loadCounts() {
        return request('GET', '/counts').then(function (list) {
            var box = $('countList'); box.textContent = '';
            list = list || [];
            $('countsEmpty').classList.toggle('hidden', list.length > 0);
            list.forEach(function (c) {
                var row = el('div', 'item');
                var main = el('div', 'item-main');
                main.appendChild(el('div', 'item-name', c.docNo + ' — ' + c.warehouse));
                main.appendChild(el('div', 'item-meta', t('counted') + ': ' + c.counted + ' / ' + c.lines + ' ' + t('lines')));
                row.appendChild(main);
                row.addEventListener('click', function () { openCount(c.id); });
                box.appendChild(row);
            });
        }).catch(function (e) { toast(e.message, true); });
    }

    function loadOrders() {
        return request('GET', '/orders').then(function (list) {
            var box = $('orderList'); box.textContent = '';
            list = list || [];
            $('ordersEmpty').classList.toggle('hidden', list.length > 0);
            list.forEach(function (o) {
                var row = el('div', 'item');
                var main = el('div', 'item-main');
                main.appendChild(el('div', 'item-name', o.docNo + ' — ' + o.customer));
                main.appendChild(el('div', 'item-meta', t('status_' + o.status) + (o.delivery ? ' · ' + o.delivery : '') + (o.weightKg ? ' · ' + qty(o.weightKg) + ' kg' : '')));
                row.appendChild(main);
                row.addEventListener('click', function () { openOrder(o.id); });
                box.appendChild(row);
            });
        }).catch(function (e) { toast(e.message, true); });
    }

    // ---------- inventarizatsiya ----------

    function openCount(id) {
        request('GET', '/counts/' + id).then(function (doc) {
            state.doc = doc; state.kind = 'count';
            $('title').textContent = doc.docNo;
            $('subtitle').textContent = doc.warehouse;
            renderCount();
            show('doc');
        }).catch(function (e) { toast(e.message, true); });
    }

    function renderCount() {
        var doc = state.doc, box = $('docLines');
        box.textContent = '';
        var done = doc.lines.filter(function (l) { return l.counted !== null && l.counted !== undefined; }).length;
        $('docProgress').textContent = t('counted') + ': ' + done + ' / ' + doc.lines.length;
        // sanalganlar tepada (oxirgi skanerlangan birinchi)
        var lines = doc.lines.slice().sort(function (a, b) { return (b.touched || 0) - (a.touched || 0); });
        lines.forEach(function (l) {
            var row = el('div', 'item');
            var main = el('div', 'item-main');
            main.appendChild(el('div', 'item-name', l.name));
            main.appendChild(el('div', 'item-meta', l.pno + (l.barcodes && l.barcodes.length ? ' · ' + l.barcodes[0] : '')));
            row.appendChild(main);
            var v = el('b', null, qty(l.counted) + ' ' + (l.unit || ''));
            row.appendChild(v);
            row.addEventListener('click', function () {
                var input = window.prompt(l.name + '\n' + t('setQty'), l.counted === null || l.counted === undefined ? '' : l.counted);
                if (input === null) return;
                var val = input.trim() === '' ? null : parseFloat(input.replace(',', '.'));
                if (val !== null && (isNaN(val) || val < 0)) return;
                request('POST', '/counts/' + doc.id + '/set', { lineId: l.lineId, counted: val }).then(function () {
                    l.counted = val; l.touched = Date.now(); renderCount();
                }).catch(function (e) { toast(e.message, true); });
            });
            box.appendChild(row);
        });
    }

    function countScan(code, n) {
        var doc = state.doc;
        return request('POST', '/counts/' + doc.id + '/scan', { code: code, qty: n }).then(function (r) {
            var line = doc.lines.filter(function (l) { return l.lineId === r.lineId; })[0];
            if (!line) {
                line = { lineId: r.lineId, productId: r.productId, name: r.name, pno: '', unit: '', barcodes: [code] };
                doc.lines.push(line);
            }
            line.counted = r.counted; line.touched = Date.now();
            renderCount();
            haptic('success');
            return f('added', r.name, qty(r.added));
        });
    }

    // ---------- yig'ish ----------

    // yig'ish natijasi har o'zgarishda localStorage'ga (oyna yopilsa yo'qolmasin), jo'natilgach o'chiriladi (05_FINDINGS K2-23)
    var pickTtlMs = 2 * 24 * 3600 * 1000;
    function pickKey(id) { return 'dst-pick-' + tenant + '-' + id; }
    function savePicked() {
        try { localStorage.setItem(pickKey(state.doc.id), JSON.stringify({ at: Date.now(), picked: state.picked })); } catch (e) { /* saqlanmasa ham ishlaydi */ }
    }
    function loadPicked(doc) {
        var picked = {};
        try {
            var raw = JSON.parse(localStorage.getItem(pickKey(doc.id)) || 'null');
            if (raw && raw.picked && Date.now() - (raw.at || 0) < pickTtlMs)
                doc.lines.forEach(function (l) {   // qolgan miqdordan oshmasin (boshqa joyda qisman jo'natilgan bo'lishi mumkin)
                    var v = raw.picked[l.lineId];
                    if (v > 0) picked[l.lineId] = Math.min(v, l.remaining);
                });
        } catch (e) { /* bo'sh boshlanadi */ }
        return picked;
    }
    function clearPicked(id) { try { localStorage.removeItem(pickKey(id)); } catch (e) { /* jim */ } }

    function openOrder(id) {
        request('GET', '/orders/' + id).then(function (doc) {
            state.doc = doc; state.kind = 'order'; state.picked = loadPicked(doc);
            $('title').textContent = doc.docNo + ' — ' + doc.customer;
            $('subtitle').textContent = (doc.warehouse || '') + ' · ' + t('status_' + doc.status);
            renderOrder();
            show('doc');
        }).catch(function (e) { toast(e.message, true); });
    }

    function pickedOf(l) { return state.picked[l.lineId] || 0; }

    function renderOrder() {
        var doc = state.doc, box = $('docLines');
        box.textContent = '';
        var full = doc.lines.filter(function (l) { return pickedOf(l) >= l.remaining; }).length;
        $('docProgress').textContent = t('picked') + ': ' + full + ' / ' + doc.lines.length;
        doc.lines.forEach(function (l) {
            var p = pickedOf(l);
            var row = el('div', 'item' + (p >= l.remaining ? ' done' : ''));
            var main = el('div', 'item-main');
            main.appendChild(el('div', 'item-name', (p >= l.remaining ? '✅ ' : '') + l.name));
            main.appendChild(el('div', 'item-meta', l.pno + ' · ' + t('remaining') + ' ' + qty(l.remaining) + ' ' + (l.unit || '') +
                (l.factor !== 1 ? ' (' + qty(l.remaining / l.factor) + ' ' + (l.saleUnit || '') + ')' : '')));
            row.appendChild(main);
            var st = el('div', 'stepper');
            var minus = el('button', null, '−'); minus.type = 'button';
            var val = el('input'); val.type = 'number'; val.min = '0'; val.step = 'any'; val.value = qty(p);
            var plus = el('button', null, '+'); plus.type = 'button';
            minus.addEventListener('click', function () { setPicked(l, Math.max(0, pickedOf(l) - 1)); });
            plus.addEventListener('click', function () { setPicked(l, pickedOf(l) + 1); });
            val.addEventListener('change', function () { var x = parseFloat(val.value.replace(',', '.')); setPicked(l, isNaN(x) || x < 0 ? 0 : x); });
            st.appendChild(minus); st.appendChild(val); st.appendChild(plus);
            row.appendChild(st);
            box.appendChild(row);
        });
        updateMainButton();
    }

    function setPicked(l, v) {
        state.picked[l.lineId] = Math.min(Math.round(v * 10000) / 10000, l.remaining);
        savePicked();
        renderOrder();
    }

    function orderScan(code, n) {
        return request('GET', '/resolve?code=' + encodeURIComponent(code)).then(function (r) {
            var lines = state.doc.lines.filter(function (l) { return l.productId === r.productId; });
            if (lines.length === 0) { haptic('error'); throw new Error(f('notInOrder', r.name)); }
            var add = n * (r.factor || 1);
            var line = lines.filter(function (l) { return pickedOf(l) < l.remaining; })[0];
            if (!line) { haptic('warning'); throw new Error(f('tooMuch', r.name)); }
            var room = line.remaining - pickedOf(line);
            if (add > room + 1e-9) { setPicked(line, line.remaining); haptic('warning'); throw new Error(f('tooMuch', r.name + ' +' + qty(add - room))); }
            setPicked(line, pickedOf(line) + add);
            haptic('success');
            return f('added', r.name, qty(add));
        });
    }

    function startPicking() {
        return request('POST', '/orders/' + state.doc.id + '/start').then(function () {
            state.doc.status = 'Picking';
            $('subtitle').textContent = (state.doc.warehouse || '') + ' · ' + t('status_Picking');
            updateMainButton();
        });
    }

    function ship() {
        var lines = state.doc.lines.filter(function (l) { return pickedOf(l) > 0; })
            .map(function (l) { return { lineId: l.lineId, qtyBase: pickedOf(l) }; });
        if (lines.length === 0) { toast(t('nothingPicked'), true); return; }
        var go = function (ok) {
            if (!ok || state.busy) return;
            state.busy = true;
            if (tg && tg.MainButton) tg.MainButton.showProgress(false);
            var shippedId = state.doc.id;
            request('POST', '/orders/' + shippedId + '/ship', { lines: lines }).then(function (r) {
                clearPicked(shippedId);
                haptic('success');
                toast(f('shipped', r.docNo));
                show('orders'); loadOrders();
            }).catch(function (e) { haptic('error'); toast(e.message, true); }).then(function () {
                state.busy = false;
                if (tg && tg.MainButton) tg.MainButton.hideProgress();
            });
        };
        if (tg && tg.showConfirm) tg.showConfirm(t('shipConfirm'), go); else go(window.confirm(t('shipConfirm')));
    }

    function updateMainButton() {
        if (!tg || !tg.MainButton) return;
        if (state.view === 'doc' && state.kind === 'order') {
            tg.MainButton.setText(state.doc.status === 'Confirmed' ? t('start') : t('ship'));
            tg.MainButton.show();
        } else tg.MainButton.hide();
    }

    // ---------- kod kiritish va skaner ----------

    function handleCode(code) {
        code = (code || '').trim();
        if (!code || !state.doc) return Promise.resolve();
        var n = parseFloat(($('codeQty').value || '1').replace(',', '.'));
        if (isNaN(n) || n <= 0) n = 1;
        var p = state.kind === 'count' ? countScan(code, n) : orderScan(code, n);
        return p.then(function (msg) { $('scanStatus').textContent = msg; toast(msg); return msg; })
            .catch(function (e) { $('scanStatus').textContent = e.message; toast(e.message, true); });
    }

    var lastScan = { code: null, at: 0 };
    function onScanned(code) {
        var now = Date.now();
        if (!code) return;
        var same = code === lastScan.code && now - lastScan.at < 1500;
        lastScan = { code: code, at: now };
        if (same) return;
        handleCode(code);
    }
    function openScanner() {
        if (!window.DstScanner) { toast(t('scanUnsupported'), true); return; }
        $('scanner').classList.remove('hidden');
        $('scanStatus').textContent = t('scan');
        window.DstScanner.start($('scanVideo'), onScanned).catch(function (e) {
            closeScanner();
            toast(t(e && e.message === 'camera' ? 'scanNoCamera' : 'scanUnsupported'), true);
        });
    }
    function closeScanner() {
        if (window.DstScanner) window.DstScanner.stop();
        $('scanner').classList.add('hidden');
    }

    // ---------- ishga tushirish ----------

    function applyTexts() {
        document.documentElement.lang = lang === 'oz' ? 'uz-Cyrl' : lang;
        $('tabCounts').textContent = t('counts');
        $('tabOrders').textContent = t('orders');
        $('countsEmpty').textContent = t('noCounts');
        $('ordersEmpty').textContent = t('noOrders');
        $('code').placeholder = t('code');
        $('backBtn').textContent = t('back');
        $('scanClose').textContent = t('scanDone');
    }

    function backToList() {
        closeScanner();
        $('title').textContent = t(state.kind === 'order' ? 'orders' : 'counts');
        $('subtitle').textContent = '';
        var v = state.kind === 'order' ? 'orders' : 'counts';
        state.doc = null;
        show(v);
        if (v === 'orders') loadOrders(); else loadCounts();
    }

    function init() {
        if (!tg || !tg.initData) {
            $('title').textContent = t('notTelegram');
            $('tabs').classList.add('hidden');
            document.querySelector('main').classList.add('hidden');
            return;
        }
        tg.ready();
        tg.expand();
        lang = (tg.initDataUnsafe && tg.initDataUnsafe.user && /^ru/.test(tg.initDataUnsafe.user.language_code || '')) ? 'ru' : 'uz';
        applyTexts();
        $('title').textContent = t('counts');

        Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (b) {
            b.addEventListener('click', function () {
                show(b.dataset.view);
                $('title').textContent = t(b.dataset.view);
                if (b.dataset.view === 'orders') loadOrders(); else loadCounts();
            });
        });
        $('code').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); handleCode($('code').value).then(function () { $('code').value = ''; }); }
        });
        $('scanBtn').addEventListener('click', openScanner);
        $('scanClose').addEventListener('click', closeScanner);
        $('backBtn').addEventListener('click', backToList);
        tg.BackButton.onClick(backToList);
        tg.MainButton.onClick(function () {
            if (state.kind !== 'order' || !state.doc) return;
            if (state.doc.status === 'Confirmed') startPicking().catch(function (e) { toast(e.message, true); });
            else ship();
        });
        document.addEventListener('visibilitychange', function () { if (document.hidden) closeScanner(); });

        fetch('/app/api/' + encodeURIComponent(tenant) + '/me', { headers: { 'X-Telegram-Init-Data': tg.initData } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (me) { if (me && (me.language === 'ru' || me.language === 'uz' || me.language === 'oz')) { lang = me.language; applyTexts(); $('title').textContent = t('counts'); } })
            .catch(function () { })
            .then(function () { show('counts'); loadCounts(); });
    }

    init();
})();
