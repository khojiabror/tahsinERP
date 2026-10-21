# tahsinERP.Bot

Distributsiya uchun Telegram bot va Mini App (agent, ekspeditor, rahbar, mijoz). Batafsil arxitektura va qarorlar:
`tahsinERP/Docs/Distribution/01_PHASE1_MVP.md`, 3 va 14-bo'limlar.

## Qanday ishlaydi

```
Telegram ─webhook (secret_token)─► POST /tg/{tenant} ─► BotInbox ─► InboxWorker ─► UpdateRouter ─► ERP /Distribution/DstApi/*
                                                                                     │
ERP dst.NOTIFICATIONS ◄─ pull (NotificationPoller, 12 s) ─┐                          ▼
                                                          └──────────────────► BotOutbox ─► OutboxWorker (rate limit) ─► Telegram
Mini App /app/?t=..&c=.. ─ initData ─► /app/api/{tenant}/* (BFF) ─► ERP (agent nomidan)
```

- ERP'ga har so'rov imzolanadi (`tahsinERP.Licensing/Integration/DstApiSignature.cs`): tenant kaliti = HMAC(master, "dst-api:" + slug).
  Bot va ERP bir xil master kalitni biladi (bot: `Bot__MasterKey`, ERP: `TAHSINERP_BOT_KEY`). ERP tenant slug'i — `--erp-tenant`
  (bitta bazali ERP — `default`, ko'rsatilmasa shu); bot URL'idagi slug (`/tg/{slug}`) undan farq qilishi mumkin.
- Har tenant — o'z boti (BotFather tokeni bazada AES-GCM bilan shifrlangan). Bot o'chib qolsa, ERP bildirishnomalari yo'qolmaydi.
- Baza `tahsin_bot` ishga tushishda o'zi yaratiladi (`Data/Schema.sql`, idempotent).

## Sozlash

| Kalit | Qiymat |
|---|---|
| `Bot__MasterKey` | ≥ 32 belgi, ERP'dagi `TAHSINERP_BOT_KEY` bilan bir xil |
| `Bot__PublicBaseUrl` | `https://bot.example.uz` (webhook va Mini App havolalari) |
| `ConnectionStrings__Bot` | `tahsin_bot` bazasi |
| `Bot__FilesRoot` | foto va imzolar papkasi |
| `Bot__Voice__GroqApiKey` | ixtiyoriy: ovozli buyurtma (Groq Whisper + JSON ajratish). Bo'sh — bot "sozlanmagan" deydi. ERP'da ham yoqiladi: Mashinalar → Yetkazish sozlamalari |
| `Bot__Voice__ChatModel` | ixtiyoriy, standart `llama-3.3-70b-versatile`; bo'sh — faqat qoidaviy ajratgich |
| `Bot__FontsDir` | ixtiyoriy: nakladnoy PDF shrifti (bo'sh — Arial / DejaVu / Liberation; Linux'da `fonts-dejavu-core` paketi) |

## Tenant ulash

```
dotnet tahsinERP.Bot.dll tenant add firma1 https://erp.example.uz/ 123456:ABC... [--erp-tenant firma1]
dotnet tahsinERP.Bot.dll webhook set firma1
```

Keyin ERP → Distributsiya → Sozlamalar → "Telegram bot nomi" ga bot nomini yozing; Jamoalar sahifasida xodimga,
mijoz kartasida mijozga ulash kodi beriladi (`t.me/<bot>?start=<kod>`). BotFather'da Mini App uchun domen: `/setdomain`.

Selfi va fotolar ERP sahifalarida ko'rinishi uchun ERP Sozlamalarida "Bot ochiq manzili"ga `Bot__PublicBaseUrl` qiymatini yozing:
ERP brauzerga `{bot}/files/{kalit}?e=..&s=..` havolasini beradi (tenant kaliti bilan imzolangan, ~2 soat amal qiladi), bot uni
tekshirib rasmni beradi — ERP botga murojaat qilmaydi.

## Lokal sinov

`tenant add ... --offline` — tokenni tekshirmasdan qo'shadi (soxta token bilan oqimni sinash uchun). Testlar: `dotnet test tahsinERP.Bot.Tests`.
