# SF va TTN: elektron hujjat (EDO) integratsiyasi

## Hozirgi holat
- Счёт-фактура (SF) va TTN invoysdan yaratiladi va **faqat tahsinERP ichida** saqlanadi.
- Holat `IntegrationStatusCode = LOCAL` bo'ladi. Details sahifasidagi "Elektron hujjat (EDO)" panelida bu holat "Ichki (tizimda)" deb ko'rinadi.
- Tashqi tizimga hech narsa yuborilmaydi. Buning uchun sozlama kerak emas.

## Tuzilishi (`Services/Crm/EDocuments/`)
| Fayl | Vazifasi |
|---|---|
| `EDocumentModels.cs` | Provayderdan mustaqil hujjat modellari: `EInvoiceDocument` (SF) va `EWaybillDocument` (TTN). Ularning maydonlari e-faktura (roaming) formatiga yaqin: INN, MXIK, QQS, summa. Shu faylda holat kodlari (`EDocumentStatus`) ham bor. |
| `EDocumentProviders.cs` | `IEDocumentProvider` interfeysi va uchta provayder: `InternalEDocumentProvider` (standart), `DidoxEDocumentProvider`, `FakturaUzEDocumentProvider`. Ikkala tashqi provayder umumiy HTTP/JSON asosni (`HttpEDocumentProvider`) ishlatadi. |
| `EDocumentService.cs` | SF/TTN'dan hujjat modelini quradi, uni tekshiradi, yuboradi, holatni yangilaydi va Details paneli uchun ma'lumot tayyorlaydi. |

Holat SF/TTN jadvallarining mavjud ustunlarida saqlanadi, shuning uchun sxema o'zgarishi kerak emas:

| Ustun | Ma'nosi |
|---|---|
| `ExternalSystemCode` | Provayder kodi |
| `ExternalSFKey` / `ExternalDocKey` | Provayderdagi hujjat ID'si |
| `IntegrationStatusCode` | `LOCAL` / `SENT` / `ACCEPTED` / `REJECTED` / `ERROR` |
| `LastSyncDT` | Oxirgi sinxronlash vaqti |
| `TaxMessageID` | Provayder qaytargan oxirgi xabar |
| `XmlPayload` | Yuborilgan hujjat nusxasi (XML) |
| `XmlSchemaVersion` | Hujjat formati versiyasi (`tahsin-edoc/1`) |

## Amallar
Amallar SalesSF va SalesTTNK controllerlarida joylashgan:

| Amal | Nima qiladi | Kerakli ruxsat |
|---|---|---|
| `GET /SalesSF/EDocumentPayload/{id}` (TTN uchun `/SalesTTNK/...`) | Yuboriladigan ma'lumotni va kamchiliklar ro'yxatini JSON ko'rinishida beradi. Integratsiyani sozlashda foydalaniladi. | Ko'rish |
| `POST .../SendEDocument/{id}` | Hujjatni faol provayderga yuboradi. Ichki rejimda tugma ko'rinmaydi. | O'zgartirish |
| `POST .../RefreshEDocument/{id}` | Provayderdan hujjat holatini so'raydi. | O'zgartirish |

## Didox yoki Faktura.uz'ni ulash
1. Provayder bilan shartnoma tuzing. Ulardan API hujjatlarini, test muhitini va kalitni (token) oling.
2. `web.config` → `<appSettings>` bo'limiga quyidagi kalitlarni qo'shing. Misol Didox uchun; Faktura.uz uchun bo'lim nomi `FakturaUz`, provayder kodi `FAKTURA_UZ`:
   ```xml
   <add key="EDoc:Provider" value="DIDOX" />            <!-- INTERNAL (standart) | DIDOX | FAKTURA_UZ -->
   <add key="EDoc:SellerTin" value="123456789" />        <!-- bo'lmasa COMPANIES.DUNS -->
   <add key="EDoc:TimeoutSeconds" value="30" />
   <add key="EDoc:Didox:BaseUrl" value="https://..." />
   <add key="EDoc:Didox:AuthHeader" value="Authorization" />  <!-- provayder talab qilgan sarlavha nomi -->
   <add key="EDoc:Didox:Token" value="..." />
   <add key="EDoc:Didox:InvoicePath" value="/..." />     <!-- SF yaratish endpoint'i -->
   <add key="EDoc:Didox:WaybillPath" value="/..." />     <!-- TTN yaratish endpoint'i -->
   <add key="EDoc:Didox:StatusPath" value="/.../{id}" /> <!-- {id} = tashqi ID, {kind} = invoice|waybill -->
   ```
   Token sir saqlanadi: uni repoga commit qilmang, serverdagi `web.config`'ga yoki muhit sozlamasiga yozing.
3. Provayder JSON formati umumiy modeldan farq qilsa, quyidagi metodlarni qayta yozing (override):
   - `MapInvoice` / `MapWaybill` — umumiy modelni provayder JSON'iga o'girish;
   - `ParseResponse` — javobdan hujjat ID'si, holati va xabarini o'qish;
   - `MapStatus` — provayder holat kodlarini tizim holatlariga moslash.

   Bu metodlar `DidoxEDocumentProvider` va `FakturaUzEDocumentProvider` klasslarida yoziladi. Qolgan qismlar o'zgarmaydi: holatni saqlash, Details paneli va ruxsatlar.
4. Ulanishni test muhitida bitta SF bilan tekshiring. Avval "E-hujjat ma'lumotlari (JSON)" tugmasi orqali yuboriladigan ma'lumotni ko'ring.

Imzolash (ERI/ЭЦП) odatda provayder tomonida bajariladi. Agar provayder imzoni ERP'dan talab qilsa, bu alohida bosqich bo'ladi.

## Yuborishdan oldin to'ldirilishi kerak
Panel kamchiliklarni ko'rsatadi. Ular bartaraf etilmaguncha "Yuborish" tugmasi chiqmaydi:
- sotuvchi va xaridor INN'i (9 raqam);
- **MXIK (IKPU) kodi** — soliq e-fakturasi uchun majburiy. Hozir `PRODUCTS` jadvalida bunday ustun yo'q. Uni qo'shish alohida qaror talab qiladi (sxema o'zgarishi) va mahsulot kartasiga maydon qo'shiladi.
- TTN uchun: avtomobil raqami va haydovchi.
