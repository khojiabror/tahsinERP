# Server, domen va Telegram bot — ishga tushirish rejasi

Holat: 2026-10-21. Kod tomoni tayyor (distributsiya 1–4-bosqichlar, sayt-katalog, Control Panel 0–4). Bu reja — birinchi ishlab turgan
muhit (pilot va birinchi SaaS mijozlar). Bog'liq: `Docs/ControlPanel/00_PLAN.md` (9-bo'lim, deploy), `tahsinERP.Bot/README.md`,
`tahsinERP.Site/README.md`, `tahsinERP.ControlPanel/deploy/Install-PanelIis.ps1`.

**Bosqichma-bosqich buyruqlar — `01_DEPLOY_GUIDE.md`** (ERP, panel, agent, bot, sayt, DNS/SSL, zaxira bitta qo'llanmada).

## 1. Avval hal qilinadigan uchta savol

| # | Savol | Tavsiya |
|---|---|---|
| 1 | **Domen nomi** | **Hal qilindi: `tahsinerp.uz` sotib olindi (2026-10-21).** |
| 2 | **Server qayerda** | **Muhim:** O'zbekiston qonunchiligi bo'yicha (Shaxsga doir ma'lumotlar to'g'risidagi qonun, 27¹-modda) O'zbekiston fuqarolarining shaxsiy ma'lumotlari (mijoz, agent ism va telefonlari) O'zbekiston hududidagi serverlarda saqlanishi kerak. Pilot va real mijozlar uchun **O'zbekistondagi VPS/data-markaz** (Windows Server ijarasi) tavsiya qilinadi. AWS (Frankfurt) — demo va sinov uchun arzon va qulay. Yakuniy qarorni yurist bilan tasdiqlang. |
| 3 | **Nechta server** | Boshlanishiga **bitta Windows server** (pastda). Keyinroq ERP .NET Core'ga o'tganda — Linux. |

## 2. Server (variant A — tavsiya, bitta Windows server)

ERP .NET Framework 4.8 da — faqat Windows/IIS. Qolgan servislar (.NET 10) Windows'da ham ishlaydi, shuning uchun hammasi bitta serverda:

| Komponent | Texnologiya | Manzil (misol: `tahsinerp.uz`) |
|---|---|---|
| ERP (SaaS, ko'p tenant) | IIS, .NET 4.8 | `{firma}.tahsinerp.uz` (wildcard) |
| SQL Server Express | tenant bazalari, `tahsin_control`, `tahsin_bot`, `tahsin_site` | faqat lokal |
| Boshqaruv paneli + mijoz portali | IIS, ASP.NET Core 10 | `admin.tahsinerp.uz`, `my.tahsinerp.uz` |
| Telegram bot + Mini App | IIS, ASP.NET Core 10 | `bot.tahsinerp.uz` |
| Sayt-katalog | IIS, ASP.NET Core 10 | `{firma}.sayt.tahsinerp.uz` (wildcard) |
| Provisioning agenti | Windows Service | — |

**O'lcham:** 4 vCPU, 8 GB RAM, 150+ GB SSD, Windows Server 2022, statik IP. 2 vCPU / 8 GB ham boshlanishga yetadi.
(Narx: AWS Lightsail Windows 8 GB — oyiga taxminan $70–80; O'zbekistondagi Windows VPS — provayderga qarab. Litsenziya narxga kirganini tekshiring.)
SQL Server Express cheklovlari: bitta baza ≤ 10 GB, bufer 1.4 GB — taxminan 15–20 faol tenantgacha yetadi (keyin Standard yoki ikkinchi server).

**Variant B (keyin):** bot va saytni alohida kichik Linux serverga (1–2 GB) Caddy bilan ko'chirish. Repoda tayyor fayllar bor: `tahsinERP.Bot/deploy/*`, `tahsinERP.Site/deploy/*`.
Kompaniyalarning **o'z domeni** (masalan `firma1.uz`) uchun avtomatik sertifikat — Caddy on-demand TLS bilan (`tahsinERP.Site/deploy/Caddyfile`). Variant A da
boshlanishiga faqat `{firma}.sayt.tahsinerp.uz` subdomenlari; o'z domeni kerak bo'lganda — Caddy yoki har domen uchun win-acme.

## 3. DNS

DNS'ni **Cloudflare'ga** (bepul) o'tkazish tavsiya qilinadi: wildcard sertifikatni avtomatik yangilash (DNS-01) uchun API kerak.

| Yozuv | Tur | Qiymat |
|---|---|---|
| `tahsinerp.uz`, `*.tahsinerp.uz` | A | server IP |
| `*.sayt.tahsinerp.uz` | A | server IP |
| `sites.tahsinerp.uz` | A | server IP (kompaniya domeni shu nomga CNAME qiladi — ERP `SiteCnameTarget`) |

`admin`, `my`, `bot`, `sayt`, `sites` va boshqa xizmat nomlari tenant nomi bo'la olmaydi (`ReservedSlugs`, ERP va panelda bir xil ro'yxat).

## 4. Telegram bot

Har kompaniyaga o'z boti. Birinchi (pilot kompaniya yoki demo) bot:
1. Telegram'da **@BotFather** → `/newbot` → nom va `..._bot` bilan tugaydigan username → **token** keladi. Tokenni hech kimga yubormang, chatga yozmang.
2. `/setdescription`, `/setuserpic` — ixtiyoriy.
3. Serverda (token faqat serverda kiritiladi, bazada shifrlanib saqlanadi):
   `dotnet tahsinERP.Bot.dll tenant add firma1 https://firma1.tahsinerp.uz/ <TOKEN> --erp-tenant firma1` va `dotnet tahsinERP.Bot.dll webhook set firma1`.
4. ERP → Distributsiya → Sozlamalar: "Telegram bot nomi" va "Bot ochiq manzili" (`https://bot.tahsinerp.uz`).
5. Ovozli buyurtma kerak bo'lsa — Groq API kaliti (`Bot__Voice__GroqApiKey`); ixtiyoriy.

## 5. O'rnatish tartibi

| # | Qadam | Kim |
|---|---|---|
| 1 | Domen sotib olish, DNS'ni Cloudflare'ga ulash, Cloudflare API token (faqat DNS:Edit) | Siz |
| 2 | Server ijarasi (Windows Server 2022), RDP faqat sizning IP'ingizdan, firewall — 80/443 | Siz |
| 3 | Serverga o'rnatish: IIS (+ URL Rewrite, WebSockets), .NET 4.8, ASP.NET Core 10 Hosting Bundle, SQL Server Express, win-acme | Skript (men tayyorlayman, siz serverda ishga tushirasiz) |
| 4 | Wildcard sertifikatlar: `*.tahsinerp.uz`, `*.sayt.tahsinerp.uz` (win-acme + Cloudflare DNS-01, avtomatik yangilanadi) | Skript |
| 5 | Bazalar: `New-TenantDb.ps1` (tenant), `Invoke-DbMigrations.ps1`; panel/bot/sayt bazalari o'zi yaratiladi | Skript |
| 6 | Sirlar (bir marta yaratiladi, repoga tushmaydi): yangi `machineKey` (repodagi Web.config'dagi qiymat **ishlab chiqarishda ishlatilmaydi**), bot/sayt master kaliti (`TAHSINERP_BOT_KEY` = `Bot__MasterKey` = `Site__MasterKey`), SQL parollari, panel imzo kaliti | Skript yashirin so'rov bilan; qiymatlarni siz kiritasiz |
| 7 | ERP, panel, bot, sayt publish va IIS saytlari | Skript (`Install-PanelIis.ps1` kabi) |
| 8 | Panelda birinchi Owner, paketlar narxi, birinchi tenant (provisioning) | Siz (panelda) |
| 9 | Litsenziya kaliti — panel orqali (imzo kaliti faqat sizda) | Siz |
| 10 | Telegram bot (4-bo'lim) | Siz + skript |
| 11 | Zaxira: SQL bazalar har kuni (siqilgan .bak) + server snapshot, 14 kun saqlash; serverdan tashqariga nusxa | Skript + siz (saqlash joyi) |
| 12 | Kuzatuv: `/health` (bot, sayt, panel) — tashqi monitoring (masalan UptimeRobot, bepul) | Siz |

Ishga tushgandan keyin: ERP sahifalari brauzerda, bot va Mini App Telegram ichida, sayt real domenda — hozirgacha tekshirilmagan qismlar shu muhitda sinaladi.

## 6. Sozlanadigan tashqi xizmatlar (ixtiyoriy, keyin ham bo'ladi)

| Xizmat | Nima uchun | Kalitlar |
|---|---|---|
| Eskiz.uz | portalga SMS kod | `ControlPanel:Eskiz:*` (shablonlar Eskiz'da tasdiqlanadi) |
| SMTP (pochta) | portalga email kod, parol tiklash | `ControlPanel:Smtp:*` |
| Didox | panel hisob-fakturalari e-faktura | `ControlPanel:Didox:*` — avval stage muhitida |
| Groq | ovozli buyurtma | `Bot__Voice__GroqApiKey` |
