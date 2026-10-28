# ERP "Qo'llanma" sahifasini Word bilan 1:1 tekshirish (modul sessiyalari uchun)

ERP'dagi **Qo'llanma** sahifasi (`/UserManual`) qo'lda yozilgan HTML emas — u
`tahsinERP/Docs/Qollanma/modullar/<NN>-<slug>/<NN>-<slug>_{uz,ru}.docx` fayllardan **konvertor** bilan avtomatik yaratiladi.
Modul sessiyasining vazifasi: o'z modulining sahifasini Word bilan solishtirish va nomuvofiqlikni tuzatish (yoki xabar berish).

## 1. Qayerda nima turadi

| Nima | Yo'l |
|---|---|
| Konvertor | `tools/manual/convert_docx.py` (repo ildizida) |
| Natija (commit qilinadi) | `tahsinERP/ManualData/<uz\|ru>/<NN>-<slug>.html`, `ManualData/img/<xesh>.webp`, `ManualData/manifest.json` |
| Sahifa kodi | `Controllers/AdminSetting/UserManualController.cs`, `Services/UserManualProvider.cs`, `Views/UserManual/Index.cshtml` |
| Manba | `tahsinERP/Docs/Qollanma/modullar` (asosiy checkout'da, git'da emas) |

Til UI tilidan olinadi: **uz → uz**, **ru → ru**, **oz (kirill) → uz**. Tilni yuqori paneldagi globus belgisidan almashtiring
(yoki `/Language/Set?lang=ru&returnUrl=%2FUserManual%3Fm%3D<NN-slug>`).

`ManualData` papkasi to'g'ridan-to'g'ri ochilmaydi (Web.config `hiddenSegments`): HTML ham, rasmlar ham faqat ruxsat
tekshiruvidan keyin `UserManualController` orqali beriladi (`/UserManual?m=...`, `/UserManual/Img/<xesh>`).

## 2. Konvertorni ishga tushirish

Repo ildizidan, Python 3 + `lxml` + `Pillow` bilan:

```
python tools/manual/convert_docx.py --src "C:/Work/SCHAIN/tahsinERP/tahsinERP/tahsinERP/Docs/Qollanma/modullar" --only 08
```

- `--only NN` — faqat bitta modul (bir necha marta berish mumkin), `--lang uz|ru` — faqat bitta til; hech biri berilmasa hammasi
  (28 fayl, birinchi marta ~3 daqiqa, keyin bir necha soniya).
- Idempotent: docx o'zgarsa qayta ishga tushirish yetadi. Tayyor rasmlar qayta kodlanmaydi (nom = kontent xeshi),
  hech qaysi HTML ishlatmaydigan rasmlar o'chiriladi, `manifest.json` (modul nomlari) qayta yoziladi.
- Har fayl uchun "N sarlavha, N rasm, N KB" chiqadi; xato bo'lsa `!` bilan va chiqish kodi 1.
- Kontent titul va Word mundarijasidan keyin, **birinchi Heading 1 dan** boshlanadi. Modul nomi — titul sahifasidagi "NN. ..."
  yozuvi, bo'lmasa eng katta shriftli yozuv ("tahsinERP" logotipidan tashqari), oldiga `NN.` qo'yiladi.
- Modul sessiyalari `ManualData`ni **commit qilmaydi** (docx tuzatishi untracked Docs papkasida qoladi). Oxirida konvertor
  sessiyasi hamma modulni bir marta qayta yaratib, bitta commit qiladi — bir nechta sessiya bir faylda to'qnashmaydi.

## 3. Sahifani ochish

Lokal ERP (DB_QOLLANMA bazasi) — **port 51020**, worktree `.claude/worktrees/manual-docx-page-bc2e93`:
`.claude/run-manual-page.cmd` (yoki o'sha worktree'dagi `.claude/launch.json` → `erp-manual-page`).
Login: asosiy checkout'dagi `.claude/qollanma-01-login.txt` (`qollanma.admin`).

URL: `http://localhost:51020/UserManual?m=<NN>-<slug>` (masalan `?m=08-moliya`).
Sahifa HTML fayldan o'qiladi va fayl vaqti o'zgarsa yangisi olinadi — konvertordan keyin brauzerda F5 kifoya.
Diqqat: 51020 dagi server `manual-docx-page-bc2e93` worktree'idagi `ManualData`ni ko'rsatadi — konvertorni o'sha worktree
ildizidan ishga tushiring (yoki `--out <worktree>/tahsinERP/ManualData` bering).

## 4. Word bilan solishtirish (Word oynasi ochilmasin!)

Word'ni foydalanuvchiga ko'rsatmang. Taqqoslash uchun docx'ni **ko'rinmas** Word bilan PDF'ga chiqaring
(faylni `%TEMP%` ga nusxalab, `Visible=$false`, `DisplayAlerts=0`, `ExportAsFixedFormat(pdf, 17)`, oxirida
`NormalTemplate.Saved=$true` va `Quit([ref]0)`), so'ng kerakli PDF sahifasini rasmga aylantirib
(`pymupdf.open(pdf)[i].get_pixmap(dpi=80).save(...)`) sahifadagi o'sha bo'lim bilan yonma-yon qarang.
Hamma sahifani emas — har turdagi elementdan 1–2 namunani ko'rish yetadi.

Matn yo'qolmaganini skript bilan tekshirish mumkin: docx'dagi `w:t` matni (birinchi H1 dan) va HTML matni (teglarsiz,
bo'shliqlarsiz) uzunligi deyarli teng bo'lishi kerak (HTML'da faqat ro'yxat raqamlari ortiqcha).

Sahifa 100% masshtabdagi Word kabi: o'lchamlar pt da; ustun tor bo'lsa sahifa Word'dagi "sahifa kengligiga moslash" kabi
kichrayadi — **satr bo'linishi ham Word bilan bir xil bo'lishi kerak**. Telefonda matn qayta oqadi — bu normal.

### Checklist (har modul, uz va ru)

- [ ] Chap panelda modul nomi to'g'ri ("NN. ..."), mundarija H1/H2 bo'yicha to'liq, qidiruv ishlaydi.
- [ ] Sarlavhalar: matn, raqam, rang, o'lcham Word bilan bir xil.
- [ ] Paragraflar: qalin/kursiv/rang/tagiga chizilgan bo'laklar, oraliqlar, tekislash (markaz, ikki tomonlama).
- [ ] Raqamlangan ro'yxatlar: raqamlar ketma-ketligi (har ro'yxat 1 dan qayta boshlanishi yoki davom etishi Word'dagidek),
      raqam rangi/qalinligi, chekinish; belgili ro'yxatlar: belgi va chekinish.
- [ ] Jadvallar: ustunlar kengligi nisbati, sarlavha qatori (fon, oq qalin matn), katak fon ranglari (yo'l-yo'l qatorlar),
      chegaralar, katak ichidagi ro'yxat/qalin matn, vertikal tekislash.
- [ ] Eslatma bloklari (bir katakli rangli jadvallar): fon rangi, chapdagi qalin chiziq rangi, "Eslatma/Diqqat/Maslahat" so'zi.
- [ ] Formula/kod bloklari: shrift (Cambria Math / Consolas), fon, markazlash, ostidagi kursiv izoh.
- [ ] Rasmlar: hammasi bor, o'lchami Word'dagidek, kesilgan (crop) bo'lsa Word'dagidek kesilgan, matni o'qiladi;
      bosganda kattalashadi (lightbox) va pastida "Rasm N. ..." izohi chiqadi.
- [ ] Rasm va jadval izohlari ("Rasm N." / "Рис. N.", "Jadval N." / "Таблица N.") joyida va uslubi to'g'ri.
- [ ] Ichki havolalar (Word'da havola bo'lsa) o'sha bo'limga olib boradi; tashqi havolalar yangi oynada ochiladi.
- [ ] Tungi rejim (yuqoridagi oy/quyosh belgisi): matn o'qiladi, och fonlar to'q rangga o'tadi, rasmlar o'zgarmaydi.
- [ ] Chop etish ("Chop etish" tugmasi → oldindan ko'rish): faqat qo'llanma chiqadi, har H1 yangi sahifadan, ranglar Word'dagidek.
- [ ] ru versiyasi ham xuddi shu tekshiruvlardan o'tadi.

## 5. Nomuvofiqlik topilsa

1. **Sababini aniqlang.** docx XML'iga qarang (`word/document.xml`): Word'da ko'rinish xuddi shunday yozilganmi?
   - Word'da ham noto'g'ri / docx'da xato (matn, raqamlash, noto'g'ri uslub, probel bilan tekislash, rasm yo'q) →
     **muammo docx'da**: o'z modulingiz build skriptini (yoki docx'ni) tuzating, docx'ni qayta yarating, konvertorni
     `--only NN` bilan qayta ishga tushirib, sahifada tekshiring.
   - Word'da to'g'ri, sahifada noto'g'ri → **muammo konvertorda yoki sahifa CSS'ida**: o'zingiz tuzatmang (hamma modulga ta'sir qiladi).
2. **Konvertor muammosi haqida xabar** — konvertor sessiyasiga ("Qo'llanma sahifasi" / `manual-docx-page`, "Linked sessions"
   ro'yxatidagi id bilan) bitta `SendMessage`, topilmasa bosh sessiyaga. Xabarda: modul va til, bo'lim raqami, Word'da qanday,
   sahifada qanday (1 qator). Katta tafsilotni (XML parchasi va h.k.) umumiy eslatmalar papkasiga
   `NN-<slug>_sahifa-konvertor.md` deb yozing va xabarda faqat fayl nomini bering.
3. Tekshiruv natijasini umumiy eslatmalar papkasiga `NN-<slug>_SAHIFA.md` deb yozing: nima tekshirildi (uz/ru),
   docx'da nima tuzatildi, konvertorga qanday xabar yuborildi.

## 6. Cheklovlar

- Word'dagi sahifa raqamlari, kolontitullar, titul sahifasi va Word mundarijasi sahifaga chiqmaydi (o'rniga chap paneldagi mundarija).
- Har modulning .docx faylini yuklab olish **qo'shilmagan**: 28 docx jami ~75 MB — reliz ZIP'i va har firma instansiyasi shuncha
  og'irlashardi. O'rniga "Chop etish" → "PDF sifatida saqlash".
- Rasmlar WebP (q80, eni ≤1400px): 918 rasm, `ManualData` jami ~33,5 MB.
