// tahsinerp.uz — sahifa animatsiyalari va demo formasi. Kutubxonasiz; CSP: script-src 'self'.
(function () {
    "use strict";
    document.documentElement.classList.remove("no-js");
    var $ = function (s, r) { return (r || document).querySelector(s); };
    var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var lang = document.documentElement.lang || "uz";
    // Minglik ajratgich: en — vergul, uz/ru — bo'shliq (nbsp); o'nlik: en — nuqta, uz/ru — vergul
    var group = function (n) { return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, lang === "en" ? "," : "\u00A0"); };

    // ── Header: fon scroll'da, mobil menyu ──────────────────────────────────
    var hdr = $("[data-header]");
    var onScroll = function () { hdr.classList.toggle("scrolled", window.scrollY > 24); };
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();
    $("[data-burger]").addEventListener("click", function () { hdr.classList.toggle("open"); });
    $$("[data-nav] a").forEach(function (a) { a.addEventListener("click", function () { hdr.classList.remove("open"); }); });

    // ── Ko'rinishga kirganda: reveal, sanoq, telefon ────────────────────────
    var countUp = function (el) {
        var raw = el.getAttribute("data-count");
        var m = /^(\d+)(.*)$/.exec(raw);
        if (!m || reduced) return;
        var target = parseInt(m[1], 10), rest = m[2], t0 = null, dur = 1400;
        var step = function (t) {
            if (!t0) t0 = t;
            var p = Math.min(1, (t - t0) / dur), e = 1 - Math.pow(1 - p, 3);
            el.textContent = Math.round(target * e) + rest;
            if (p < 1) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    };

    if ("IntersectionObserver" in window) {
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) {
                if (!en.isIntersecting) return;
                var el = en.target;
                // Bir qatordagi kartalar ketma-ket chiqadi
                var sib = el.parentElement ? $$(":scope > .reveal", el.parentElement) : [];
                var idx = Math.max(0, sib.indexOf(el));
                el.style.transitionDelay = Math.min(idx, 6) * 70 + "ms";
                el.classList.add("in");
                if (el.hasAttribute("data-count")) countUp(el);
                io.unobserve(el);
            });
        }, { threshold: 0.12, rootMargin: "0px 0px -40px 0px" });
        $$(".reveal, [data-count]").forEach(function (el) { io.observe(el); });
    } else {
        $$(".reveal").forEach(function (el) { el.classList.add("in"); });
    }

    // ── Hero: "jonli" dashboard ─────────────────────────────────────────────
    var fmt = function (v, dec) { return dec ? v.toFixed(dec).replace(".", lang === "en" ? "." : ",") : group(Math.round(v)); };
    $$("[data-live]").forEach(function (el) {
        var dec = parseInt(el.getAttribute("data-dec") || "0", 10);
        el.textContent = fmt(parseFloat(el.getAttribute("data-v")), dec);
    });
    var tickKpi = function () {
        $$("[data-live]").forEach(function (el) {
            var v = parseFloat(el.getAttribute("data-v"));
            var dec = parseInt(el.getAttribute("data-dec") || "0", 10);
            var kind = el.getAttribute("data-live");
            var nv = kind === "plan" ? Math.min(99.4, v + Math.random() * 0.4) : v + Math.random() * (kind === "sales" ? 9 : 3);
            el.setAttribute("data-v", String(nv));
            el.textContent = fmt(nv, dec);
            el.classList.add("flash");
            setTimeout(function () { el.classList.remove("flash"); }, 700);
        });
        $$(".bars i").forEach(function (b) { b.style.height = (40 + Math.random() * 55).toFixed(0) + "%"; });
    };

    var feed = $("[data-feed]");
    var src = $("[data-feed-src]");
    var events = src ? (src.content ? src.content.textContent : src.textContent).split("\n").filter(Boolean) : [];
    var ei = 3;
    var tickFeed = function () {
        if (!feed || events.length === 0) return;
        var li = document.createElement("li");
        li.textContent = events[ei++ % events.length];
        li.className = "enter";
        feed.insertBefore(li, feed.firstChild);
        while (feed.children.length > 3) feed.removeChild(feed.lastChild);
        setTimeout(function () { li.classList.remove("enter"); }, 1800);
    };
    if (!reduced) {
        setInterval(function () { if (!document.hidden) { tickFeed(); tickKpi(); } }, 3200);
    }

    // ── Ekotizim: buyurtma bosqichlari bo'ylab yuradi ───────────────────────
    var fsteps = $$("[data-flow] .fstep");
    if (fsteps.length && !reduced) {
        var fi = 0;
        setInterval(function () {
            fsteps.forEach(function (s, i) { s.classList.toggle("hot", i === fi); });
            fi = (fi + 1) % fsteps.length;
        }, 1000);
    }

    // ── Ekranlar: tablar avtomatik almashadi ────────────────────────────────
    var show = $("[data-show]");
    if (show) {
        var tabs = $$("[data-tab]", show), shots = $$("[data-shot]", show), cur = 0, timer = null, DUR = 6000;
        show.style.setProperty("--dur", DUR / 1000 + "s");
        var go = function (n) {
            cur = (n + tabs.length) % tabs.length;
            tabs.forEach(function (t, i) {
                var on = i === cur;
                t.classList.toggle("on", on);
                t.setAttribute("aria-selected", on ? "true" : "false");
                if (on) { var p = $(".prog", t); p.style.animation = "none"; void p.offsetWidth; p.style.animation = ""; }
            });
            shots.forEach(function (s, i) { s.classList.toggle("on", i === cur); });
            if (window.innerWidth <= 960 && tabs[cur].scrollIntoView) {
                var bar = tabs[cur].parentElement;
                bar.scrollTo({ left: tabs[cur].offsetLeft - 16, behavior: reduced ? "auto" : "smooth" });
            }
        };
        var start = function () { stop(); if (!reduced) timer = setInterval(function () { if (!show.classList.contains("paused")) go(cur + 1); }, DUR); };
        var stop = function () { if (timer) clearInterval(timer); timer = null; };
        tabs.forEach(function (t, i) { t.addEventListener("click", function () { go(i); start(); }); });
        show.addEventListener("mouseenter", function () { show.classList.add("paused"); });
        show.addEventListener("mouseleave", function () { show.classList.remove("paused"); });
        // Faqat ko'rinib turganda aylansin
        if ("IntersectionObserver" in window) {
            new IntersectionObserver(function (en) { en[0].isIntersecting ? start() : stop(); }, { threshold: 0.3 }).observe(show);
        } else start();
    }

    // ── Telegram telefoni: ko'rinishga kirganda suhbat o'ynaydi ─────────────
    var phone = $("[data-phone]");
    if (phone) {
        var replay = function () { phone.classList.remove("play"); void phone.offsetWidth; phone.classList.add("play"); };
        if ("IntersectionObserver" in window && !reduced) {
            var loop = null;
            new IntersectionObserver(function (en) {
                if (en[0].isIntersecting) { replay(); loop = loop || setInterval(replay, 9000); }
                else if (loop) { clearInterval(loop); loop = null; }
            }, { threshold: 0.35 }).observe(phone);
        } else phone.classList.add("play");
    }

    // ── Pastdagi "Demo" tugmasi (mobil) ─────────────────────────────────────
    var sticky = $("[data-sticky]"), demoSec = $("#demo"), heroSec = $(".hero");
    if (sticky && demoSec && heroSec) {
        var upd = function () {
            var past = heroSec.getBoundingClientRect().bottom < 0;
            var r = demoSec.getBoundingClientRect();
            var atDemo = r.top < window.innerHeight && r.bottom > 0;
            sticky.classList.toggle("show", past && !atDemo);
        };
        window.addEventListener("scroll", upd, { passive: true });
        upd();
    }

    // ── Demo formasi ────────────────────────────────────────────────────────
    var form = $("[data-demo-form]");
    if (!form) return;
    var msgs = $("[data-msgs]", form);
    var errBox = $("[data-err]", form);
    var btn = $("[data-submit]", form);
    var okBox = $("[data-ok]");
    var planInput = $("[data-plan-input]", form);
    var phoneInput = $("[data-phone-input]", form);
    var showErr = function (text) { errBox.textContent = text; errBox.classList.add("show"); };
    var clearErr = function () { errBox.classList.remove("show"); $$(".bad", form).forEach(function (e) { e.classList.remove("bad"); }); };

    // Paket kartasidagi "Taklif olish" — paket formaga yoziladi
    $$("[data-plan]").forEach(function (a) {
        a.addEventListener("click", function () { if (planInput) planInput.value = a.getAttribute("data-plan"); });
    });

    if (phoneInput) {
        phoneInput.addEventListener("focus", function () { if (!phoneInput.value) phoneInput.value = "+998 "; });
        phoneInput.addEventListener("blur", function () { if (phoneInput.value.trim() === "+998") phoneInput.value = ""; });
    }

    form.addEventListener("submit", function (ev) {
        if (!window.fetch || !window.FormData) return; // oddiy POST (PRG)
        ev.preventDefault();
        clearErr();
        var name = form.elements["Input.FullName"], company = form.elements["Input.Company"], tel = form.elements["Input.Phone"], email = form.elements["Input.Email"];
        var missing = [name, company, tel].filter(function (f) { return !f.value.trim(); });
        if (missing.length) { missing.forEach(function (f) { f.classList.add("bad"); }); missing[0].focus(); showErr(msgs.getAttribute("data-required")); return; }
        var digits = tel.value.replace(/\D/g, "");
        if (digits.length < 9 || digits.length > 15) { tel.classList.add("bad"); tel.focus(); showErr(msgs.getAttribute("data-phone")); return; }
        if (email.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) { email.classList.add("bad"); email.focus(); showErr(msgs.getAttribute("data-email")); return; }

        var label = btn.innerHTML;
        btn.disabled = true;
        btn.textContent = btn.getAttribute("data-sending");
        fetch(form.getAttribute("action"), {
            method: "POST",
            body: new FormData(form),
            headers: { "Accept": "application/json" },
            credentials: "same-origin"
        }).then(function (r) {
            return r.json().catch(function () { return { ok: false }; });
        }).then(function (res) {
            if (res && res.ok) {
                if (res.title) $("[data-ok-title]").textContent = res.title;
                if (res.text) $("[data-ok-text]").textContent = res.text;
                form.classList.add("hide");
                okBox.classList.add("show");
                okBox.scrollIntoView({ behavior: reduced ? "auto" : "smooth", block: "center" });
            } else {
                showErr((res && res.error) || msgs.getAttribute("data-generic"));
            }
        }).catch(function () {
            showErr(msgs.getAttribute("data-generic"));
        }).then(function () {
            btn.disabled = false;
            btn.innerHTML = label;
        });
    });
    $$("input, textarea", form).forEach(function (f) { f.addEventListener("input", function () { f.classList.remove("bad"); }); });
})();
