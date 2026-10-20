// tahsinERP sayt-katalog: savat (brauzerda), tez qo'shish, birlik/miqdor, filtrlar (sahifani qayta yuklamasdan), menyu, slayder, ariza.
(function () {
  'use strict';

  var script = document.currentScript || document.querySelector('script[data-tenant]');
  var tenant = script.getAttribute('data-tenant');
  var lang = script.getAttribute('data-lang') || 'uz';
  var addedText = script.getAttribute('data-added') || 'OK';
  var KEY = 'cart:' + tenant;
  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

  // ---------- savat ----------
  function load() {
    try { var v = JSON.parse(localStorage.getItem(KEY) || '[]'); return Array.isArray(v) ? v : []; } catch (e) { return []; }
  }
  function save(items) {
    try { localStorage.setItem(KEY, JSON.stringify(items)); } catch (e) { /* xususiy rejim — savat sessiyada qoladi */ }
    renderCount(items);
  }
  function renderCount(items) {
    var n = (items || load()).length;
    $$('[data-cart-count]').forEach(function (el) { el.textContent = n; el.hidden = n === 0; });
  }
  function unitValue(v) { return v === '' || v === null || v === undefined || v === 'null' ? null : +v; }
  function add(id, unit, qty) {
    var items = load();
    var found = items.filter(function (x) { return x.id === id && x.unit === unit; })[0];
    if (found) found.qty = Math.round((found.qty + qty) * 1000) / 1000;
    else items.push({ id: id, unit: unit, qty: qty });
    save(items);
  }

  var toastTimer;
  function toast(html) {
    var t = $('#toast');
    if (!t) return;
    t.innerHTML = html;
    t.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.hidden = true; }, 3200);
  }
  function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }

  document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-add]');
    if (!btn) return;
    e.preventDefault();
    var host = btn.closest('[data-product]');
    if (!host) return;
    var data = JSON.parse(host.getAttribute('data-product'));
    var qty = 1;
    if (btn.hasAttribute('data-from-pdp')) {
      var input = $('[data-qty] input', host);
      qty = Math.max(0.001, parseFloat(String(input && input.value || '1').replace(',', '.')) || 1);
    }
    add(data.id, unitValue(btn.getAttribute('data-unit')), qty);
    btn.classList.add('done');
    setTimeout(function () { btn.classList.remove('done'); }, 900);
    toast(esc(addedText) + ' <a href="/' + lang + '/cart">→</a>');
  });

  // ---------- birlik tanlash (tovar sahifasi) ----------
  $$('[data-units]').forEach(function (group) {
    var host = group.closest('[data-product]');
    var data = JSON.parse(host.getAttribute('data-product'));
    group.addEventListener('click', function (e) {
      var b = e.target.closest('[data-unit-id]');
      if (!b) return;
      $$('[data-unit-id]', group).forEach(function (x) { x.classList.toggle('on', x === b); x.setAttribute('aria-checked', x === b ? 'true' : 'false'); });
      var unit = unitValue(b.getAttribute('data-unit-id'));
      var add = $('[data-add]', host);
      if (add) add.setAttribute('data-unit', unit === null ? '' : unit);
      var u = data.units.filter(function (x) { return x.id === unit; })[0];
      var priceEl = $('[data-unit-price]', host), nameEl = $('[data-unit-name]', host);
      if (u && priceEl && u.price != null) priceEl.textContent = money(u.price);
      if (u && nameEl) nameEl.textContent = '/ ' + u.name;
    });
  });
  function money(v) {
    var s = (Math.round(v * 100) / 100).toFixed(v % 1 === 0 ? 0 : 2).replace('.', ',').replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
    return s + ' ' + (lang === 'ru' ? 'сум' : "so'm");
  }

  // ---------- miqdor tugmalari ----------
  document.addEventListener('click', function (e) {
    var b = e.target.closest('[data-qty] [data-step]');
    if (!b) return;
    var input = $('input', b.closest('[data-qty]'));
    var v = parseFloat(String(input.value).replace(',', '.')) || 0;
    v = Math.max(b.getAttribute('data-step') === '-1' ? 0 : 1, Math.round((v + +b.getAttribute('data-step')) * 1000) / 1000);
    input.value = v;
    input.dispatchEvent(new Event('change', { bubbles: true }));
  });

  // ---------- header, mobil menyu ----------
  var nav = $('#nav');
  window.addEventListener('scroll', function () { if (nav) nav.classList.toggle('scrolled', window.scrollY > 4); }, { passive: true });
  var menu = $('#menu');
  document.addEventListener('click', function (e) {
    if (e.target.closest('[data-menu-open]')) { menu.hidden = false; document.body.style.overflow = 'hidden'; }
    if (e.target.closest('[data-menu-close]')) { menu.hidden = true; document.body.style.overflow = ''; }
    if (e.target.closest('[data-filters-open]')) { var f = $('#filters'); if (f) { f.classList.add('open'); document.body.style.overflow = 'hidden'; } }
    if (e.target.closest('[data-filters-close]')) { var g = $('#filters'); if (g) { g.classList.remove('open'); document.body.style.overflow = ''; } }
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && menu && !menu.hidden) { menu.hidden = true; document.body.style.overflow = ''; }
  });

  // ---------- katalog: filtr/saralash/sahifalash — faqat natijalar bloki almashadi ----------
  var results = $('[data-results]');
  function navigate(url, push) {
    var section = $('.results', results);
    if (section) section.classList.add('loading');
    return fetch(url, { headers: { 'X-Partial': '1' } })
      .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
      .then(function (html) {
        results.innerHTML = html;
        document.body.style.overflow = '';
        if (push) history.pushState({ partial: true }, '', url);
        var top = results.getBoundingClientRect().top + window.scrollY - 90;
        if (window.scrollY > top) window.scrollTo({ top: top });
      })
      .catch(function () { location.href = url; });
  }
  if (results) {
    results.addEventListener('click', function (e) {
      var a = e.target.closest('a[data-nav]');
      if (!a || e.ctrlKey || e.metaKey || e.shiftKey) return;
      e.preventDefault();
      navigate(a.href, true);
    });
    results.addEventListener('change', function (e) {
      if (e.target.matches('[data-sort]')) navigate(e.target.value, true);
    });
    window.addEventListener('popstate', function () { navigate(location.href, false); });
  }

  // ---------- slayder ----------
  var slides = $('[data-slides]'), dots = $$('[data-dots] button');
  if (slides && dots.length) {
    var idx = 0;
    function show(i) { idx = (i + dots.length) % dots.length; slides.scrollTo({ left: slides.clientWidth * idx, behavior: 'smooth' }); }
    function mark() { var i = Math.round(slides.scrollLeft / slides.clientWidth); dots.forEach(function (d, j) { d.classList.toggle('on', j === i); }); idx = i; }
    dots.forEach(function (d, i) { d.addEventListener('click', function () { show(i); }); });
    slides.addEventListener('scroll', function () { window.requestAnimationFrame(mark); }, { passive: true });
    mark();
    var timer = setInterval(function () { show(idx + 1); }, 6000);
    slides.addEventListener('pointerdown', function () { clearInterval(timer); });
  }

  // ---------- galereya (mobil): kichik rasmlar ----------
  var gallery = $('[data-gallery]'), thumbs = $$('[data-thumbs] .thumb');
  if (gallery && thumbs.length) {
    gallery.addEventListener('scroll', function () {
      var i = Math.round(gallery.scrollLeft / gallery.clientWidth);
      thumbs.forEach(function (t, j) { t.classList.toggle('on', j === i); });
    }, { passive: true });
    thumbs.forEach(function (t, i) {
      t.addEventListener('click', function (e) { e.preventDefault(); gallery.scrollTo({ left: gallery.clientWidth * i, behavior: 'smooth' }); });
    });
  }

  // ---------- savat sahifasi ----------
  var linesBox = $('[data-cart-lines]');
  function refreshLines() {
    var items = load();
    var empty = $('[data-cart-empty]');
    if (!items.length) { linesBox.innerHTML = ''; if (empty) empty.hidden = false; return; }
    if (empty) empty.hidden = true;
    var qs = items.map(function (x) { return 'i=' + encodeURIComponent(x.id + ':' + (x.unit === null ? '' : x.unit) + ':' + x.qty); }).join('&');
    fetch(linesBox.getAttribute('data-url') + '&' + qs)
      .then(function (r) { return r.text(); })
      .then(function (html) {
        linesBox.innerHTML = html;
        // nusxada yo'q tovarlar savatdan tushadi
        var present = $$('[data-line]', linesBox).map(function (el) { return el.getAttribute('data-line') + ':' + el.getAttribute('data-unit'); });
        var kept = items.filter(function (x) { return present.indexOf(x.id + ':' + (x.unit === null ? '' : x.unit)) >= 0; });
        if (kept.length !== items.length) save(kept);
        if (!kept.length && empty) empty.hidden = false;
      });
  }
  if (linesBox) {
    refreshLines();
    var debounce;
    linesBox.addEventListener('change', function (e) {
      if (!e.target.matches('[data-line-qty]')) return;
      var li = e.target.closest('[data-line]');
      var id = +li.getAttribute('data-line'), unit = unitValue(li.getAttribute('data-unit'));
      var qty = Math.round((parseFloat(String(e.target.value).replace(',', '.')) || 0) * 1000) / 1000;
      var items = load().filter(function (x) { return !(x.id === id && x.unit === unit && qty <= 0); });
      items.forEach(function (x) { if (x.id === id && x.unit === unit) x.qty = qty; });
      save(items);
      clearTimeout(debounce);
      debounce = setTimeout(refreshLines, 350);
    });
    linesBox.addEventListener('click', function (e) {
      if (!e.target.closest('[data-remove]')) return;
      var li = e.target.closest('[data-line]');
      var id = +li.getAttribute('data-line'), unit = unitValue(li.getAttribute('data-unit'));
      save(load().filter(function (x) { return !(x.id === id && x.unit === unit); }));
      refreshLines();
    });
  }

  // ---------- ariza formasi ----------
  var form = $('[data-lead-form]');
  if (form) {
    var ts = $('[data-ts]', form);
    if (!ts.value) ts.value = Date.now();
    var rid = $('[data-request-id]', form);
    if (!rid.value) {
      var stored = null;
      try { stored = sessionStorage.getItem('lead-rid:' + tenant); } catch (e) { }
      rid.value = stored || (window.crypto && crypto.randomUUID ? crypto.randomUUID() : String(Date.now()) + Math.random()).toString();
      try { sessionStorage.setItem('lead-rid:' + tenant, rid.value); } catch (e) { }
    }
    form.addEventListener('submit', function () {
      $('[data-items]', form).value = JSON.stringify(load());
      var btn = $('button[type=submit]', form);
      if (btn) setTimeout(function () { btn.disabled = true; }, 0);
    });
  }
  if ($('[data-sent]')) {
    save([]);
    try { sessionStorage.removeItem('lead-rid:' + tenant); } catch (e) { }
  }

  renderCount();
})();
