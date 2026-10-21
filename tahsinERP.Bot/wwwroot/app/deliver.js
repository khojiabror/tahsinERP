// tahsinERP — ekspeditorning yetkazish oynasi (Telegram Mini App): mijoz imzosi, qisman yetkazish miqdorlari, rad sababi.
// Server: /app/api/{tenant}/stop/{id} (initData sarlavhada). Natija ERP'da (StopDeliver), chatga tasdiq xabari keladi.
(function () {
    'use strict';

    var tg = window.Telegram && window.Telegram.WebApp;
    var params = new URLSearchParams(location.search);
    var tenant = params.get('t') || '';
    var stopId = parseInt(params.get('s'), 10) || 0;
    var api = '/app/api/' + encodeURIComponent(tenant);

    var L = {
        uz: {
            reason: "Sabab (qisman yoki rad bo'lsa)", note: 'Izoh', sign: 'Mijoz imzosi', clear: 'Tozalash', total: 'Yetkaziladi',
            deliver: 'Tasdiqlash', refuse: 'Mijoz butunlay rad etdi', refuseConfirm: 'Rad etilgani qayd qilinsinmi?', sum: "so'm",
            needSign: 'Mijoz imzosini oling.', needReason: 'Qisman yetkazish yoki rad uchun sababni tanlang.', done: 'Saqlandi', error: 'Xatolik',
            notTelegram: 'Bu sahifa Telegram ichida ochiladi.', shipped: "jo'natilgan", closed: "Bu manzil bo'yicha natija allaqachon kiritilgan."
        },
        ru: {
            reason: 'Причина (если частично или отказ)', note: 'Комментарий', sign: 'Подпись клиента', clear: 'Очистить', total: 'К доставке',
            deliver: 'Подтвердить', refuse: 'Клиент полностью отказался', refuseConfirm: 'Зарегистрировать отказ?', sum: 'сум',
            needSign: 'Получите подпись клиента.', needReason: 'Для частичной доставки или отказа выберите причину.', done: 'Сохранено', error: 'Ошибка',
            notTelegram: 'Эта страница открывается внутри Telegram.', shipped: 'отгружено', closed: 'По этому адресу результат уже внесён.'
        }
    };
    var lang = 'uz';
    function t(k) { return (L[lang] && L[lang][k]) || L.uz[k] || k; }
    var $ = function (id) { return document.getElementById(id); };
    var state = { signed: false, sending: false };

    function money(v) { return Math.round(v || 0).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' '); }
    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }
    var toastTimer;
    function toast(text, isError) {
        var x = $('toast');
        x.textContent = text;
        x.className = 'toast' + (isError ? ' err' : '');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { x.className = 'toast hidden'; }, 3500);
    }
    function request(method, path, body) {
        var headers = { 'X-Telegram-Init-Data': tg ? tg.initData : '' };
        if (body) headers['Content-Type'] = 'application/json';
        return fetch(api + path, { method: method, headers: headers, body: body ? JSON.stringify(body) : undefined }).then(function (r) {
            return r.text().then(function (txt) {
                var data = null;
                try { data = txt ? JSON.parse(txt) : null; } catch (e) { data = null; }
                if (!r.ok) throw new Error((data && data.error) || (t('error') + ' ' + r.status));
                return data;
            });
        });
    }

    function inputs() { return Array.prototype.slice.call(document.querySelectorAll('[data-line]')); }
    function num(i) { return parseFloat(String(i.value).replace(',', '.')) || 0; }
    function totals() {
        var sum = 0, all = true;
        inputs().forEach(function (i) {
            var q = Math.max(0, num(i)), max = +i.getAttribute('data-max'), amount = +i.getAttribute('data-amount');
            if (q < max) all = false;
            sum += max > 0 ? amount * Math.min(q, max) / max : 0;
        });
        $('total').textContent = money(sum) + ' ' + t('sum');
        return { sum: sum, all: all };
    }

    function render(s) {
        $('custName').textContent = s.seq + '. ' + s.customer;
        $('custInfo').textContent = (s.orderNo || '') + ' · ' + money(s.amount) + ' ' + t('sum');
        var box = $('lines');
        box.innerHTML = '';
        (s.lines || []).forEach(function (l) {
            var row = el('div', 'item');
            var main = el('div', 'item-main');
            main.appendChild(el('div', 'item-name', l.product));
            main.appendChild(el('div', 'item-meta', l.qty + ' ' + (l.unit || '') + ' ' + t('shipped') + ' · ' + money(l.amount)));
            var input = el('input', 'qty-input');
            input.type = 'number'; input.min = '0'; input.step = 'any'; input.value = l.qty; input.style.width = '80px';
            input.setAttribute('data-line', l.id); input.setAttribute('data-max', l.qty); input.setAttribute('data-amount', l.amount);
            input.addEventListener('input', totals);
            row.appendChild(main);
            row.appendChild(input);
            box.appendChild(row);
        });
        totals();
        if (s.status !== 0) {
            toast(t('closed'), true);
            if (tg && tg.MainButton) tg.MainButton.hide();
        }
    }

    function setupSignature() {
        var c = $('sign'), ctx = c.getContext('2d'), drawing = false;
        ctx.lineWidth = 3; ctx.lineCap = 'round'; ctx.strokeStyle = '#111';
        function pos(e) { var r = c.getBoundingClientRect(); return { x: (e.clientX - r.left) * c.width / r.width, y: (e.clientY - r.top) * c.height / r.height }; }
        c.addEventListener('pointerdown', function (e) { drawing = true; c.setPointerCapture(e.pointerId); var p = pos(e); ctx.beginPath(); ctx.moveTo(p.x, p.y); });
        c.addEventListener('pointermove', function (e) { if (!drawing) return; var p = pos(e); ctx.lineTo(p.x, p.y); ctx.stroke(); state.signed = true; });
        c.addEventListener('pointerup', function () { drawing = false; });
        c.addEventListener('pointercancel', function () { drawing = false; });
        $('signClear').addEventListener('click', function () { ctx.clearRect(0, 0, c.width, c.height); state.signed = false; });
    }

    function send(refuse) {
        if (state.sending) return;
        var tt = totals();
        var reasonId = $('reason').value ? +$('reason').value : null;
        if (!refuse && !state.signed) { toast(t('needSign'), true); return; }
        if ((refuse || !tt.all) && !reasonId) { toast(t('needReason'), true); return; }
        state.sending = true;
        if (tg && tg.MainButton) tg.MainButton.showProgress(false);
        var signature = state.signed ? $('sign').toDataURL('image/png') : null;
        var body = refuse
            ? { all: false, lines: [], reasonId: reasonId, note: $('note').value || null, signature: signature }
            : {
                all: tt.all, reasonId: tt.all ? null : reasonId, note: $('note').value || null, signature: signature,
                lines: inputs().map(function (i) { return { lineId: +i.getAttribute('data-line'), qty: Math.min(Math.max(0, num(i)), +i.getAttribute('data-max')) }; })
            };
        request('POST', '/stop/' + stopId + '/deliver', body).then(function () {
            if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred('success');
            toast(t('done'));
            setTimeout(function () { if (tg) tg.close(); }, 800);
        }).catch(function (e) {
            if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred('error');
            toast(e.message, true);
        }).then(function () {
            state.sending = false;
            if (tg && tg.MainButton) tg.MainButton.hideProgress();
        });
    }

    function labels() {
        $('lblReason').textContent = t('reason'); $('lblNote').textContent = t('note'); $('lblSign').textContent = t('sign');
        $('signClear').textContent = t('clear'); $('lblTotal').textContent = t('total'); $('refuseBtn').textContent = t('refuse');
        if (tg && tg.MainButton) tg.MainButton.setText(t('deliver'));
    }

    function init() {
        if (!tg || !tg.initData) { document.body.textContent = t('notTelegram'); return; }
        tg.ready();
        tg.expand();
        var user = tg.initDataUnsafe && tg.initDataUnsafe.user;
        lang = user && /^ru/.test(user.language_code || '') ? 'ru' : 'uz';
        labels();
        setupSignature();
        request('GET', '/me').then(function (me) { if (me && me.language) { lang = me.language === 'ru' ? 'ru' : 'uz'; labels(); totals(); } }).catch(function () { });
        request('GET', '/reasons').then(function (list) {
            var sel = $('reason');
            var none = el('option', null, '—'); none.value = ''; sel.appendChild(none);
            (list || []).forEach(function (r) { var o = el('option', null, r.name); o.value = r.id; sel.appendChild(o); });
        }).catch(function (e) { toast(e.message, true); });
        request('GET', '/stop/' + stopId).then(function (r) { render(r.stop); }).catch(function (e) { toast(e.message, true); });
        tg.MainButton.onClick(function () { send(false); });
        tg.MainButton.show();
        $('refuseBtn').addEventListener('click', function () {
            if (tg.showConfirm) tg.showConfirm(t('refuseConfirm'), function (ok) { if (ok) send(true); });
            else if (window.confirm(t('refuseConfirm'))) send(true);
        });
    }
    init();
})();
