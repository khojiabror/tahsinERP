# tahsinERP.Site

Kompaniyalarning ochiq sayt-katalogi (Docs/Distribution/02_WEBSITE_CATALOG.md). Bitta servis — ko'p sayt (domen bo'yicha).
Boshqaruv — tahsinERP → **Sayt** moduli (Areas/Site); bu servis faqat ko'rsatadi va arizalarni qabul qiladi.

```
Brauzer ─► Caddy (TLS, on-demand) ─► tahsinERP.Site ─ pull (ETag, 60 s) ─► ERP /Site/SiteApi/Snapshot
                                          │  rasmlar: ERP SiteApi/Media → WebP (200…1600 px) → disk kesh
                                          └─ ariza → LeadOutbox → ERP /Site/SiteApi/Lead (qayta urinish bilan)
```

- **Ma'lumot:** ERP nusxasi (`tahsin_site.SiteSnapshots`) + xotiradagi indeks (qidiruv lotin/kirill, artikul, shtrix-kod; filtrlar sonlari bilan).
  ERP o'chiq bo'lsa ham sayt oxirgi nusxa bilan ishlaydi.
- **Imzo:** `DstApiSignature` (bot bilan bir xil master kalit `Site__MasterKey` = ERP `TAHSINERP_BOT_KEY`); ERP tenant — `--erp-tenant` (bitta bazali ERP — `default`).
- **Sahifalar (SSR):** `/{uz|ru}/`, `/catalog`, `/c/{slug}`, `/p/{slug}`, `/search?q=`, `/page/{slug}`, `/cart`; `sitemap.xml` (hreflang), `robots.txt`, `llms.txt`;
  JSON-LD (Product/Offer, BreadcrumbList, Organization, WebSite). Sahifa keshi 60 s (yangi nusxada tozalanadi).
- **Dizayn:** vercel/shop (MIT) dizayn tizimi asosida qo'lda yozilgan CSS (`wwwroot/css/site.css`) — brend: rang, shrift, radius (ERP sozlamasi).
- **Xavfsizlik:** CSP (inline skript yo'q), tavsif matni HTML emas (SafeText), ariza — yashirin maydon, vaqt tuzog'i, IP/telefon chegarasi.

## Sozlash

| Kalit | Qiymat |
|---|---|
| `Site__MasterKey` | ≥ 32 belgi, ERP `TAHSINERP_BOT_KEY` bilan bir xil |
| `Site__BaseDomain` | `tahsin.uz` → `{slug}.tahsin.uz` |
| `ConnectionStrings__Site` | `tahsin_site` (ishga tushishda o'zi yaratiladi) |
| `Site__DataRoot` | rasm keshi papkasi |

## Buyruqlar

```
dotnet tahsinERP.Site.dll tenant add firma1 https://erp.example.uz/ [--erp-tenant firma1]
dotnet tahsinERP.Site.dll snapshot sync firma1              # ERP'dan hozir olish (tekshiruv)
dotnet tahsinERP.Site.dll snapshot import demo snapshot.json # ERP'siz namoyish (rasmlar: {DataRoot}/demo/orig/{id}_{hash})
```

Namoyish nusxasini lokal ERP bazasidan olish: `tahsinERP.Tests` → `SiteDemoExport` (SITE_DEMO_OUT muhit o'zgaruvchisi bilan; bazaga yozilgani rollback).
Lokal sinov: `?tenant=demo` (`Site__AllowTenantQuery=true`, faqat localhost).

Deploy: `deploy/tahsin-site.service`, `deploy/Caddyfile` (on-demand TLS — kompaniya domenlari uchun sertifikat avtomatik, faqat ma'lum domenlarga).
