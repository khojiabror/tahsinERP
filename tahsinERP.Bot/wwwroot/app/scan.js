// Kamera orqali shtrix-kod (EAN/UPC/Code128...). Avval brauzerning BarcodeDetector'i (Android Chrome/WebView),
// bo'lmasa — vendor/zxing.min.js (iOS) faqat kerak bo'lganda yuklanadi. Kadr har 200 ms da tekshiriladi.
(function () {
    'use strict';

    var FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'itf'];
    var video, stream, timer, detector, zxing, canvas, busy = false, onCode = null;

    function loadZxing() {
        if (window.ZXing) return Promise.resolve(window.ZXing);
        return new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = 'vendor/zxing.min.js';
            s.onload = function () { window.ZXing ? resolve(window.ZXing) : reject(new Error('zxing')); };
            s.onerror = function () { reject(new Error('zxing')); };
            document.head.appendChild(s);
        });
    }

    function makeDecoder() {
        if ('BarcodeDetector' in window) {
            return window.BarcodeDetector.getSupportedFormats().then(function (supported) {
                var formats = FORMATS.filter(function (f) { return supported.indexOf(f) >= 0; });
                if (!formats.length) throw new Error('formats');
                detector = new window.BarcodeDetector({ formats: formats });
            }).catch(function () { detector = null; return useZxing(); });
        }
        return useZxing();
    }

    function useZxing() {
        detector = null;
        return loadZxing().then(function (Z) {
            var hints = new Map();
            hints.set(Z.DecodeHintType.POSSIBLE_FORMATS, [Z.BarcodeFormat.EAN_13, Z.BarcodeFormat.EAN_8, Z.BarcodeFormat.UPC_A,
                Z.BarcodeFormat.UPC_E, Z.BarcodeFormat.CODE_128, Z.BarcodeFormat.CODE_39, Z.BarcodeFormat.ITF]);
            hints.set(Z.DecodeHintType.TRY_HARDER, true);
            var reader = new Z.MultiFormatReader();
            reader.setHints(hints);
            zxing = { Z: Z, reader: reader };
            canvas = document.createElement('canvas');
        });
    }

    function decodeFrame() {
        if (!video || video.readyState < 2) return Promise.resolve(null);
        if (detector) {
            return detector.detect(video).then(function (codes) { return codes && codes.length ? codes[0].rawValue : null; });
        }
        // ZXing: kadrning markaziy qismi (ramka) kulrang tasvirga
        var w = video.videoWidth, h = video.videoHeight;
        if (!w || !h) return Promise.resolve(null);
        var cw = Math.round(w * 0.9), ch = Math.round(h * 0.5);
        canvas.width = cw; canvas.height = ch;
        var ctx = canvas.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(video, (w - cw) / 2, (h - ch) / 2, cw, ch, 0, 0, cw, ch);
        try {
            var Z = zxing.Z;
            var bitmap = new Z.BinaryBitmap(new Z.HybridBinarizer(new Z.HTMLCanvasElementLuminanceSource(canvas)));
            return Promise.resolve(zxing.reader.decodeWithState(bitmap).getText());
        } catch (e) {
            return Promise.resolve(null);   // NotFoundException — kadrda kod yo'q
        }
    }

    function tick() {
        if (busy || !stream) return;
        busy = true;
        decodeFrame().then(function (code) {
            if (code && onCode) onCode(String(code).trim());
        }).catch(function () { }).then(function () { busy = false; });
    }

    /** Kamerani ochadi va kodlarni callback'ga beradi; xatoda Promise rad etiladi ("camera" / "unsupported"). */
    function start(videoEl, callback) {
        stop();
        video = videoEl;
        onCode = callback;
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return Promise.reject(new Error('unsupported'));
        return makeDecoder().then(function () {
            return navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 } }, audio: false });
        }, function () { throw new Error('unsupported'); }).then(function (s) {
            stream = s;
            video.srcObject = s;
            video.setAttribute('playsinline', '');
            video.muted = true;
            return video.play();
        }, function (e) {
            throw new Error(e && e.message === 'unsupported' ? 'unsupported' : 'camera');
        }).then(function () {
            timer = setInterval(tick, 200);
        });
    }

    function stop() {
        clearInterval(timer);
        timer = null;
        if (stream) stream.getTracks().forEach(function (t) { t.stop(); });
        stream = null;
        if (video) video.srcObject = null;
        onCode = null;
    }

    window.DstScanner = { start: start, stop: stop };
})();
