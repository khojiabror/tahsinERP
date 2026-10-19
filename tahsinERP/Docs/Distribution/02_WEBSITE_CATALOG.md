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

Ruxsatlar: yangi `PERMISSIONMODULES`, masalan `SiteSettings`, `SiteCatalog`, `SiteLeads`. Paketda `ProductModule.Website` (`EditionModules`). Qaysi paketga kirishi 9-bo'limda.

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

Jami ~6.5 hafta. Distributsiya MVP'si bilan to'qnashmaydi. Taklif qilinadigan tartib: S5 (hisobotlar) va S6 (pilot) dan keyin. Agar sayt sotuv uchun muhimroq bo'lsa, S5 bilan parallel olib borish mumkin: W0 va W1 ERP'da, W2 alohida loyihada.

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

## 9. Sizdan qaror kerak

1. **Domen:** har kompaniya o'z domeni bilanmi, yoki standart subdomen (`firma.tahsin.uz` — asosiy domen nomi Control Panel rejasida ham ochiq) va xohlasa o'z domeni?
2. **Narx:** saytda narx ko'rinadimi (bitta "chakana / sayt" narx turi), "narxni so'rang" bo'ladimi, yoki kompaniya o'zi tanlaydimi? Tavsiya: kompaniya tanlaydi.
3. **Paket:** "Sayt" qaysi tarifga kiradi (START / BIZNES / KORP) yoki alohida qo'shimcha xizmat bo'ladimi?
4. **Dizayn:** bitta shablonni men tayyorlaymi (zamonaviy, mobil-birinchi) yoki tayyor dizayn yoki maket bormi?
5. **Tartib:** sayt S5/S6 dan keyinmi yoki parallel?
