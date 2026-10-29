# @tahsinERPBot — sotuv va yordam boti

Panel ilovasi ichida ishlaydi (alohida xizmat emas). Telegram → `POST /api/telegram/webhook` (secret sarlavha bilan) →
`BotQueue` (xotirada, chat bo'yicha tartibli) → `BotWorker` → `SalesBot`.

## Foydalanuvchi uchun

* `/start` → til (O'zbekcha / Ўзбекча / Русский) → salomlashish va inline menyu.
* **📘 tahsinERP nima?** — sayt matnlaridan (Pages/Public/Content): kimlar uchun, natijalar, afzalliklar, raqamlar;
  joriy etish bosqichlari, Telegram orqali savdo.
* **🧩 Modullar** — 14 modul, har biri: qisqa tavsif + qo'llanma bo'limlari (sahifalab, ◀️ ▶️, ⏮ ⏭).
* **💼 Paketlar**, **❓ FAQ** — saytdagi bilan bir xil.
* **Erkin savol** — yozilgan matn qo'llanma va FAQ bo'yicha qidiriladi (BM25, o'zbek/rus qo'shimchalari uchun 5 harfli
  ildiz); javob: eng mos parcha + bo'limlar tugmalari; topilmasa — konsultatsiya / yordam tugmalari.
* **🎁 14 kunlik demo** — ism → kompaniya → telefon (📱 tugma) → faoliyat turi. `DemoRequest` (Source = telegram)
  + savdo guruhiga xabar (StaffChatId) + email (Leads:NotifyEmail). Panel «Demo so'rovlar»da «Telegram bot» belgisi.
* **👨‍💼 Sotib olish / konsultatsiya** — ism → kompaniya → telefon → savol (ixtiyoriy). `DemoRequest.Kind = Consultation`.
* **🛠 Texnik yordam** — firma → muammo (matn, skrinshot, fayl, ovoz) → support guruhiga murojaat №N; qo'shimcha
  xabarlar o'sha murojaatga qo'shiladi.
* **🌐 tahsinerp.uz** — URL tugma (ru tilida /ru). Buyruqlar: /menu /demo /consult /support /modules /lang /help /id.
* Deep link: `t.me/tahsinERPBot?start=demo` (consult, support, modules, plans). Boshqa parametr — portal bog'lash kodi.
* Cheklovlar: kuniga 3 ta lead, 10 ta murojaat; daqiqasiga 20 xabar (guruhga spam bo'lmasin).

## Xodimlar uchun (guruhlar)

* Bot xabariga (lead, murojaat yoki mijozning keyingi xabari) **reply** — javob mijozga bot nomidan yetadi
  (matn, rasm, fayl). Yetkazilganda 👍 reaksiya. Lead'ga birinchi javob — so'rov «Bog'lanildi» bo'ladi.
* Mijoz «✍️ Javob yozish» bosib yoki xodim xabariga reply qilib yozsa — guruhda sarlavhaga reply bo'lib keladi.
* `/close` (reply bilan) — murojaatni yopadi; support murojaatida mijozdan baho (⭐1–5) so'raladi. Mijoz ham
  «✅ Muammo hal bo'ldi» bilan yopishi mumkin.
* `/id` — guruh chat ID'si. Guruh supergroup'ga o'tsa support guruhi ID'si avtomatik yangilanadi
  (savdo guruhi env'da — logda ogohlantirish chiqadi).

## Sozlash (prod)

1. Panelni yangilash (EF migratsiya `SalesBot`).
2. Panel → **Telegram bot** → «Webhook'ni o'rnatish / yangilash» (Owner). Secret panel o'zi yaratadi va shifrlab
   saqlaydi (env `ControlPanel__Telegram__WebhookSecret` berilsa — u ustun). Buyruqlar menyusi va bot tavsifi
   (uz/ru) ham shu tugma bilan o'rnatiladi. Webhook o'rnatilgach bu token bilan `getUpdates` boshqa joyda ishlamaydi.
3. Botni «tahsinERP: Bugs» guruhiga qo'shing → sahifadagi ro'yxatdan tanlab «Saqlash» (guruhga tasdiq xabari keladi).
4. Savdo guruhi — avvalgidek `ControlPanel__Telegram__StaffChatId` (demo so'rovlari guruhi).

## Qo'llanma bilim bazasi

`Bot/Content/kb.{uz,ru}.json` — `tahsinERP/ManualData/{uz,ru}/*.html` dan quriladi:

```
python tools/manual/build_bot_kb.py
```

ManualData yangilansa (convert_docx.py) shu skriptni ham ishga tushirib, natijani commit qiling. Kirill (oz) —
lotin matnidan avtomatik transliteratsiya. Interfeys matnlari: `Bot/Content/bot.{uz,ru}.json` (kalitlar ikkala tilda
bir xil bo'lishi testda tekshiriladi).
