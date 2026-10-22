# Tizim rangi va kunduzgi/tungi rejim

## Foydalanuvchi uchun

- **Tizim rangi** — Sozlamalar → Korxona ma'lumotlari → "Tizim rangi". Tayyor palitradan tanlang yoki HEX
  (`#RRGGBB`) kiriting. O'zgarish shu sahifada darhol ko'rinadi, "Saqlash"dan keyin hamma uchun qo'llanadi
  (kesh 10 daqiqagacha; saqlagan administratorda darhol).
- Standart rang — neytral indigo `#3B5BFD`. **iPack**: palitradagi "Yashil" (`#198754`) avvalgi ko'rinishni
  aynan qaytaradi (gradientli tugmalar, och yashil menyu, `#33CC00` jadval havolalari).
- **Kunduzgi / tungi rejim** — yuqori paneldagi ◐ tugmasi: Kunduzgi, Tungi, Tizim sozlamasi (standart —
  operatsion tizim/brauzer `prefers-color-scheme` ga ergashadi). Tanlov brauzerda saqlanadi (`localStorage`
  `erp-theme`), har foydalanuvchi va qurilma uchun alohida.

## Texnik tuzilma

| Qism | Fayl |
|---|---|
| Rang saqlanadi | `COMPANIES.BrandColor` (migratsiya 005; `NULL` = standart neytral rang) |
| Hosilalar (hover, soft, kontrastli matn, tungi variantlar) | `Services/Common/BrandingService.cs` — `BuildTokens`, `RenderCssVariables` |
| `<head>` dagi o'zgaruvchilar | `<style id="brand-vars">` — `_Layout`, `_AdminPanelLayout`, Login, xato sahifalari |
| Jonli ko'rinish | `Branding/Vars?color=%23RRGGBB` (faqat CSS qaytaradi, saqlamaydi) |
| Rang tanlagich | `Views/Company/_BrandColorPicker.cshtml` |
| ERP qoidalari (avval `_Layout` ichida edi) + tungi rejim qoidalari | `assets/css/theme.css` |
| Frest shablonining `#5A8DEE` qoidalari → o'zgaruvchi | `assets/css/theme-accent.css` (generatsiya) |
| Frest `dark-layout.css` → `html[data-theme="dark"]` | `assets/css/theme-dark.css` (generatsiya) |
| Rejim almashtirish, ApexCharts sozlamasi, `accentColor()` | `assets/js/theme.js` (`<head>` da sinxron) |

CSS o'zgaruvchilari: `--accent`, `--accent-2` (gradient oxiri), `--accent-hover`, `--accent-active`,
`--accent-contrast` (accent fonidagi matn), `--accent-soft`, `--accent-ring` (fokus), `--accent-text`,
`--accent-text-hover`, `--accent-text-visited`, `--accent-table-link`, `--accent-table-link-hover`.
Fon/chegara uchun `--accent`, matn uchun `--accent-text` (u oq fonda ham, tungi fonda ham kamida 4.5:1 kontrastga
yetkaziladi).

Yangi sahifada rang yozmang: `var(--accent)` / `var(--accent-text)` yoki Bootstrap `primary` sinflaridan
foydalaning. Grafik (ApexCharts) uchun — `accentColor()`. Fon uchun qattiq `#fff` / `#f8f9fa` yozilsa, tungi rejimda
`theme.css` dagi umumiy qoida uni to'q yuzaga almashtiradi, lekin yaxshisi `card` / `bg-light` sinflari.

`theme-accent.css` va `theme-dark.css` ni qo'lda tahrir qilmang — Frest fayllari o'zgarsa:

```
python tools/theme/build_theme_css.py
```

(`tahsinERP/` papkasidan). Fayllar `?v=` versiya bilan ulanadi (`Helpers/AssetVersion.cs`), statik kesh
(1 soat) yangilanishdan keyin eski CSS ni ushlab qolmaydi.

## Boshqa qismlar

- **Distributsiya va Sayt boshqaruvi** (ERP ichidagi `Areas/Distribution`, `Areas/Site`) — `_Layout` orqali, rang va
  rejim avtomatik.
- **Mini App** (`tahsinERP.Bot/wwwroot/app`) — Telegram mavzusiga (`--tg-theme-*`) ergashadi: tungi rejim va tugma
  rangini Telegram beradi, ERP rangi qo'llanmaydi.
- **Ommaviy sayt** (`tahsinERP.Site`) — o'z sozlamasi bor (`site.SETTINGS` → `PrimaryColor`, Sayt → Sozlamalar), ERP
  rangiga bog'lanmagan.
