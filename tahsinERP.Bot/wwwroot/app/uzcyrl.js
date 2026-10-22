// O'zbek lotin -> kirill (Mini App "oz" tili uchun). tahsinERP.Localization/UzTransliterator.cs ning JS nusxasi —
// qoidalar bir xil bo'lsin: {0} o'rinbosarlari, lotin qisqartmalar (ERP, PDF), brendlar o'zgarmaydi.
(function (window) {
    'use strict';

    var KEEP = ['tahsinerp', 'telegram', 'excel', 'word', 'google', 'yandex', 'click', 'payme', 'uzum', 'whatsapp', 'email', 'mini', 'app', 'ok'];
    var UZ_ACRONYMS = ['QQS', 'STIR', 'JSHSHIR', 'MCHJ', 'AJ', 'XK', 'YATT', 'XT', 'DSQ', 'SHJ', 'TTN', 'HR', 'AKT', "YO'Q", 'HA', 'VA'];
    var MAP = { a: 'а', b: 'б', c: 'с', d: 'д', f: 'ф', g: 'г', h: 'ҳ', i: 'и', j: 'ж', k: 'к', l: 'л', m: 'м', n: 'н', o: 'о', p: 'п',
        q: 'қ', r: 'р', s: 'с', t: 'т', u: 'у', v: 'в', w: 'в', x: 'х', y: 'й', z: 'з' };
    var APOS = "'ʻʼ‘’`";
    // UzTransliterator.WordOverrides bilan bir xil
    var OVERRIDES = { dashboard: 'дашборд', dashboardi: 'дашборди', dashboardga: 'дашбордга', kanban: 'канбан', traction: 'трекшн',
        coverage: 'ковераж', engineering: 'инжиниринг', provisioning: 'провижининг', online: 'онлайн', offline: 'офлайн' };

    function isLatin(c) { return /[A-Za-z]/.test(c || ''); }
    function isApos(c) { return !!c && APOS.indexOf(c) >= 0; }
    function isUpper(c) { return c !== c.toLowerCase(); }
    function isVowel(c) { return 'aeiou'.indexOf(c) >= 0; }

    function keep(w) {
        if (KEEP.indexOf(w.toLowerCase()) >= 0) return true;
        var up = 0, low = 0, i;
        for (i = 0; i < w.length; i++) { if (/[A-Z]/.test(w[i])) up++; else if (/[a-z]/.test(w[i])) low++; }
        var norm = w.replace(/[ʻ‘’`]/g, "'").toUpperCase();
        if (up >= 2 && low === 0) return w.length <= 5 && UZ_ACRONYMS.indexOf(norm) < 0;
        if (up >= 2 && low > 0 && !isUpper(w[0])) return true;
        if (up >= 2 && low > 0 && UZ_ACRONYMS.indexOf(norm) < 0) {
            for (i = 1; i < w.length; i++) if (/[A-Z]/.test(w[i]) && /[a-z]/.test(w[i - 1])) return true;
        }
        return false;
    }

    function tsIsTse(w, i) {
        if (i === 0) return true;
        var k = i + 2;
        if (k >= w.length) return false;
        var a = w[k].toLowerCase();
        if (a === 'e') return true;
        return a === 'i' && k + 1 < w.length && 'yoaepkf'.indexOf(w[k + 1].toLowerCase()) >= 0;
    }

    function word(w) {
        var fx = OVERRIDES[w.toLowerCase()];
        if (fx) return w === w.toUpperCase() && w.length > 1 ? fx.toUpperCase() : isUpper(w[0]) ? fx[0].toUpperCase() + fx.slice(1) : fx;
        var out = '', n = w.length, allUpper = w.length > 1 && w === w.toUpperCase();
        for (var i = 0; i < n; i++) {
            var c = w[i], lc = c.toLowerCase(), up = isUpper(c), next = i + 1 < n ? w[i + 1].toLowerCase() : '';
            var cs = function (s) { return up ? (allUpper || s.length === 1 ? s.toUpperCase() : s[0].toUpperCase() + s.slice(1)) : s; };
            if ((lc === 'o' || lc === 'g') && isApos(w[i + 1])) { out += cs(lc === 'o' ? 'ў' : 'ғ'); i++; continue; }
            if (isApos(c)) { out += allUpper ? 'Ъ' : 'ъ'; continue; }
            if (lc === 's' && next === 'h') { out += cs('ш'); i++; continue; }
            if (lc === 'c' && next === 'h') { out += cs('ч'); i++; continue; }
            if (lc === 't' && next === 's' && tsIsTse(w, i)) { out += cs('ц'); i++; continue; }
            if (lc === 's' && i > 0 && w[i - 1].toLowerCase() === 'k' && next === 'i' && i + 2 < n && 'yo'.indexOf(w[i + 2].toLowerCase()) >= 0) { out += cs('ц'); continue; }
            if (lc === 'y' && 'ouae'.indexOf(next) >= 0 && next !== '' && !(next === 'o' && isApos(w[i + 2]))) {
                out += cs(next === 'o' ? 'ё' : next === 'u' ? 'ю' : next === 'a' ? 'я' : 'е'); i++; continue;
            }
            if (lc === 'e') { out += cs(i === 0 || isVowel((w[i - 1] || '').toLowerCase()) ? 'э' : 'е'); continue; }
            out += MAP[lc] ? cs(MAP[lc]) : c;
        }
        return out;
    }

    function withSuffix(w) {
        for (var k = 1; k < w.length - 1; k++) {
            if (isApos(w[k]) && keep(w.slice(0, k))) return w.slice(0, k) + w[k] + word(w.slice(k + 1));
        }
        return word(w);
    }

    window.uzCyrl = function (text) {
        if (!text) return text;
        var out = '', i = 0, s = String(text);
        while (i < s.length) {
            var c = s[i], end;
            if (c === '{' && (end = s.indexOf('}', i)) > i) { out += s.slice(i, end + 1); i = end + 1; continue; }
            if (c === '<' && /[A-Za-z\/!]/.test(s[i + 1] || '') && (end = s.indexOf('>', i)) > i) { out += s.slice(i, end + 1); i = end + 1; continue; }
            if (c === '&' && (end = s.indexOf(';', i)) > i && end - i <= 8) { out += s.slice(i, end + 1); i = end + 1; continue; }
            if (isLatin(c)) {
                var start = i;
                while (i < s.length && (isLatin(s[i]) || (isApos(s[i]) && i > start && isLatin(s[i + 1])))) i++;
                var w = s.slice(start, i), prev = s[start - 1] || '', after = s[i] || '';
                var technical = './@_#\\'.indexOf(prev) >= 0 && prev !== '' || '@_/\\'.indexOf(after) >= 0 && after !== '' || (after === '.' && isLatin(s[i + 1]));
                out += technical || keep(w) ? w : withSuffix(w);
                continue;
            }
            out += c;
            i++;
        }
        return out;
    };
})(window);
