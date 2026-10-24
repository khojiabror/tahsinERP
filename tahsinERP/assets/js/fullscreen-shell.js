/*
 * To'liq ekran sahifa almashganda ham saqlanadi.
 * Brauzer Fullscreen API hujjat almashganda (har link/forma) to'liq ekrandan chiqaradi — buni chetlab
 * bo'lmaydi. Shuning uchun tugma bosilganda joriy sahifa ustiga butun ekranni egallaydigan iframe
 * ochiladi va to'liq ekranga TASHQI hujjat o'tadi; keyingi navigatsiya iframe ichida bo'ladi, tashqi
 * hujjat qolaveradi — to'liq ekran ham. Manzil satri va sarlavha iframe'dan sinxronlanadi.
 * app.js'dagi screenfull.toggle() ishlovchisi shu fayl bilan almashtiriladi.
 */
(function ($) {
    'use strict';

    var doc = document;
    var root = doc.documentElement;

    function isFullscreen(d) {
        return !!(d.fullscreenElement || d.webkitFullscreenElement);
    }

    function requestFs(el) {
        if (el.requestFullscreen) return el.requestFullscreen();
        if (el.webkitRequestFullscreen) return el.webkitRequestFullscreen();
    }

    function exitFs(d) {
        if (d.exitFullscreen) return d.exitFullscreen();
        if (d.webkitExitFullscreen) return d.webkitExitFullscreen();
    }

    function setIcon(d, on) {
        var icons = d.querySelectorAll('.nav-link-expand i');
        for (var i = 0; i < icons.length; i++) {
            icons[i].classList.toggle('bx-exit-fullscreen', on);
            icons[i].classList.toggle('bx-fullscreen', !on);
        }
        d.documentElement.classList.toggle('full-screen', on);
    }

    function fsSupported() {
        return !!(root.requestFullscreen || root.webkitRequestFullscreen);
    }

    // Ichki sahifa (iframe ichida) — boshqaruv tashqi hujjatda
    var parentShell = null;
    try {
        if (window.parent !== window && window.parent.__erpFsShell) parentShell = window.parent.__erpFsShell;
    } catch (e) { parentShell = null; }

    $(function () {
        var $btn = $('.nav-link-expand');
        $btn.off('click');
        $(doc).off('fullscreenchange webkitfullscreenchange');

        if (parentShell) {
            setIcon(doc, parentShell.isOn());
            $btn.on('click', function (e) {
                e.preventDefault();
                parentShell.toggle();   // foydalanuvchi faolligi ota hujjatga ham o'tadi
            });
            return;
        }

        if (!fsSupported()) { $btn.closest('li').hide(); return; }

        var frame = null;

        function openFrame() {
            frame = doc.createElement('iframe');
            frame.id = 'erp-fs-frame';
            frame.setAttribute('allow', 'fullscreen');
            frame.setAttribute('allowfullscreen', '');
            frame.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;border:0;margin:0;padding:0;' +
                'z-index:2147483647;background:var(--erp-bg, #f2f4f4);';
            frame.src = location.href;
            frame.addEventListener('load', function () {
                try {
                    var w = frame.contentWindow;
                    if (w.location.href && w.location.href !== 'about:blank') {
                        history.replaceState(null, '', w.location.href);
                    }
                    doc.title = w.document.title;
                    setIcon(w.document, isFullscreen(doc));
                } catch (err) { /* boshqa domen — sinxronlanmaydi */ }
            });
            doc.body.appendChild(frame);
            root.style.overflow = 'hidden';
        }

        var shell = {
            isOn: function () { return isFullscreen(doc); },
            toggle: function () {
                if (isFullscreen(doc)) { exitFs(doc); return; }
                var p = requestFs(root);   // so'rov bosish bilan sinxron bo'lishi shart
                if (!frame) openFrame();
                if (p && p.catch) p.catch(function () { /* brauzer rad etdi — sahifa iframe'da qoladi */ });
            }
        };
        window.__erpFsShell = shell;

        $btn.on('click', function (e) {
            e.preventDefault();
            shell.toggle();
        });

        // Chiqishda (Esc) iframe qoladi — sahifa holati yo'qolmaydi; keyingi kirish shu iframe'ni ishlatadi
        $(doc).on('fullscreenchange webkitfullscreenchange', function () {
            var on = isFullscreen(doc);
            setIcon(doc, on);
            if (frame) {
                try { setIcon(frame.contentWindow.document, on); } catch (err) { }
            }
        });
    });
})(jQuery);
