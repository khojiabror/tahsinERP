// Analitika (sayt sozlamasida yoqilgan bo'lsa): Google Analytics 4 va Yandex Metrika. Inline skript yo'q (CSP).
(function () {
  'use strict';
  var root = document.documentElement;
  var ga = root.getAttribute('data-ga');
  var ym = root.getAttribute('data-metrika');

  if (ga && /^G-[A-Z0-9]+$/.test(ga)) {
    var s = document.createElement('script');
    s.async = true;
    s.src = 'https://www.googletagmanager.com/gtag/js?id=' + ga;
    document.head.appendChild(s);
    window.dataLayer = window.dataLayer || [];
    window.gtag = function () { window.dataLayer.push(arguments); };
    window.gtag('js', new Date());
    window.gtag('config', ga);
  }

  if (ym && /^\d+$/.test(ym)) {
    window.ym = window.ym || function () { (window.ym.a = window.ym.a || []).push(arguments); };
    window.ym.l = 1 * new Date();
    var m = document.createElement('script');
    m.async = true;
    m.src = 'https://mc.yandex.ru/metrika/tag.js';
    document.head.appendChild(m);
    window.ym(+ym, 'init', { clickmap: true, trackLinks: true, accurateTrackBounce: true });
  }
})();
