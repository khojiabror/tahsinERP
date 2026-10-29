// tahsinERP — Telegram do'kon (mijozning o'zi buyurtma beradi). Server: /app/api/{tenant}/shop/... (initData sarlavhada).
// Onlayn to'lov YO'Q: sahifa pul qabul qilmaydi — firma yozgan to'lov tartibi matni ko'rsatiladi, mijoz firmaga bevosita to'laydi.
(function () {
    'use strict';

    var tg = window.Telegram && window.Telegram.WebApp;
    var params = new URLSearchParams(location.search);
    var tenant = params.get('t') || '';
    var api = '/app/api/' + encodeURIComponent(tenant) + '/shop';

    var L = {
        uz: {
            catalog: 'Katalog', cart: 'Savat', orders: 'Buyurtmalarim', search: 'Tovar nomi yoki artikul', all: 'Hammasi', add: "Savatga",
            noPrice: "narx so'rang", out: 'Mavjud emas', low: 'Kam qoldi', inStock: 'Bor', empty: 'Hech narsa topilmadi',
            cartEmpty: "Savat bo'sh", ordersEmpty: "Hali buyurtma yo'q", delivery: 'Yetkazib berish', pickup: "O'zim olib ketaman",
            address: 'Manzil', comment: 'Izoh', total: 'Jami', remove: "O'chirish", submit: 'Buyurtma berish',
            promoNote: "Aksiya chegirmasi va sovg'alar buyurtma qabul qilinganda hisoblanadi.",
            pay: "To'lov", payDefault: "To'lov firmaning o'ziga — operator buyurtmani tasdiqlaganda tartibini aytadi. Ilovada to'lov qilinmaydi.",
            minOrder: "Eng kam buyurtma: {0} so'm", notApproved: "Ro'yxatdan o'tdingiz — birinchi buyurtmangizni operator tekshirib tasdiqlaydi.",
            sent: 'Buyurtma {0} yuborildi. Tasdiqlanganda botda xabar keladi.', error: 'Xatolik', sum: "so'm",
            notTelegram: "Bu sahifa Telegram ichida ochiladi.", addressNeeded: 'Yetkazish manzilini yozing.',
            st_New: 'Qabul qilindi', st_Confirmed: 'Tasdiqlandi', st_Picking: "Yig'ilmoqda", st_PartiallyShipped: "Qisman jo'natildi",
            st_Shipped: "Jo'natildi", st_Delivered: 'Yetkazildi', st_Closed: 'Yakunlandi', st_Cancelled: 'Bekor qilindi'
        },
        ru: {
            catalog: 'Каталог', cart: 'Корзина', orders: 'Мои заказы', search: 'Название или артикул', all: 'Все', add: 'В корзину',
            noPrice: 'цена по запросу', out: 'Нет в наличии', low: 'Мало', inStock: 'Есть', empty: 'Ничего не найдено',
            cartEmpty: 'Корзина пуста', ordersEmpty: 'Заказов пока нет', delivery: 'Доставка', pickup: 'Самовывоз',
            address: 'Адрес', comment: 'Комментарий', total: 'Итого', remove: 'Удалить', submit: 'Оформить заказ',
            promoNote: 'Скидки и подарки по акциям рассчитываются при приёме заказа.',
            pay: 'Оплата', payDefault: 'Оплата самой фирме — порядок сообщит оператор при подтверждении заказа. В приложении оплата не производится.',
            minOrder: 'Минимальный заказ: {0} сум', notApproved: 'Вы зарегистрированы — первый заказ оператор проверит и подтвердит.',
            sent: 'Заказ {0} отправлен. О подтверждении придёт сообщение в боте.', error: 'Ошибка', sum: 'сум',
            notTelegram: 'Эта страница открывается внутри Telegram.', addressNeeded: 'Укажите адрес доставки.',
            st_New: 'Принят', st_Confirmed: 'Подтверждён', st_Picking: 'Собирается', st_PartiallyShipped: 'Отгружен частично',
            st_Shipped: 'Отгружен', st_Delivered: 'Доставлен', st_Closed: 'Завершён', st_Cancelled: 'Отменён'
        }
    };
    var lang = 'uz';
    function t(k) { return lang === 'oz' ? window.uzCyrl(L.uz[k] || k) : (L[lang] && L[lang][k]) || L.uz[k] || k; }

    var $ = function (id) { return document.getElementById(id); };
    var state = { items: {}, cart: [], category: null, query: '', sending: false, info: null };
    var cartKey = 'shop-cart-' + tenant;

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
        toastTimer = setTimeout(function () { x.className = 'toast hidden'; }, 4000);
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

    // savat localStorage'da, 3 kun — keyin eskirgan narxli savat tashlanadi
    var cartTtlMs = 3 * 24 * 3600 * 1000;
    function saveCart() {
        try {
            if (state.cart.length === 0) localStorage.removeItem(cartKey);
            else localStorage.setItem(cartKey, JSON.stringify({ at: Date.now(), items: state.cart }));
        } catch (e) { /* saqlanmasa ham ishlaydi */ }
    }
    function loadCart() {
        try {
            var raw = JSON.parse(localStorage.getItem(cartKey) || 'null');
            state.cart = raw && raw.items && Date.now() - (raw.at || 0) < cartTtlMs ? raw.items : [];
        } catch (e) { state.cart = []; }
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
    function priceText(u) {
        return u.price === null || u.price === undefined ? t('noPrice') : money(u.price) + ' ' + t('sum') + ' / ' + u.name;
    }
    // narxsiz yoki qoldig'i yo'q tovar savatga qo'shilmaydi (ERP baribir qabul qilmaydi)
    function canBuy(it) { return it.stock !== 'out' && defaultUnit(it).price !== null && defaultUnit(it).price !== undefined; }

    var searchTimer;
    function loadCatalog() {
        var q = [];
        if (state.query) q.push('q=' + encodeURIComponent(state.query));
        if (state.category) q.push('categoryId=' + state.category);
        return request('GET', '/catalog' + (q.length ? '?' + q.join('&') : '')).then(function (list) {
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
            var meta = el('div', 'item-meta', it.pNo);
            if (it.stock) {
                meta.appendChild(document.createTextNode(' · '));
                meta.appendChild(el('span', it.stock === 'in' ? '' : 'low', t(it.stock === 'in' ? 'inStock' : it.stock)));
            }
            main.appendChild(meta);

            var line = cartLine(it.id);
            var unit = line ? unitById(it, line.productUnitId) : defaultUnit(it);
            var price = el('div', 'item-price', priceText(unit));
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
                    price.textContent = priceText(unitById(it, id));
                });
                main.appendChild(sel);
            }
            row.appendChild(main);
            if (canBuy(it) || cartLine(it.id)) row.appendChild(stepper(it, row));
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

    // ---------- savat ----------

    function lineTotal(line) {
        var it = state.items[line.productId];
        if (!it) return 0;
        return (unitById(it, line.productUnitId).price || 0) * line.qty;
    }
    function cartTotal() { return state.cart.reduce(function (s, l) { return s + lineTotal(l); }, 0); }
    function isPickup() { return document.querySelector('input[name=how]:checked').value === 'pickup'; }

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
        var min = state.info && state.info.minOrder;
        $('minNote').textContent = min > 0 ? t('minOrder').replace('{0}', money(min)) : '';
        $('minNote').classList.toggle('hidden', !(min > 0));
        var hasPromo = state.cart.some(function (l) { var it = state.items[l.productId]; return it && it.promo; });
        $('promoNote').textContent = t('promoNote');
        $('promoNote').classList.toggle('hidden', !hasPromo);
        $('addressWrap').classList.toggle('hidden', isPickup());
    }

    // ---------- buyurtmalarim ----------

    function loadOrders() {
        return request('GET', '/orders').then(function (list) {
            var box = $('orderList');
            box.textContent = '';
            list = list || [];
            $('ordersEmpty').classList.toggle('hidden', list.length > 0);
            list.forEach(function (o) {
                var row = el('div', 'item');
                var main = el('div', 'item-main');
                main.appendChild(el('div', 'item-name', o.docNo + ' · ' + o.date));
                main.appendChild(el('div', 'item-meta', t('st_' + o.status) + ' · ' + t(o.isPickup ? 'pickup' : 'delivery')));
                row.appendChild(main);
                row.appendChild(el('b', null, money(o.total)));
                box.appendChild(row);
            });
        }).catch(function (e) { toast(e.message, true); });
    }

    // ---------- ko'rinishlar ----------

    function show(view) {
        ['catalog', 'cart', 'orders'].forEach(function (v) {
            $('view' + v.charAt(0).toUpperCase() + v.slice(1)).classList.toggle('hidden', view !== v);
            $('tab' + v.charAt(0).toUpperCase() + v.slice(1)).classList.toggle('active', view === v);
        });
        if (view === 'cart') renderCart();
        if (view === 'orders') loadOrders();
        if (tg && tg.BackButton) { if (view !== 'catalog') tg.BackButton.show(); else tg.BackButton.hide(); }
        updateMainButton();
    }

    function updateMainButton() {
        var n = state.cart.length;
        $('tabCart').textContent = t('cart') + (n ? ' (' + n + ')' : '');
        if (!tg || !tg.MainButton) return;
        var onOrders = !$('viewOrders').classList.contains('hidden');
        if (n === 0 || onOrders) { tg.MainButton.hide(); return; }
        var onCart = !$('viewCart').classList.contains('hidden');
        tg.MainButton.setText(onCart ? t('submit') + ' · ' + money(cartTotal()) : t('cart') + ' (' + n + ') · ' + money(cartTotal()));
        tg.MainButton.show();
    }

    // ---------- yuborish ----------

    var requestId = null;   // bitta savat — bitta ClientRequestId (takroriy bosish xavfsiz)
    function submit() {
        if (state.sending || state.cart.length === 0) return;
        var address = $('address').value.trim();
        if (!isPickup() && !address) { toast(t('addressNeeded'), true); $('address').focus(); return; }
        state.sending = true;
        requestId = requestId || uuid();
        if (tg && tg.MainButton) tg.MainButton.showProgress(false);
        request('POST', '/order', {
            isPickup: isPickup(), address: isPickup() ? null : address, comment: $('comment').value || null, clientRequestId: requestId,
            lines: state.cart.map(function (l) { return { productId: l.productId, productUnitId: l.productUnitId, qty: l.qty }; })
        }).then(function (r) {
            state.cart = []; saveCart(); requestId = null;
            $('comment').value = '';
            if (tg && tg.HapticFeedback) tg.HapticFeedback.notificationOccurred('success');
            toast(t('sent').replace('{0}', r.docNo));
            show('orders');
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
        document.documentElement.lang = lang === 'oz' ? 'uz-Cyrl' : lang;
        $('tabCatalog').textContent = t('catalog');
        $('tabOrders').textContent = t('orders');
        $('search').placeholder = t('search');
        $('empty').textContent = t('empty');
        $('cartEmpty').textContent = t('cartEmpty');
        $('ordersEmpty').textContent = t('ordersEmpty');
        $('lblDelivery').textContent = t('delivery');
        $('lblPickup').textContent = t('pickup');
        $('lblAddress').textContent = t('address');
        $('lblComment').textContent = t('comment');
        $('lblTotal').textContent = t('total');
        $('lblPay').textContent = t('pay');
        $('payNote').textContent = (state.info && state.info.paymentNote) || t('payDefault');
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

        Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (b) {
            b.addEventListener('click', function () { show(b.dataset.view); });
        });
        Array.prototype.forEach.call(document.querySelectorAll('input[name=how]'), function (r) {
            r.addEventListener('change', function () { $('addressWrap').classList.toggle('hidden', isPickup()); });
        });
        $('search').addEventListener('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () { state.query = $('search').value.trim(); loadCatalog(); }, 350);
        });
        tg.MainButton.onClick(function () {
            if ($('viewCart').classList.contains('hidden')) show('cart');
            else submit();
        });
        tg.BackButton.onClick(function () { show('catalog'); });

        request('GET', '/info').then(function (r) {
            if (r && (r.language === 'ru' || r.language === 'uz' || r.language === 'oz')) lang = r.language;
            state.info = r && r.info;
            if (state.info) {
                $('custName').textContent = state.info.name || '';
                $('custNote').textContent = state.info.approved ? '' : t('notApproved');
            }
            applyTexts();
            loadCategories();
            loadCatalog().then(function () {
                // savatdagi, lekin joriy ro'yxatda yo'q tovarlar (jami hisobi uchun) — umumiy ro'yxatdan
                if (state.cart.some(function (l) { return !state.items[l.productId]; })) {
                    request('GET', '/catalog').then(function (list) {
                        (list || []).forEach(function (it) { state.items[it.id] = it; });
                        updateMainButton();
                    }).catch(function () { });
                }
                updateMainButton();
            });
        }).catch(function (e) {
            $('custName').textContent = e.message;
            document.querySelector('main').classList.add('hidden');
        });
    }

    init();
})();
