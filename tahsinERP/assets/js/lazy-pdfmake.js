/*
 * pdfmake (~1.9 MB: pdfmake.min.js + vfs_fonts.js) har sahifada yuklanmasligi uchun "dangasa" o'rinbosar.
 * DataTables Buttons PDF tugmasi window.pdfMake mavjudligini tekshiradi va bosilganda
 * pdfMake.createPdf(doc).download(...) chaqiradi. Bu stub chaqiruvni eslab qoladi, haqiqiy
 * kutubxonani faqat birinchi PDF bosilganda yuklaydi va so'ng o'sha metodni bajaradi.
 * Kutubxona yuklangach window.pdfMake haqiqiy obyekt bilan almashadi.
 */
(function () {
    'use strict';
    if (window.pdfMake) return;

    var BASE = 'https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.1.53/';
    var loading = null;

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = src;
            s.onload = resolve;
            s.onerror = function () { reject(new Error('Yuklanmadi: ' + src)); };
            document.head.appendChild(s);
        });
    }

    function loadPdfMake() {
        if (!loading) {
            loading = loadScript(BASE + 'pdfmake.min.js')
                .then(function () { return loadScript(BASE + 'vfs_fonts.js'); })
                .then(function () { return window.pdfMake; })
                .catch(function (err) {
                    loading = null; // keyingi bosishda qayta urinish
                    throw err;
                });
        }
        return loading;
    }

    var METHODS = ['download', 'open', 'print', 'getBlob', 'getBase64', 'getDataUrl', 'getBuffer'];

    window.pdfMake = {
        __lazy: true,
        createPdf: function (docDefinition) {
            var proxy = {};
            METHODS.forEach(function (name) {
                proxy[name] = function () {
                    var args = arguments;
                    loadPdfMake().then(function (pdfMake) {
                        var doc = pdfMake.createPdf(docDefinition);
                        doc[name].apply(doc, args);
                    }).catch(function () {
                        alert("PDF kutubxonasini yuklab bo'lmadi. Internet aloqasini tekshirib, qayta urinib ko'ring.");
                    });
                };
            });
            return proxy;
        }
    };
})();
