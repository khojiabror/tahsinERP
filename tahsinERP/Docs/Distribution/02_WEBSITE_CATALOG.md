# Sayt-katalog va uning tahsinERP ichidagi admin paneli: reja

Sana: 2026-10-19. Asos: foydalanuvchi talabi — Mini App'dan tashqari, har bir kompaniyaning ochiq sayti bo'lsin. Saytda mahsulotlar katalogi turadi, sayt esa tahsinERP ichidan boshqariladi.
Bog'liq hujjatlar: `00_PLAN.md` (umumiy reja), `01_PHASE1_MVP.md` (3-bo'lim — bot arxitekturasi, 14-bo'lim — S4 natijasi).

---

## 1. Maqsad va chegaralar

**Nima beradi:**
- Har kompaniya o'z domenida (`stroymarket.uz`) yoki bizning subdomenda (`firma.tahsin.uz`) ochiq katalog saytiga ega bo'ladi.
- Katalogda kategoriyalar, brendlar, qidiruv, filtrlar bor. Tovar sahifasida fotolar, tavsif, xususiyatlar, birliklar, narx va "bor / buyurtmaga" belgisi ko'rsatiladi.
- Mijoz savatcha orqali **ariza** qoldiradi. Ariza ERP'ga tushadi, menejer uni bir tugma bilan buyurtmaga aylantiradi.
- Saytda kompaniya haqida, yetkazib berish va kontaktlar (xarita bilan) sahifalari, bannerlar bo'ladi. Tillar: uz/ru.
- Saytning hamma narsasi tahsinERP'dagi **"Sayt" moduli**dan boshqariladi. Alohida CMS ham, dasturchi ham kerak emas.
- Tovar, narx va qoldiq ERP'dan avtomatik keladi, ikkinchi marta kiritilmaydi.

**1-versiyaga kirmaydi** (8-bo'limga qarang):
- Shaxsiy kabinet va shaxsiy narxlar.
- Onlayn to'lov.
- Bir nechta dizayn shabloni.
- Blog.
- Marketpleyslar (Uzum, Yandex Market) bilan integratsiya.

Modul faqat distributsiyaga bog'lanmaydi: ishlab chiqarish korxonasi ham o'z mahsulot katalogini chiqarishi mumkin. Shuning uchun u alohida `site` sxemasi va `Areas/Site` sifatida quriladi. Narx va qoldiq bor joyda distributsiya ma'lumotidan (narx turlari, savdo omborlari), yo'q joyda oddiy `PRODUCTS` dan foydalanadi.

---

## 2. Hozir nima bor

| Kerak | Bor | Holat |
|---|---|---|
| Tovar, kategoriya, birlik, shtrix-kod, brend | `PRODUCTS`, `CATEGORIES`, `dst.PRODUCT_UNITS`, `PRODUCT_BARCODES`, `BRANDS`, `PRODUCT_PROFILES` (NameRu, IsSaleActive) | ✅ |
| Narx | `dst` narx turlari va narx varag'i (`PricingService`) | ✅, "saytda ko'rinadigan narx turi" kerak |
| Qoldiq | Savdo omborlari bo'yicha mavjud qoldiq (`CatalogService`) | ✅ |
| Fotolar | `dbo.PRODUCTIMAGES` (varbinary, bazada) | ⚠️ saytga yaramaydi: o'lcham va tartib yo'q, CDN'ga qo'yib bo'lmaydi. Media ombori kerak (4-bo'lim) |
| Tavsif, xususiyatlar, SEO, slug, nashr belgisi | — | ❌ |
| Sahifalar, bannerlar, sayt sozlamalari | — | ❌ |
| Ariza → mijoz → buyurtma | Buyurtma oqimi (`OrderService.SaveDraft`), bildirishnomalar (bot) | ✅ asos bor, ariza qatlami yo'q |
| Ochiq servis va ERP bilan xavfsiz aloqa | `tahsinERP.Bot` naqshi: imzoli API (`DstApiSignature`), pull, outbox | ✅ naqsh tayyor, shuni qayta ishlatamiz |

---

## 3. Arxitektura

```
 Brauzer / Google ──HTTPS──► CDN (CloudFront) ──► tahsinERP.Site (ASP.NET Core .NET 10, SSR)
                                                    │  domen → tenant (Host sarlavhasi)
                                                    │  tahsin_site bazasi: katalog nusxasi, media keshi, ariza outbox'i
                                                    │
                     pull (versiya, delta) ◄────────┤  har 30–60 s: SiteApi/Version → o'zgarganlar
                     imzoli so'rov (HMAC) ─────────►│  ariza: SiteApi/Lead (outbox orqali, ERP o'chiq bo'lsa kutadi)
                                                    ▼
                                          tahsinERP (ERP, .NET 4.8)
                                          Areas/Site — admin paneli, site.* jadvallari
                                          SiteApi — faqat imzoli so'rovlar (DstApi kabi)
```

**Asosiy qarorlar (tavsiya):**
1. **Sayt ERP ichida ishlamaydi.** ERP login ortida turadi (`<deny users="?">`). Uni Google va internet trafigiga ochish xavf va yuk qo'shadi, keshlash va SEO esa boshqa talab qiladi. Sayt alohida, ochiq servis bo'ladi, ERP esa ochilmaydi. Bot bilan bir xil yondashuv.
2. **Alohida loyiha `tahsinERP.Site`.** U bot bilan bir serverda ishlashi mumkin, lekin jarayoni alohida, chunki trafik, kesh va deploy ritmi boshqa. Umumiy kod `tahsinERP.Integration` kutubxonasiga ko'chiriladi: `ErpClient`, imzo, tenant reyestri, outbox naqshi. Uni bot ham, sayt ham ishlatadi.
3. **Ma'lumot pull usulida keladi.** Har `site.*` o'zgarishi va nashr qilingan tovarning narx/qoldiq o'zgarishi umumiy versiyani oshiradi. Sayt versiyani so'raydi va faqat o'zgarganlarni oladi. ERP sayt manzilini bilmaydi va tashqariga hech narsa chaqirmaydi. Sayt o'chsa yoki qayta tursa, nusxa bazadan tiklanadi.
4. **Narx va qoldiq:**
   - Narx nusxada saqlanadi.
   - Qoldiq "bor / kam / buyurtmaga" ko'rinishida beriladi, aniq son sozlamaga bog'liq.
   - Savatchada aniq qoldiq va narx ERP'dan 60 soniyalik kesh bilan qayta tekshiriladi.
5. **SSR (server tomonda HTML).** Google katalogni to'liq indekslashi uchun SPA emas, Razor Pages ishlatiladi. JS minimal: savatcha, galereya, filtrlar.
6. **Multi-tenant:**
   - Bitta servis ko'p saytga xizmat qiladi, tenant domen bo'yicha topiladi.
   - Domenni ERP admin panelida kiritadi, bizning panel tasdiqlaydi.
   - Sertifikat CloudFront+ACM yoki Let's Encrypt orqali.
   - Subdomen (`*.tahsin.uz`) wildcard sertifikat bilan darhol ishlaydi.

---

## 4. Ma'lumotlar modeli (ERP, `site` sxemasi)

| Jadval | Asosiy maydonlar |
|---|---|
| `SETTINGS` | Key/Value. Qiymatlar:<br>• sayt nomi, logo, rang, kontaktlar, ish vaqti, xarita nuqtasi, ijtimoiy tarmoqlar<br>• ko'rinadigan narx turi yoki "narxni yashirish"<br>• qoldiq ko'rinishi (aniq / bor-yo'q / yashirin)<br>• ariza qabul qilish yoqilgan, ariza oluvchilar<br>• analitika ID, SEO standartlari |
| `DOMAINS` | Host (UNIQUE), IsPrimary, Status (kutilmoqda / tasdiqlangan), VerifiedAt |
| `CATEGORY_CONTENT` | CategoryID (PK), IsPublished, Slug, NameUz/Ru, DescriptionUz/Ru, MediaID, SortOrder, SeoTitle/Description |
| `PRODUCT_CONTENT` | ProductID (PK), IsPublished, Slug (UNIQUE), TitleUz/Ru (bo'sh bo'lsa — tovar nomi), DescriptionUz/Ru (tozalangan HTML), Badges (yangi / aksiya / xit), SortOrder, SeoTitle/Description, UpdatedAt |
| `PRODUCT_ATTRIBUTES` | ProductID, NameUz/Ru, ValueUz/Ru, SortOrder (masalan: "Marka: M400", "Og'irlik: 50 kg") |
| `MEDIA` | ID, Key (fayl ombori: `tenant/yyyy/MM/guid.webp`), ContentType, Width, Height, Hash, Alt |
| `PRODUCT_MEDIA` | ProductID, MediaID, SortOrder. Mavjud `PRODUCTIMAGES` bir martalik import qilinadi |
| `PAGES` | ID, Slug, TitleUz/Ru, BodyUz/Ru, IsPublished, ShowInMenu, SortOrder |
| `BANNERS` | ID, MediaID, TitleUz/Ru, Link, ActiveFrom/To, SortOrder |
| `LEADS` | ID, CreatedAt, ClientRequestId (UNIQUE), Name, Phone, CompanyName, INN, Message, Items (JSON: tovar, birlik, miqdor), Language, Source (sayt / domen), Status (Yangi / Ko'rildi / Buyurtma / Rad), CustomerID, OrderID, ProcessedBy/At, RejectReason |
| `VERSION` | Bitta ketma-ketlik. Kontent jadvallari va nashr qilingan tovarning narx/qoldiq o'zgarishi `ChangeVersion` ni yangilaydi |

Fotolar uchun bot uchun yozilgan `IFileStore` naqshi ishlatiladi: hozircha disk, keyin S3. Admin yuklagan rasm WebP'ga aylantiriladi va 3 o'lchamda (200/600/1200 px) saqlanadi. Bazaga faqat kalit yoziladi.

---

## 5. Admin paneli (tahsinERP → "Sayt" moduli)

| Sahifa | Nima qiladi |
|---|---|
| **Boshqaruv** | Sayt holati (yoniq / o'chiq, oxirgi sinxron, domenlar), bugungi arizalar, ko'p ko'rilgan tovarlar, "Saytni ochish" va "Ko'rib chiqish" (preview) |
| **Sozlamalar** | Nom, logo, ranglar (bitta yaxshi shablon, rang va logo almashadi), kontaktlar va xarita, tillar, narx va qoldiq ko'rinishi, ariza sozlamalari, SEO standartlari, analitika |
| **Katalog** | Kategoriya va tovarlar ro'yxati "nashr qilingan / qilinmagan" filtri bilan.<br>• Ommaviy amallar: kategoriya bo'yicha nashr qilish, belgi qo'yish.<br>• Tovar kartasida "Sayt" yorlig'i: tavsif (uz/ru), xususiyatlar, fotolar (sudrab tartiblash), slug, SEO.<br>• Excel import/eksport tavsif va xususiyatlar uchun.<br>• Ogohlantirishlar: fotosiz, narxsiz va tavsifsiz nashr qilingan tovarlar. |
| **Sahifalar va bannerlar** | Biz haqimizda, yetkazib berish, kontaktlar va boshqa sahifalar (oddiy matn muharriri), bosh sahifa bannerlari muddati bilan |
| **Arizalar** | Ro'yxat (holat filtri).<br>• Arizani ochganda telefon bo'yicha mavjud mijoz taklif qilinadi.<br>• **"Buyurtma yaratish"** — mavjud buyurtma formasiga tovarlar bilan o'tadi (`D_ORDERS`, manba = sayt). Rad etish sabab bilan.<br>• Yangi ariza rahbar/menejerga **Telegram bot** orqali xabar qilinadi (mavjud `NotificationService`). |
| **Domenlar** | Domen qo'shish, DNS ko'rsatmasi (CNAME), tasdiq holati |

Ruxsatlar: yangi `PERMISSIONMODULES`, masalan `SiteSettings`, `SiteCatalog`, `SiteLeads`. Litsenziyada `ProductModule.Website` — paketdan tashqari alohida xizmat (9-bo'lim).

---

## 6. Ochiq sayt (`tahsinERP.Site`)

- **Sahifalar:**
  - Bosh sahifa: bannerlar, kategoriyalar, yangi va aksiya tovarlar.
  - Kategoriya: filtr (brend, xususiyat, "bor"), saralash, sahifalash.
  - Tovar: galereya, birliklar va narx, "bor / buyurtmaga", xususiyatlar, o'xshash tovarlar.
  - Qidiruv (nom, artikul, shtrix-kod).
  - Savatcha va ariza formasi.
  - Matnli sahifalar, kontaktlar.
  - 404.
- **Til:** `/uz/...` va `/ru/...`, `hreflang`. Brauzer tiliga qarab birinchi yo'naltirish.
- **SEO:**
  - Slug URL'lar, `sitemap.xml` (har tilga), `robots.txt`, canonical.
  - OpenGraph (Telegram'da chiroyli ko'rinish).
  - schema.org `Product` / `Offer` / `BreadcrumbList` / `Organization`.
- **Dizayn:** 10-bo'lim (`vercel/shop` asosi + B2B katalog naqshlari, Razor + Tailwind + htmx).
- **Tezlik:** HTML 60 soniya keshlanadi va versiya o'zgarganda tozalanadi. Rasmlar WebP, `srcset`, lazy, CDN orqali. Maqsad: mobil Lighthouse ≥ 90.
- **Ariza:**
  - Forma maydonlari: ism, telefon, tashkilot (ixtiyoriy) va savatcha.
  - Anti-spam: honeypot, IP va telefon bo'yicha rate limit, ixtiyoriy Cloudflare Turnstile.
  - Ariza avval sayt outbox'iga yoziladi, keyin `SiteApi/Lead` ga imzo va `ClientRequestId` bilan ketadi. ERP o'chiq bo'lsa ariza yo'qolmaydi.
  - Mijozga "Arizangiz qabul qilindi" ko'rsatiladi. Kerak bo'lsa Telegram orqali kuzatish uchun bot havolasi beriladi.
- **Telegram bilan bog'lash:** tovar sahifasida "Telegram orqali so'rash" (bot deep link). Kompaniyaning boti bo'lsa, mijoz botga ulanib holat xabarlarini oladi (mavjud mijoz bildirishnomalari).
- **Xavfsizlik:**
  - Saytga ichki narxlar ketmaydi: tannarx, boshqa narx turlari, mijozlar.
  - Faqat nashr qilingan maydonlar sinxronlanadi.
  - HTML tavsif ERP'da tozalanadi (allowlist).
  - CSP sarlavhalari.

---

## 7. Bosqichlar

| # | Bosqich | Ichida | Muddat | Natija |
|---|---|---|---|---|
| **W0** | Poydevor | `tahsinERP.Integration` kutubxonasi (bot kodidan ko'chirish, bot testlari o'tishi shart); `site` sxemasi migratsiyasi; `Areas/Site` skeleti; ruxsatlar; Edition moduli; `SiteApi` (imzo, Version, Delta, Lead) | 1 hafta | Imzoli `SiteApi/Version` javob beradi |
| **W1** | Admin: katalog | Sozlamalar, kategoriya va tovar kontenti, media ombori (yuklash, WebP, o'lchamlar, `PRODUCTIMAGES` importi), xususiyatlar, Excel import/eksport, ogohlantirishlar | 1.5 hafta | 300 tovar bir kunda nashrga tayyorlanadi |
| **W2** | Ochiq sayt | `tahsinERP.Site`: tenant domen bo'yicha, pull sinxron (versiya, delta, `tahsin_site` bazasi), bosh / kategoriya / tovar / qidiruv / sahifalar, uz/ru, SEO, rasm va CDN keshi, sahifalar va bannerlar | 2 hafta | Google indekslay oladigan, telefonda tez ochiladigan katalog |
| **W3** | Arizalar | Savatcha va forma, anti-spam, sayt outbox → `SiteApi/Lead`, ERP'da arizalar sahifasi, mijozni topish, "Buyurtma yaratish", bot xabari | 1 hafta | Saytdagi ariza 1 daqiqada menejer Telegram'ida, bir tugmada buyurtma |
| **W4** | Domen va ishga tushirish | Domenlar sahifasi va tasdiq, nginx/CloudFront, sertifikatlar, analitika, Lighthouse, deploy hujjati, bitta kompaniya bilan pilot | 1 hafta | Mijoz o'z domenida ishlaydigan sayt |

Jami ~6.5 hafta. **Qaror:** distributsiya S5 va S6 tugagandan keyin boshlanadi.

---

## 8. Keyingi versiyalar

- **B2B kabinet:**
  - Kirish telefon va Telegram orqali bir martalik kod bilan.
  - Mijoz o'z narx turini, qarzi va akt sverkasini, buyurtmalar tarixini ko'radi va qayta buyurtma qiladi.
  - Ariza o'rniga to'g'ridan-to'g'ri buyurtma beriladi (limit tekshiruvi bilan, xuddi Mini App'dagidek).
- **Onlayn to'lov:** Click, Payme, Uzum. Moliyaga yozish S3 to'lov oqimi orqali.
- **Dizayn va kontent:**
  - Bir nechta shablon.
  - Blog va yangiliklar.
  - Aksiyalar sahifasi (aksiya modeli 3-bosqichda).
- **Marketpleyslar:** Uzum Market va Yandex Market uchun tovar fidlari (YML/XML) — o'sha nashr qilingan katalogdan.

---

## 9. Qarorlar (foydalanuvchi, 2026-10-20)

1. **Domen:** standart subdomen `{slug}.<asosiy domen>` (wildcard sertifikat, darhol ishlaydi). Kompaniya xohlasa o'z domenini ulaydi (CNAME + tasdiq, 5-bo'lim "Domenlar"). Asosiy domen nomi Control Panel rejasida hali ochiq.
2. **Narx:** har kompaniya o'zi tanlaydi: narx turi ko'rsatiladi, "narxni so'rang" yoki yashirin. Qoldiq ko'rinishi ham kompaniya sozlamasi.
3. **Paket:** sayt **alohida pullik xizmat** (add-on), START/BIZNES/KORP paketlariga kirmaydi.
   - Litsenziyada alohida modul belgisi: `ProductModule.Website`, Edition'dan mustaqil.
   - Control Panel'da obuna qatori sifatida sotiladi. Bu Control Panel'ning add-on qo'llab-quvvatlashiga bog'liq va W0'da tekshiriladi.
4. **Dizayn:** 10-bo'limda tanlangan asos.
5. **Tartib:** distributsiya S5 (hisobotlar) va S6 (pilot) tugagandan keyin boshlanadi.

---

## 10. Dizayn: shablon tanlovi (2026-10-20)

**Qanday tanlandi.** E-commerce shablonlari bo'yicha forum va sharhlar o'rganildi:
- Shopify Community muhokamalari: "Horizon vs Dawn", "qaysi premium tema".
- Tema sharhlari: tezlik va konversiya, B2B / katta katalog bo'yicha.
- Baymard UX tadqiqotlari: katalog sahifasi va filtrlar.
- Ochiq kodli storefront reytinglari.

Reddit va Hacker News bu vosita bilan o'qilmadi (Reddit botlarni bloklaydi). Topilgan sharhlarning ko'pi tema sotuvchilarining blogi, ularning baholari bir-biriga zid. Shuning uchun xulosa mustaqil manbalarga (Shopify Community, Baymard) va litsenziyaga tayanadi.

**Asosiy xulosalar:**
- Tezlik pul bilan bog'liq: Shopify ma'lumotiga ko'ra LCP 100 ms sekinlashsa, konversiya ~3,5% kamayadi. Demo ko'rsatkichi emas, haqiqiy tovarlar va rasmlar bilan bo'lgan tezlik muhim.
- Forumlarda eng ko'p maqtaladigan Shopify temalari:
  - Dawn — barqaror va tez.
  - Horizon — yangi va moslashuvchan, lekin ayrimlarda INP yomonlashgan, eski brauzerlarni yomon qo'llaydi.
  - Katta katalog / B2B uchun: Warehouse, Empire, Hyper, Athens — zich setka, kuchli filtrlar, tez buyurtma.
- **Lekin ularning kodini olib bo'lmaydi.** Dawn va Horizon litsenziyasi faqat Shopify bilan ishlaydigan temalar uchun ruxsat beradi ("All other uses ... strictly prohibited"). Warehouse/Empire kabi pullik temalar ham faqat Shopify'da. Biz ulardan faqat UX naqshlarini olamiz (zich setka, filtrlar, tez qo'shish), kod va dizaynni ko'chirmaymiz.
- Ochiq kodli (MIT) storefrontlarning holati:
  - Vercel Next.js Commerce (14k yulduz) va Medusa Next.js starter arxivlangan.
  - Ularning o'rnini `vercel/shop` (MIT, Next.js 16, Tailwind CSS 4, shadcn/ui, Schema.org, sitemap) va Medusa `dtc-starter` (MIT) egalladi.
  - Ikkalasi ham chakana (DTC) savdo uchun: katta katalog filtrlari yo'q yoki zaif.

**Tanlov:**
- **Vizual asos — `vercel/shop` dizayn tizimi** (MIT, shadcn/ui ham MIT). Toza, zamonaviy, mobil-birinchi, kuzatilyapti, SEO uchun tayyor naqshlarga ega (Schema.org, sitemap, `llms.txt`).
- **Katalog UX — B2B / katta katalog naqshlari** (Warehouse/Empire forum tavsiyalari + Baymard):
  - **Filtrlar:** brend, kategoriya, xususiyatlar va "bor" filtrlari; har variant yonida natija soni; bir guruh ichida bir nechtasini tanlash. Tanlangan filtrlar tepada "chip" bo'lib turadi (Baymard: saytlarning 28% buni qilmaydi).
  - **Ro'yxat:** setka ↔ zich jadval almashtirgichi.
  - **Ro'yxat kartochkasi:** artikul, birlik, narx va qoldiq belgisi bir xil joyda.
  - **Tez qo'shish:** ro'yxatning o'zidan, birlik tanlash va miqdor bilan.
  - **Qidiruv:** doim ko'rinib turadi, artikul va shtrix-kod bo'yicha ham.
  - **Savatcha → ariza.**
- **Texnik ko'chirish:**
  - Next.js/React ishlatilmaydi. Ikkinchi runtime qo'shmaslik uchun dizayn **Razor Pages (SSR) + Tailwind CSS 4** ga ko'chiriladi. Tailwind build vaqtida standalone CLI bilan ishlaydi, Node.js kerak emas.
  - shadcn/ui komponentlarining belgilanishi Razor partial'lariga o'tkaziladi.
  - Interaktivlik **htmx** (BSD-0) va kichik vanilla JS bilan: filtr va sahifalash server HTML'ini almashtiradi. Google uchun har filtr holati oddiy URL bo'lib qoladi.
  - Litsenziya: MIT matni `THIRD-PARTY-NOTICES.md` ga qo'shiladi.
- **Brend:** logo, asosiy rang, shrift (Google Fonts'dan 3 ta variant), burchak radiusi. Kompaniya bularni admin panelda tanlaydi, CSS o'zgaruvchilari orqali qo'llanadi. Qorong'u rejim ham bo'ladi.
- **Sifat mezonlari (W4 qabul):**
  - Mobil Lighthouse: Performance ≥ 90, Accessibility ≥ 95, SEO = 100.
  - Core Web Vitals: LCP < 2,5 s, INP < 200 ms, CLS < 0,1. Haqiqiy katalogda (≥ 1000 tovar, fotolar bilan) o'lchanadi.
  - Kirill va lotin matnlar sinab ko'riladi, 320 px ekranda gorizontal scroll bo'lmasligi kerak.

**Manbalar:**
- [Horizon versus Dawn — Shopify Community](https://community.shopify.com/t/horizon-versus-dawn/556215)
- [Is Horizon the new Dawn? — Shopify Community](https://community.shopify.com/t/is-horizon-the-new-dawn/556102)
- [Horizon: high INP — Shopify Community](https://community.shopify.com/t/has-anyone-successfully-debugged-high-inp-scores-on-the-horizon-theme-specifically/586915)
- [Which premium theme — Shopify Community](https://community.shopify.com/t/which-premium-theme-should-i-choose-and-why/584317)
- [Best Shopify themes 2026: speed, conversions](https://qualimero.com/en/blog/best-shopify-themes)
- [Best Shopify themes for B2B wholesale](https://wholesalehelper.io/blog/best-shopify-themes-for-b2b-wholesale/)
- [Baymard: PLP UX](https://baymard.com/blog/product-listing-page-plp-ux)
- [Baymard: list item design](https://baymard.com/research-articles/list-item-design-ecommerce)
- [Next.js e-commerce templates ranked](https://designrevision.com/blog/best-nextjs-ecommerce-templates.md)
- [Shopify/dawn LICENSE](https://github.com/Shopify/dawn/blob/main/LICENSE.md)
- [vercel/commerce (arxiv)](https://github.com/vercel/commerce)
- [vercel/shop](https://github.com/vercel/shop)
- [medusajs/dtc-starter](https://github.com/medusajs/dtc-starter)

---

## 11. Natija (2026-10-20, branch `feature/website`)

Foydalanuvchi buyrug'i bilan S5/S6 dan oldin bajarildi (W0–W4).

### 11.1 ERP (tahsinERP)
- **Migratsiya `021_SITE.sql`**: `site` sxemasi (4-bo'limdagi jadvallar), standart sozlamalar, ruxsat modullari `SiteHome`, `SiteSettings`, `SiteCatalog`, `SitePages`, `SiteLeads` (+ Seed/105). Lokal bazada va `DB_DST_UI` da qo'llangan (zaxira `DBTHSN-IPACK_pre_021_SITE_20261020.bak`), qayta ishga tushirish xatosiz.
- **Litsenziya — alohida xizmat:** Edition qatorida `+SITE` (`KORP+SITE`, `START+SITE`); kalit formati o'zgarmaydi. `ProductModule.Website` hech qaysi paketga kirmaydi; "Paketda yo'q" sahifasi qo'shimcha xizmat uchun alohida matn ko'rsatadi. Eski/cheklovsiz kalitlarda — ochiq.
- **Admin paneli (`Areas/Site`, menyuda "Sayt"):** boshqaruv (holat, ko'rsatkichlar, fotosiz/tavsifsiz/narxsiz tovarlar, oxirgi arizalar); sozlamalar (brend, shrift, rang, radius, tillar, narx va qoldiq rejimi, kontaktlar, xarita, ijtimoiy tarmoqlar, SEO, GA/Metrika); domenlar (qo'shish, DNS tekshiruvi — IP'lar `SiteCnameTarget` bilan solishtiriladi, asosiy domen); katalog (server tomonda sahifalanadigan jadval, ommaviy nashr, kategoriya bo'yicha nashr, kategoriya nomi/tavsifi/rasmi/SEO; tovar kartasi — nomlar va tavsif uz/ru, xususiyatlar, belgilar, slug, SEO, galereya: yuklash, tartib, alt, eski `PRODUCTIMAGES` dan import); sahifalar va bannerlar (muddat bilan); arizalar (ro'yxat, telefon bo'yicha mijoz topish, bir tugmada mijoz + `D_ORDERS` qoralama, rad etish). Yangi ariza — rahbarlarga Telegram xabari.
- **`SiteApi`** (`/Site/SiteApi/*`, imzoli, bot bilan bir xil kalit): `Snapshot` (to'liq nusxa, ETag/304, 60 s kesh, admin saqlaganda darhol yangilanadi), `Media`, `Lead` (`ClientRequestId` — takror yuborish xavfsiz). Litsenziya va `SITE` xizmati tekshiriladi; `web.config` da `Site/SiteApi` uchun location.
- Rasm: ERP'da tekshiriladi, EXIF burilishi to'g'rilanadi, ≤ 2000 px (JPEG 85% yoki shaffof PNG), SHA-256 bilan takrorlanmaydi.

### 11.2 Ochiq sayt (`tahsinERP.Site`, .NET 10)
- Bitta servis — ko'p sayt: `{slug}.{BaseDomain}` yoki ERP'da tasdiqlangan domen; `tahsin_site` bazasi (tenantlar, oxirgi nusxa, ariza navbati) — ERP o'chiq bo'lsa ham sayt ishlaydi.
- Razor Pages (SSR): bosh sahifa (bannerlar yoki sarlavha + qidiruv, kategoriyalar, aksiya/yangi/tavsiya), katalog/kategoriya/qidiruv (filtrlar — brend, xususiyatlar, "faqat mavjudlari", har birida son; saralash; setka ↔ jadval; sahifalash; filtr bosilganda faqat natijalar bloki almashadi, URL saqlanadi), tovar sahifasi (galereya, birlik tanlash, miqdor, savatga, qo'ng'iroq, xususiyatlar, tavsif, o'xshash tovarlar), savat va ariza, matnli sahifalar, 404, "tez orada" (sayt o'chiq — 503).
- SEO: kanonik URL, hreflang (uz/ru/x-default), `sitemap.xml`, `robots.txt`, `llms.txt`, JSON-LD (Product/Offer, BreadcrumbList, Organization, WebSite+SearchAction), OpenGraph; filtrlangan/qidiruv sahifalari `noindex`.
- Rasmlar: WebP (SkiaSharp, 200–1600 px), URL'da hash — 1 yil kesh. Sahifa keshi 60 s, yangi nusxada tozalanadi.
- Xavfsizlik: qat'iy CSP (inline skript yo'q, analitika faqat yoqilganda), tavsif — xavfsiz formatlangan matn, ariza: yashirin maydon, 3 soniyalik vaqt tuzog'i, IP (5/10 daq) va telefon (3/10 daq) chegarasi, outbox orqali ERP'ga qayta urinish bilan.
- Deploy: `deploy/tahsin-site.service`, `deploy/Caddyfile` — on-demand TLS (sertifikat faqat sayt servisi tasdiqlagan domenlarga, `/_tls/allowed`).

### 11.3 Rejadan farqlar
| Rejada | Qilindi | Nega |
|---|---|---|
| Tailwind CSS 4 CLI + htmx | `vercel/shop` token'lari va komponentlari asosida qo'lda yozilgan CSS + kichik vanilla JS | Tashqi binar/paket yuklamasdan; natija bir xil (SSR, progressive enhancement) |
| Versiya + delta sinxron | To'liq nusxa + ETag (304) | Sodda va ishonchli; 20 ming tovargacha yetarli, kattasida — delta |
| `tahsinERP.Integration` umumiy kutubxona | Saytda o'z kichik ERP mijozi (imzo — umumiy `DstApiSignature`) | Botni qayta yozmaslik; birlashtirish — keyingi ish |
| Tavsif — tozalangan HTML | Xavfsiz belgilash (paragraf, ro'yxat, **qalin**, sarlavha) | HTML sanitizer'siz XSS imkoniyati yo'q |
| Media — fayl ombori | ERP bazasida (`site.MEDIA`), saytda WebP kesh | Tenant bazasi bilan birga zaxiralanadi; DB-per-tenant |

### 11.4 Tekshiruv
- ERP testlari 148/148 (yangi 7: slug/domen/belgi, `+SITE` xizmati, rasm tayyorlash, nusxa — nashr qilinmagan chiqmaydi, narx/qoldiq rejimlari, ETag barqarorligi, minimal narx chiqmasligi; ariza — takroriy yuborish, nashr qilinmagan tovar rad, buyurtmaga bir marta aylanadi, mijoz telefon bilan yaratiladi). RazorCheck: 505 view, 0 xato.
- Sayt testlari 27/27 (ERP nusxa shakli, qidiruv lotin/kirill/artikul/shtrix-kod, filtr sonlari, saralash/sahifalash, XSS, JSON-LD'dan chiqib ketmaslik, ariza tekshiruvi/takror/bot tuzog'i/chegara, domen → tenant, WebP o'lchamlari, ERP imzosi).
- Brauzer (lokal, `SiteDemoExport` — lokal bazadagi 391 tovar va rasmlar, rollback bilan): bosh sahifa, katalog va filtrlar (sahifa yuklanmasdan), tovar sahifasi, savat (3 × 76 000 = 228 000), ariza yuborildi → navbatda; telefon (375 px) — gorizontal scroll yo'q; rus tili; sitemap/robots/llms; 404; CSP.
- IIS orqali haqiqiy ERP: imzoli `Snapshot` so'rovi URL avtorizatsiyasi, marshrut va imzodan o'tib litsenziyagacha yetdi (lokal baza litsenziyasiz — 403); imzosiz — 401.

**Tekshirilmagan:** litsenziyali ERP bilan uchidan-uchiga (nusxa → sayt, ariza → ERP), Lighthouse o'lchovi (bu muhitda yo'q), haqiqiy domen va Caddy sertifikati.

**Keyingi ishlar:** Control Panel'da `+SITE` qo'shimcha xizmatini sotish va kalitga yozish; tavsif/xususiyatlar uchun Excel import-eksport; `tahsinERP.Integration` umumiy kutubxonasi (bot + sayt); 8-bo'limdagi B2B kabinet va onlayn to'lov.
