/*
 * Quill + mammoth asosidagi Word muharriri (STD Create/Edit; JobDesc ham ishlatishi mumkin).
 * Avval ~200 qatorli bir xil skript STD va JobDesc Create/Edit sahifalariga nusxalangan edi (dublikat #15/#20).
 *
 *   initDocxEditor({
 *       form: '#stdForm', hidden: '#documentContent', saveBtn: '#saveBtn',
 *       encode: 'base64',              // 'base64' (STD) yoki 'html' (JobDesc) — server kutgan format
 *       restorePosted: true,           // validatsiya xatosidan qaytganda hidden'dagi matnni tiklash
 *       initialHtml: null,             // tayyor HTML (masalan, JobDesc Edit)
 *       autoLoadUrl: null,             // ochilganda .docx yuklash (tiklangan matn bo'lmasa)
 *       onlyIfChanged: false,          // true — matn o'zgarmasa hidden bo'sh yuboriladi (yangi fayl versiyasi yo'q)
 *       notify: false,                 // yuklangach alert
 *       buttons: [{ selector: '#loadTemplateBtn', url: function () { return '/X/GetTemplate'; }, error: '...' }],
 *       fileButton: '#loadDocxBtn'
 *   });
 */
(function (window, document) {
    'use strict';

    // Joriy tildagi matn (window.erpT, _I18nScript); yuklanmagan bo'lsa — o'zbekcha
    function tr(key, fallback) { return window.erpT ? window.erpT(key) : fallback; }

    var STYLE_MAP = [
        "p[style-name='Heading 1'] => h1:fresh",
        "p[style-name='Heading 2'] => h2:fresh",
        "p[style-name='Heading 3'] => h3:fresh",
        "p[style-name='Title'] => h1.title:fresh",
        "r[style-name='Strong'] => strong",
        "p[style-name='Quote'] => blockquote:fresh",
        "p[style-name='Intense Quote'] => blockquote.intense:fresh",
        "r[style-name='Emphasis'] => em",
        "r[style-name='Code'] => code"
    ];

    function encodeBase64(text) { return window.btoa(unescape(encodeURIComponent(text))); }
    function decodeBase64(text) { return decodeURIComponent(escape(window.atob(text))); }

    function isEmpty(html) {
        var h = (html || '').trim();
        return h === '' || h === '<p><br></p>';
    }

    window.initDocxEditor = function (opts) {
        var $ = window.jQuery;
        var $hidden = $(opts.hidden || '#documentContent');
        var changed = false;

        var quill = new window.Quill(opts.editor || '#quill-editor', {
            modules: { toolbar: opts.toolbar || '#quill-toolbar' },
            theme: 'snow',
            placeholder: tr('Js_Doc_ContentPh', 'Hujjat kontentini kiriting...')
        });

        quill.on('text-change', function (delta, oldDelta, source) {
            if (source === 'user') changed = true;
        });

        // Quill 1.3: convert() HTML satrini oladi (DOM element berilsa bo'sh natija qaytaradi —
        // eski STD/JobDesc skriptlarida shu sababli Word/shablon muharrirga tushmasdi)
        function setHtml(html) {
            quill.setContents(quill.clipboard.convert(html || ''), 'silent');
        }

        function loadArrayBuffer(buffer, markChanged) {
            return window.mammoth.convertToHtml({
                arrayBuffer: buffer,
                styleMap: STYLE_MAP,
                includeDefaultStyleMap: true,
                convertImage: window.mammoth.images.imgElement(function (image) {
                    return image.read('base64').then(function (data) {
                        return { src: 'data:' + image.contentType + ';base64,' + data };
                    });
                })
            }).then(function (result) {
                setHtml(result.value);
                if (markChanged) changed = true;
                if (opts.notify) window.alert(tr('Js_Doc_Loaded', 'Hujjat muvaffaqiyatli yuklandi!'));
            });
        }

        function loadUrl(url, errorText, markChanged) {
            $.ajax({
                url: url,
                type: 'GET',
                xhrFields: { responseType: 'blob' },
                success: function (blob) {
                    var reader = new FileReader();
                    reader.onload = function (e) {
                        loadArrayBuffer(e.target.result, markChanged).catch(function () {
                            window.alert(errorText || tr('Js_Doc_FileLoadError', 'Fayl yuklashda xatolik yuz berdi'));
                        });
                    };
                    reader.readAsArrayBuffer(blob);
                },
                error: function () { window.alert(errorText || tr('Js_Doc_FileLoadError', 'Fayl yuklashda xatolik yuz berdi')); }
            });
        }

        (opts.buttons || []).forEach(function (b) {
            $(b.selector).on('click', function () {
                var url = typeof b.url === 'function' ? b.url() : b.url;
                if (!url) { window.alert(b.missing || tr('Js_Doc_FileNotFound', 'Fayl topilmadi')); return; }
                loadUrl(url, b.error, false);
            });
        });

        if (opts.fileButton) {
            $(opts.fileButton).on('click', function () {
                var input = document.createElement('input');
                input.type = 'file';
                input.accept = '.docx';
                input.onchange = function (e) {
                    var file = e.target.files[0];
                    if (!file) return;
                    var reader = new FileReader();
                    reader.onload = function (ev) {
                        loadArrayBuffer(ev.target.result, true).catch(function (err) {
                            window.alert(tr('Js_Doc_WordLoadError', 'Word fayl yuklashda xatolik yuz berdi') + (err && err.message ? ': ' + err.message : ''));
                        });
                    };
                    reader.readAsArrayBuffer(file);
                };
                input.click();
            });
        }

        // Boshlang'ich matn: validatsiya xatosidan qaytgan matn > tayyor HTML > avtomatik yuklash
        var restored = false;
        var posted = opts.restorePosted ? $hidden.val() : '';
        if (posted) {
            try {
                quill.clipboard.dangerouslyPasteHTML(opts.encode === 'base64' ? decodeBase64(posted) : posted, 'silent');
                changed = true;
                restored = true;
            } catch (e) { /* buzuq qiymat — e'tiborsiz */ }
        }
        if (!restored && opts.initialHtml) {
            quill.root.innerHTML = opts.initialHtml;
            restored = true;
        }
        if (!restored && opts.autoLoadUrl) {
            loadUrl(opts.autoLoadUrl, null, false);
        }

        $(opts.form).on('submit', function (e) {
            if (opts.onlyIfChanged && !changed) {
                // Faqat metadata saqlanadi, fayl versiyasi o'zgarmaydi
                $hidden.val('');
            } else {
                var html = quill.root.innerHTML;
                if (isEmpty(html)) {
                    e.preventDefault();
                    window.alert('Iltimos, hujjat kontentini kiriting');
                    return false;
                }
                $hidden.val(opts.encode === 'base64' ? encodeBase64(html) : html);
            }
            if (opts.saveBtn) {
                // Ikki marta yuborilmasin
                $(opts.saveBtn).prop('disabled', true).html('<i class="bx bx-loader-alt bx-spin"></i> Saqlanmoqda...');
            }
        });

        return { quill: quill, loadUrl: loadUrl, isChanged: function () { return changed; } };
    };
})(window, document);
