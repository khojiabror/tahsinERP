/*
 * CSRF token'ni avtomatik yuborish.
 *  - O'z saytiga ketadigan barcha POST/PUT/DELETE XMLHttpRequest'larga (jQuery $.ajax/$.post
 *    ham shu orqali) "RequestVerificationToken" header qo'shadi. XHR darajasida ishlagani uchun
 *    sahifadagi jQuery nusxalari soni va yuklanish tartibiga bog'liq emas.
 *  - O'z saytiga POST qilinadigan formalarda __RequestVerificationToken bo'lmasa, qo'shib qo'yadi.
 * Token sahifadagi birinchi input[name="__RequestVerificationToken"] dan olinadi
 * (layout'da @Html.AntiForgeryToken() bilan chiqariladi).
 */
(function () {
    'use strict';

    var FIELD = '__RequestVerificationToken';
    var HEADER = 'RequestVerificationToken';

    function getToken() {
        var el = document.querySelector('input[name="' + FIELD + '"]');
        return el ? el.value : null;
    }

    function isSameOrigin(url) {
        try {
            return new URL(url || window.location.href, window.location.href).origin === window.location.origin;
        } catch (e) {
            return false;
        }
    }

    function ensureFormToken(form) {
        if (!form || (form.getAttribute('method') || 'get').toLowerCase() !== 'post') return;
        if (!isSameOrigin(form.getAttribute('action'))) return;
        if (form.querySelector('input[name="' + FIELD + '"]')) return;
        var token = getToken();
        if (!token) return;
        var input = document.createElement('input');
        input.type = 'hidden';
        input.name = FIELD;
        input.value = token;
        form.appendChild(input);
    }

    var xhrProto = XMLHttpRequest.prototype;
    var nativeOpen = xhrProto.open;
    var nativeSend = xhrProto.send;
    var nativeSetHeader = xhrProto.setRequestHeader;

    xhrProto.open = function (method, url) {
        this._csrfMethod = String(method || 'GET').toUpperCase();
        this._csrfUrl = url;
        this._csrfHasHeader = false;
        return nativeOpen.apply(this, arguments);
    };

    xhrProto.setRequestHeader = function (name) {
        // Sahifa kodi header'ni o'zi qo'ygan bo'lsa, ikkinchi marta qo'shmaymiz ("a, a" bo'lib qoladi)
        if (String(name).toLowerCase() === HEADER.toLowerCase()) this._csrfHasHeader = true;
        return nativeSetHeader.apply(this, arguments);
    };

    xhrProto.send = function () {
        if (!this._csrfHasHeader && this._csrfMethod && this._csrfMethod !== 'GET' &&
            this._csrfMethod !== 'HEAD' && isSameOrigin(this._csrfUrl)) {
            var token = getToken();
            if (token) nativeSetHeader.call(this, HEADER, token);
        }
        return nativeSend.apply(this, arguments);
    };

    // Foydalanuvchi bosgan submit
    document.addEventListener('submit', function (e) {
        if (e.target && e.target.tagName === 'FORM') ensureFormToken(e.target);
    }, true);

    // JS orqali form.submit() (submit hodisasini chiqarmaydi)
    var nativeSubmit = HTMLFormElement.prototype.submit;
    HTMLFormElement.prototype.submit = function () {
        ensureFormToken(this);
        return nativeSubmit.apply(this, arguments);
    };

    // O'chirish kabi amallar uchun GET havola o'rniga: <a href="#" data-post-url="/X/Delete/5" data-confirm="...">
    // Bosilganda POST forma (CSRF token bilan) yaratib yuboradi.
    document.addEventListener('click', function (e) {
        var link = e.target && e.target.closest ? e.target.closest('[data-post-url]') : null;
        if (!link) return;
        e.preventDefault();
        var msg = link.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) return;
        var form = document.createElement('form');
        form.method = 'post';
        form.action = link.getAttribute('data-post-url');
        form.style.display = 'none';
        document.body.appendChild(form);
        form.submit();
    }, false);

    window.tahsinCsrf = { token: getToken, header: HEADER, field: FIELD };
})();
