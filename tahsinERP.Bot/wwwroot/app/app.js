// tahsinERP — agent buyurtmasi (Telegram Mini App). Server: /app/api/{tenant}/... (initData sarlavhada).
(function () {
    'use strict';

    var tg = window.Telegram && window.Telegram.WebApp;
    var params = new URLSearchParams(location.search);
    var tenant = params.get('t') || '';
    var customerId = parseInt(params.get('c'), 10) || 0;
    var visitId = parseInt(params.get('v'), 10) || null;
    var api = '/app/api/' + encodeURIComponent(tenant);

    var L = {
        uz: {
            catalog: 'Katalog', cart: 'Savat', search: 'Nomi, artikul yoki shtrix-kod', all: 'Hammasi', add: "Qo'shish",
            available: 'Qoldiq', noPrice: "narx yo'q", empty: 'Hech narsa topilmadi', cartEmpty: "Savat bo'sh",
            date: 'Yetkazish sanasi', comment: 'Izoh', sign: 'Mijoz imzosi (ixtiyoriy)', clear: 'Tozalash', total: 'Jami',
            promoNote: "Aksiya chegirmasi va sovg'alar buyurtma qabul qilinganda hisoblanadi.",
            remove: "O'chirish", submit: 'Buyurtma berish', debt: 'Qarz', overdue: "muddati o'tgan", recommended: 'Tavsiya',
            sent: 'Buyurtma {0} qabul qilindi', error: 'Xatolik', sum: "so'm", notTelegram: "Bu sahifa Telegram ichida ochiladi.",
            scan: 'Shtrix-kodni kameraga tuting', scanDone: 'Tayyor', scanAdded: "Qo'shildi: {0}", scanNotFound: 'Topilmadi: {0}',
            scanNoCamera: "Kameraga ruxsat berilmadi yoki kamera yo'q.", scanUnsupported: "Bu qurilmada skaner ishlamaydi — kodni qidiruvga yozing."
        },
        ru: {
            catalog: 'Каталог', cart: 'Корзина', search: 'Название, артикул или штрих-код', all: 'Все', add: 'Добавить',
            available: 'Остаток', noPrice: 'нет цены', empty: 'Ничего не найдено', cartEmpty: 'Корзина пуста',
            date: 'Дата доставки', comment: 'Комментарий', sign: 'Подпись клиента (необязательно)', clear: 'Очистить', total: 'Итого',
            promoNote: 'Скидки и подарки по акциям рассчитываются при приёме заказа.',
            remove: 'Удалить', submit: 'Оформить заказ', debt: 'Долг', overdue: 'просрочено', recommended: 'Рекомендация',
            sent: 'Заказ {0} принят', error: 'Ошибка', sum: 'сум', notTelegram: 'Эта страница открывается внутри Telegram.',
            scan: 'Наведите камеру на штрих-код', scanDone: 'Готово', scanAdded: 'Добавлено: {0}', scanNotFound: 'Не найдено: {0}',
            scanNoCamera: 'Нет доступа к камере или камеры нет.', scanUnsupported: 'Сканер не работает на этом устройстве — введите код в поиск.'
        }
    };
    var lang = 'uz';
    function t(k) { return (L[lang] && L[lang][k]) || L.uz[k] || k; }

    var $ = function (id) { return document.getElementById(id); };
    var state = { items: {}, cart: [], category: null, query: '', signed: false, sending: false };
    var cartKey = 'dst-cart-' + tenant + '-' + customerId;

    // ---------- yordamchi ----------

    function money(v) {
        if (v === null || v === undefined) return '—';
        return Math.round(v).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
    }
    function qty(v) { return (Math.round(v * 100) / 100).toString(); }
    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }
    function uuid() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 3 | 8)).toString(16);
        });
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
        return fetch(api + path, { method: method, headers: headers, body: body ? JSON.stringify(body) : undefined })
            .then(function (r) {
                return r.text().then(function (txt) {
                    var data = null;
                    try { data = txt ? JSON.parse(txt) : null; } catch (e) { data = null; }
                    if (!r.ok) {
                        var msg = (data && data.error) || (t('error') + ' ' + r.status);
                        if (data && data.problems && data.problems.length) msg += '\n' + data.problems.join('\n');
                        throw new Error(msg);
                    }
                    return data;
                });
            });
    }

    function saveCart() {
        try { sessionStorage.setItem(cartKey, JSON.stringify(state.cart)); } catch (e) { /* saqlanmasa ham ishlaydi */ }
    }
    function loadCart() {
        try { state.cart = JSON.parse(sessionStorage.getItem(cartKey) || '[]') || []; } catch (e) { state.cart = []; }
    }

    // ---------- katalog ----------

    function defaultUnit(item) {
        var units = item.units || [];
        for (var i = 0; i < units.length; i++) if (units[i].isDefault) return units[i];
        return units[0] || { productUnitId: null, name: '', factor: 1, price: null };
    }
    function unitById(item, id) {
        var units = item.units || [];
        for (var i = 0; i < units.length; i++) if ((units[i].productUnitId || null) === (id || null)) return units[i];
        return defaultUnit(item);
    }
    function cartLine(productId) {
        for (var i = 0; i < state.cart.length; i++) if (state.cart[i].productId === productId) return state.cart[i];
        return null;
    }

    var searchTimer;
    function loadCatalog() {
        var q = '?customerId=' + customerId;
        if (state.query) q += '&q=' + encodeURIComponent(state.query);
        if (state.category) q += '&categoryId=' + state.category;
        return request('GET', '/catalog' + q).then(function (list) {
            (list || []).forEach(function (it) { state.items[it.id] = it; });
            renderProducts(list || []);
        }).catch(function (e) { toast(e.message, true); });
    }

    function renderProducts(list) {
        var box = $('products');
        box.textContent = '';
        $('empty').classList.toggle('hidden', list.length > 0);
        list.forEach(function (it) {
            var row = el('div', 'item');
            var main = el('div', 'item-main');
            main.appendChild(el('div', 'item-name', it.name));
            if (it.promo) main.appendChild(el('div', 'item-promo', '🏷 ' + it.promo));
            var meta = el('div', 'item-meta', it.pNo + ' · ' + t('available') + ': ');
            var avail = el('span', it.available > 0 ? '' : 'low', qty(it.available));
            meta.appendChild(avail);
            main.appendChild(meta);

            var line = cartLine(it.id);
            var unit = line ? unitById(it, line.productUnitId) : defaultUnit(it);
            var price = el('div', 'item-price', unit.price === null || unit.price === undefined ? t('noPrice') : money(unit.price) + ' ' + t('sum') + ' / ' + unit.name);
            main.appendChild(price);
            if ((it.units || []).length > 1) {
                var sel = el('select', 'unit');
                it.units.forEach(function (u) {
                    var o = el('option', null, u.name + (u.factor !== 1 ? ' (×' + qty(u.factor) + ')' : ''));
                    o.value = u.productUnitId || '';
                    if ((u.productUnitId || null) === (unit.productUnitId || null)) o.selected = true;
                    sel.appendChild(o);
                });
                sel.addEventListener('change', function () {
                    var id = sel.value ? parseInt(sel.value, 10) : null;
                    var l = cartLine(it.id);
                    if (l) { l.productUnitId = id; saveCart(); updateMainButton(); }
                    else row.dataset.unit = sel.value;
                    var u = unitById(it, id);
                    price.textContent = u.price === null || u.price === undefined ? t('noPrice') : money(u.price) + ' ' + t('sum') + ' / ' + u.name;
                });
                main.appendChild(sel);
            }
            row.appendChild(main);
            row.appendChild(stepper(it, row));
            box.appendChild(row);
        });
    }

    function stepper(it, row) {
        var wrap = el('div', 'stepper');
        var line = cartLine(it.id);
        if (!line) {
            var add = el('button', 'add', t('add'));
            add.type = 'button';
            add.addEventListener('click', function () {
                var unitId = row && row.dataset.unit ? parseInt(row.dataset.unit, 10) : defaultUnit(it).productUnitId;
                state.cart.push({ productId: it.id, productUnitId: unitId || null, qty: 1 });
                changed(it, row, wrap);
            });
            wrap.appendChild(add);
            return wrap;
        }
        var minus = el('button', null, '−'); minus.type = 'button';
        var input = el('input'); input.type = 'number'; input.inputMode = 'decimal'; input.min = '0'; input.step = 'any'; input.value = qty(line.qty);
        var plus = el('button', null, '+'); plus.type = 'button';
        minus.addEventListener('click', function () { setQty(it, line.qty - 1, row, wrap); });
        plus.addEventListener('click', function () { setQty(it, line.qty + 1, row, wrap); });
        input.addEventListener('change', function () { setQty(it, parseFloat(String(input.value).replace(',', '.')) || 0, row, wrap); });
        wrap.appendChild(minus); wrap.appendChild(input); wrap.appendChild(plus);
        return wrap;
    }

    function setQty(it, value, row, wrap) {
        var line = cartLine(it.id);
        if (!line) return;
        if (value <= 0) state.cart.splice(state.cart.indexOf(line), 1);
        else line.qty = Math.round(value * 1000) / 1000;
        changed(it, row, wrap);
    }

    function changed(it, row, wrap) {
        saveCart();
        if (wrap && wrap.parentNode) wrap.parentNode.replaceChild(stepper(it, row), wrap);
        updateMainButton();
        if (!$('viewCart').classList.contains('hidden')) renderCart();
        if (tg && tg.HapticFeedback) tg.HapticFeedback.selectionChanged();
    }

    function loadCategories() {
        return request('GET', '/categories').then(function (list) {
            var box = $('cats');
            box.textContent = '';
            var all = el('button', 'chip active', t('all')); all.type = 'button';
            all.addEventListener('click', function () { pickCategory(null, all); });
            box.appendChild(all);
            (list || []).forEach(function (c) {
                var b = el('button', 'chip', c.name); b.type = 'button';
                b.addEventListener('click', function () { pickCategory(c.id, b); });
                box.appendChild(b);
            });
        }).catch(function () { /* kategoriyasiz ham ishlaydi */ });
    }
    function pickCategory(id, btn) {
        state.category = id;
        Array.prototype.forEach.call(document.querySelectorAll('.chip'), function (c) { c.classList.remove('active'); });
        btn.classList.add('active');
        loadCatalog();
    }

    // ---------- mijoz ----------

    function loadCustomer() {
        if (!customerId) return Promise.resolve();
        return request('GET', '/customer/' + customerId).then(function (c) {
            if (!c) return;
            $('custName').textContent = c.name;
            var d = $('custDebt');
            d.textContent = t('debt') + ': ' + money(c.debt) + ' ' + t('sum') + (c.overdue > 0 ? ' (' + t('overdue') + ': ' + money(c.overdue) + ')' : '');
            d.classList.toggle('bad', c.overdue > 0);
            var rec = c.recommended || [];
            var box = $('recommended');
            box.textContent = '';
            if (rec.length) {
                box.appendChild(el('b', null, '💡 ' + t('recommended')));
                rec.slice(0, 6).forEach(function (r) {
                    var b = el('button', null, (r.overdue ? '⏰ ' : '') + (r.product || ('#' + r.productId)) + ' — ~' + qty(r.avgQtyBase));
                    b.type = 'button';
                    b.addEventListener('click', function () {
                        var code = (r.product || '').split(' — ')[0];
                        $('search').value = code; state.query = code; loadCatalog();
                    });
                    box.appendChild(b);
                });
                box.classList.remove('hidden');
            }
        }).catch(function (e) { toast(e.message, true); });
    }

    // ---------- savat ----------

    function lineTotal(line) {
        var it = state.items[line.productId];
        if (!it) return 0;
        var u = unitById(it, line.productUnitId);
        return (u.price || 0) * line.qty;
    }
    function cartTotal() { return state.cart.reduce(function (s, l) { return s + lineTotal(l); }, 0); }

    function renderCart() {
        var box = $('cartLines');
        box.textContent = '';
        $('cartEmpty').classList.toggle('hidden', state.cart.length > 0);
        $('cartForm').classList.toggle('hidden', state.cart.length === 0);
        state.cart.slice().forEach(function (line) {
            var it = state.items[line.productId];
            var row = el('div', 'item');
            var main = el('div', 'item-main');
            main.appendChild(el('div', 'item-name', it ? it.name : ('#' + line.productId)));
            if (it) {
                var u = unitById(it, line.productUnitId);
                main.appendChild(el('div', 'item-meta', qty(line.qty) + ' ' + u.name + ' × ' + money(u.price) + ' = ' + money(lineTotal(line)) + ' ' + t('sum')));
            }
            var rm = el('button', 'remove', t('remove')); rm.type = 'button';
            rm.addEventListener('click', function () {
                state.cart.splice(state.cart.indexOf(line), 1);
                saveCart(); renderCart(); updateMainButton(); loadCatalog();
            });
            main.appendChild(rm);
            row.appendChild(main);
            if (it) row.appendChild(stepper(it, null));
            box.appendChild(row);
        });
        $('cartTotal').textContent = money(cartTotal()) + ' ' + t('sum');
        var hasPromo = state.cart.some(function (l) { var it = state.items[l.productId]; return it && it.promo; });
        $('promoNote').textContent = t('promoNote');
        $('promoNote').classList.toggle('hidden', !hasPromo);
    }

    function show(view) {
        $('viewCatalog').classList.toggle('hidden', view !== 'catalog');
        $('viewCart').classList.toggle('hidden', view !== 'cart');
        $('tabCatalog').classList.toggle('active', view === 'catalog');
        $('tabCart').classList.toggle('active', view === 'cart');
        if (view === 'cart') renderCart();
        if (tg && tg.BackButton) { if (view === 'cart') tg.BackButton.show(); else tg.BackButton.hide(); }
    }

    function updateMainButton() {
        var n = state.cart.length;
        $('tabCart').textContent = t('cart') + (n ? ' (' + n + ')' : '');
        if (!tg || !tg.MainButton) return;
        if (n === 0) { tg.MainButton.hide(); return; }
        var onCart = !$('viewCart').classList.contains('hidden');
        tg.MainButton.setText(onCart ? t('submit') + ' · ' + money(cartTotal()) : t('cart') + ' (' + n + ') · ' + money(cartTotal()));
        tg.MainButton.show();
    }

    // ---------- shtrix-kod skaneri ----------

    // Skaner ochiq turadi: har yangi kod savatga +1 (shtrix-kod birligi bilan). Kod kadrda turgan paytda (oxirgi ko'rinishdan
    // 1,5 soniya o'tmagan) qayta sanalmaydi — xuddi shu tovarni yana qo'shish uchun kamerani boshqa tomonga burib qaytarish kerak.
    var lastScan = { code: null, at: 0 };
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
    function onScanned(code) {
        var now = Date.now();
        if (!code) return;
        var stillInView = code === lastScan.code && now - lastScan.at < 1500;
        lastScan = { code: code, at: now };
        if (stillInView) return;
        if (tg && tg.HapticFeedback) tg.HapticFeedback.impactOccurred('light');
        request('GET', '/catalog?customerId=' + customerId + '&q=' + encodeURIComponent(code)).then(function (list) {
            list = list || [];
            if (list.length !== 1) {
                $('scanStatus').textContent = t('scanNotFound').replace('{0}', code);
                if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred('error');
                return;
            }
            var it = list[0];
            state.items[it.id] = it;
            var unit = defaultUnit(it);   // shtrix-kod bo'yicha qidiruvda — o'sha kodning birligi
            var line = cartLine(it.id);
            if (line && (line.productUnitId || null) === (unit.productUnitId || null)) line.qty = Math.round((line.qty + 1) * 1000) / 1000;
            else if (line) { line.productUnitId = unit.productUnitId || null; line.qty = 1; }
            else state.cart.push({ productId: it.id, productUnitId: unit.productUnitId || null, qty: 1 });
            saveCart();
            updateMainButton();
            var l = cartLine(it.id);
            $('scanStatus').textContent = t('scanAdded').replace('{0}', it.name + ' — ' + qty(l.qty) + ' ' + unit.name);
            $('search').value = code; state.query = code;
            renderProducts(list);
        }).catch(function (e) { $('scanStatus').textContent = e.message; });
    }

    // ---------- imzo ----------

    function setupSignature() {
        var c = $('sign');
        var ctx = c.getContext('2d');
        ctx.lineWidth = 3; ctx.lineCap = 'round'; ctx.strokeStyle = '#111';
        var drawing = false;
        function pos(e) {
            var r = c.getBoundingClientRect();
            return { x: (e.clientX - r.left) * c.width / r.width, y: (e.clientY - r.top) * c.height / r.height };
        }
        c.addEventListener('pointerdown', function (e) { drawing = true; c.setPointerCapture(e.pointerId); var p = pos(e); ctx.beginPath(); ctx.moveTo(p.x, p.y); });
        c.addEventListener('pointermove', function (e) { if (!drawing) return; var p = pos(e); ctx.lineTo(p.x, p.y); ctx.stroke(); state.signed = true; });
        c.addEventListener('pointerup', function () { drawing = false; });
        c.addEventListener('pointercancel', function () { drawing = false; });
        $('signClear').addEventListener('click', function () { ctx.clearRect(0, 0, c.width, c.height); state.signed = false; });
    }

    // ---------- yuborish ----------

    var requestId = null;   // bitta savat — bitta ClientRequestId (takroriy bosish xavfsiz)
    function submit() {
        if (state.sending || state.cart.length === 0) return;
        state.sending = true;
        requestId = requestId || uuid();
        if (tg && tg.MainButton) tg.MainButton.showProgress(false);
        request('POST', '/order', {
            customerId: customerId, visitId: visitId, deliveryDate: $('deliveryDate').value || null, comment: $('comment').value || null,
            clientRequestId: requestId, signature: state.signed ? $('sign').toDataURL('image/png') : null,
            lines: state.cart.map(function (l) { return { productId: l.productId, productUnitId: l.productUnitId, qty: l.qty }; })
        }).then(function (r) {
            state.cart = []; saveCart(); requestId = null;
            $('comment').value = ''; $('deliveryDate').value = ''; $('signClear').click();
            show('catalog'); loadCatalog();
            if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred('success');
            toast(t('sent').replace('{0}', r.docNo));
            setTimeout(function () { if (tg) tg.close(); }, 900);
        }).catch(function (e) {
            if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred('error');
            toast(e.message, true);
        }).then(function () {
            state.sending = false;
            if (tg && tg.MainButton) tg.MainButton.hideProgress();
            updateMainButton();
        });
    }

    // ---------- ishga tushirish ----------

    function applyTexts() {
        document.documentElement.lang = lang;
        $('tabCatalog').textContent = t('catalog');
        $('search').placeholder = t('search');
        $('empty').textContent = t('empty');
        $('cartEmpty').textContent = t('cartEmpty');
        $('lblDate').textContent = t('date');
        $('lblComment').textContent = t('comment');
        $('lblSign').textContent = t('sign');
        $('signClear').textContent = t('clear');
        $('lblTotal').textContent = t('total');
        $('scanClose').textContent = t('scanDone');
        updateMainButton();
    }

    function init() {
        if (!tg || !tg.initData) {
            $('custName').textContent = t('notTelegram');
            document.querySelector('.tabs').classList.add('hidden');
            document.querySelector('main').classList.add('hidden');
            return;
        }
        tg.ready();
        tg.expand();
        lang = (tg.initDataUnsafe && tg.initDataUnsafe.user && /^ru/.test(tg.initDataUnsafe.user.language_code || '')) ? 'ru' : 'uz';
        loadCart();
        applyTexts();
        setupSignature();

        Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (b) {
            b.addEventListener('click', function () { show(b.dataset.view); updateMainButton(); });
        });
        $('search').addEventListener('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () { state.query = $('search').value.trim(); loadCatalog(); }, 350);
        });
        tg.MainButton.onClick(function () {
            if ($('viewCart').classList.contains('hidden')) { show('cart'); updateMainButton(); }
            else submit();
        });
        tg.BackButton.onClick(function () { show('catalog'); updateMainButton(); });
        $('scanBtn').addEventListener('click', openScanner);
        $('scanClose').addEventListener('click', closeScanner);
        document.addEventListener('visibilitychange', function () { if (document.hidden) closeScanner(); });

        request('GET', '/me').then(function (me) {
            if (me && (me.language === 'ru' || me.language === 'uz')) { lang = me.language; applyTexts(); }
        }).catch(function (e) { toast(e.message, true); }).then(function () {
            loadCustomer();
            loadCategories();
            loadCatalog().then(function () {
                // savatdagi, lekin joriy ro'yxatda yo'q tovarlar ma'lumoti ham kerak (jami hisobi uchun)
                if (state.cart.some(function (l) { return !state.items[l.productId]; })) {
                    request('GET', '/catalog?customerId=' + customerId).then(function (list) {
                        (list || []).forEach(function (it) { state.items[it.id] = it; });
                        updateMainButton();
                    }).catch(function () { });
                }
                updateMainButton();
            });
        });
    }

    init();
})();
