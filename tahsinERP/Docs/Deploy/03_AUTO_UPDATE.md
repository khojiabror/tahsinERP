# Avto-yangilash: paneldan bitta tugma bilan (2026-10-22)

ZIP + RDP o'rniga: `main`ga push → CI imzolangan relizni chiqaradi → panel **Yangilanishlar** sahifasida "yangi versiya" ko'rinadi →
Owner **O'rnatish** ni bosadi → serverdagi Updater zaxira oladi, almashtiradi, tekshiradi, xato bo'lsa o'zi qaytaradi.

**ERP har firma uchun alohida IIS instance'ida** (sayt va pool `erp-<slug>`, papka `C:\inetpub\erp\<slug>`). Kod bitta repo, lekin
versiya firma bo'yicha: masalan iPack yangi relizda, qolganlar eskisida. Panel, bot, sayt va agent server bo'yicha bitta.

## 1. Arxitektura

```
GitHub Actions (windows)  ── push main ──►  GitHub Release vYYYY.M.D.N (private repo)
  Build-Release.ps1: testlar, ERP (MSBuild) + Database,         erp.zip  panel.zip  bot.zip  site.zip  agent.zip  updater.zip
  dotnet publish ×5, manifest.json (SHA-256), manifest.sig       manifest.json  manifest.sig (ECDSA P-256)
                                    │
                 GitHub token (faqat panelda, read-only)
                                    ▼
Panel admin.  ── relizlarni 10 daqiqada tekshiradi, imzoni tekshiradi (AppReleases), navbat (UpdateJobs), audit
   │  /api/agent/updates/*  (mavjud HMAC protokoli; paket panel orqali proxy qilinadi)
   ▼
Agent (NETWORK SERVICE) ── job oladi, manifest+imzo+ZIP'ni Updates\Inbox\<id>\ ga qo'yadi, Status'ni kuzatib panelga jonli log
   │  fayl almashuvi (bir server): Inbox (agent yozadi) → Status, state.json (Updater yozadi)
   ▼
Updater (LocalSystem) ── tarmoqqa chiqmaydi, sir saqlamaydi
   imzo + SHA-256 QAYTA → zaxira → pool/xizmat stop → robocopy → (ERP: Web.config birlashtirish, migratsiya+seed) → start → /health
   xato → avtomatik rollback (fayllar; migratsiya boshlangan bo'lsa baza .bak'dan)
```

## 2. Xavfsizlik va huquqlar

| Nima | Qaror |
|---|---|
| Paket haqiqiyligi | CI manifestni **ECDSA P-256** bilan imzolaydi. Private kalit faqat GitHub secret `RELEASE_SIGNING_KEY` (environment `release`, faqat `main`). Public kalit kodda: `tahsinERP.Provisioning.Contracts/Releases.cs` → `ReleaseKeys` (kalit almashtirish — yangi `keyId` qo'shish). |
| Kim tekshiradi | Panel (keshlashda), agent (yuklashdan keyin — erta to'xtatish) va **Updater (hal qiluvchi)**. Imzosiz, boshqa kalitli, hash mos kelmagan yoki manifestdagi versiya so'ralganiga teng bo'lmagan paket o'rnatilmaydi. |
| Eski versiyaga tushirish | Taqiqlanadi (eski imzoli relizni "qayta o'rnatish" hujumi). Orqaga — faqat **Rollback** (lokal zaxiradan). |
| GitHub token | Fine-grained PAT: faqat `khojiabror/tahsinERP`, **Contents: Read-only**, muddat 1 yil. Faqat panel pool env'ida: `ControlPanel__Updates__GitHubToken`. Server/agent/Updater token ko'rmaydi. |
| Updater (LocalSystem) | IIS pool/sayt va `inetpub` uchun administrator huquqi kerak — shuning uchun alohida kichik xizmat. Kirishi — faqat imzolangan paket; tarmoq yo'q; sir yo'q. O'z papkasi, `releases`, `_backup`, `state.json` — faqat SYSTEM/Administrators yozadi. SQL: `NT AUTHORITY\SYSTEM` sysadmin (zaxira/tiklash, firma pool login'i). |
| Agent (NETWORK SERVICE) | O'zgarmadi: IIS'ga tegmaydi. `Updates\Inbox` va `Status` ga yozadi, `state.json` ni o'qiydi. |
| Sirlar | Pool `environmentVariables`, agent xizmat muhiti, ERP `db.connection` — **hech biriga tegilmaydi**. ERP `Web.config`: `machineKey`, `SiteBaseDomain`, `SiteCnameTarget` eski fayldan yangisiga ko'chiriladi. Agent `appsettings.json` almashtirilmaydi. CLI chiqishidagi pool env qiymatlari va `Password=` log'da yashiriladi. |
| Updater o'zi | O'zini yangilamaydi (kichik, kam o'zgaradi): `Install-Updater.ps1` bilan qo'lda. |

## 3. Bir martalik sozlash

1. **Imzo kaliti** (ishlab chiquvchi kompyuterida, bir marta). Kalit allaqachon yaratilgan: `C:\Users\khoji\.tahsinERP-release\release-r1.private.pem`
   (public qismi `ReleaseKeys["r1"]` da). GitHub'ga yuklang va faylni xavfsiz joyga (parol menejeri) oling:
   ```
   gh secret set RELEASE_SIGNING_KEY --env release < C:\Users\khoji\.tahsinERP-release\release-r1.private.pem
   ```
   GitHub → Settings → Environments → `release`: *Deployment branches* = `main`. Yangi kalit kerak bo'lsa:
   `dotnet run --project tahsinERP.ReleaseTool -- keygen --out <papka> --key-id r2` va `ReleaseKeys` ga `r2` qo'shing.
2. **GitHub token panelga**: github.com → Settings → Developer settings → Fine-grained tokens → repo `tahsinERP`, *Contents: Read-only*.
   Serverda (Administrator PowerShell; token chatga/skriptga yozilmaydi — Notepad'da vaqtinchalik faylga):
   ```
   $t = (Get-Content C:\temp\gh.txt -Raw).Trim(); Remove-Item C:\temp\gh.txt
   & "$env:windir\System32\inetsrv\appcmd.exe" set config -section:system.applicationHost/applicationPools "/+[name='tahsinERP.ControlPanel'].environmentVariables.[name='ControlPanel__Updates__GitHubToken',value='$t']" /commit:apphost
   Restart-WebAppPool tahsinERP.ControlPanel
   ```
3. **Panel, agent va Updater'ning yangi versiyasi — oxirgi marta ZIP orqali** (avto-yangilash shu versiyadan boshlanadi):
   panel (`Install-PanelIis.ps1 -SkipPublish -KeepSecrets` yoki 02_UPDATE_PANEL_REDESIGN.md tartibi — `migrate` yangi jadvallarni yaratadi),
   agent (`Install-Agent.ps1 -Domain tahsinerp.uz -ServerId 1` — birinchi marta sirlar fayli bilan; keyin `-KeepSecrets`),
   Updater: `Install-Updater.ps1 -Domain tahsinerp.uz` (paket `app\` = `updater.zip` ichidagi fayllar).
4. **Firmalarni alohida instance'larga ajratish** (bir martalik; hozirgi umumiy `tahsinERP` sayti fallback va HTTP→HTTPS uchun qoladi):
   ```
   & 'C:\tahsinERP\Updater\tahsinERP.Updater.exe' split-erp            # barcha yoqiq firmalar
   & 'C:\tahsinERP\Updater\tahsinERP.Updater.exe' split-erp ipack      # yoki faqat bittasi
   ```
   Har firma uchun: `C:\inetpub\erp\<slug>` (umumiy saytdan nusxa, faqat shu firmaning `App_Data\DstFiles\<slug>`), pool `erp-<slug>`
   (env umumiy pool'dan nusxa), https binding `<slug>.<domen>` (umumiy saytning sertifikati), SQL login `IIS APPPOOL\erp-<slug>` → db_owner,
   Web.config'da umumiy saytning machineKey'i (kirgan foydalanuvchilar chiqib ketmaydi). Versiya — "eski (ajratilgan)".
   Muqobil: panelda firma qatoridagi **Instance yaratish** (relizdan).

## 4. Kundalik ish

1. `main`ga push (yoki Actions → release → *Run workflow*). ~15–25 daqiqada reliz chiqadi. Faqat hujjat o'zgarsa — reliz chiqmaydi.
2. Panel → **Yangilanishlar** (Owner). Relizlar 10 daqiqada o'zi tekshiriladi ("Relizlarni hozir tekshirish" — darhol).
   Har ilova uchun: o'rnatilgan / mavjud versiya, o'zgarishlar (shu ilovaga tegishli commit'lar). Kodi o'zgarmagan ilovaga yangilash taklif qilinmaydi.
3. **O'rnatish** → tasdiq → job sahifasi (log jonli, 3 s'da yangilanadi). Panelning o'zini yangilashda sahifa 30–60 s ochilmaydi — keyin natija chiqadi.
4. **Firmalar ERP'si — tavsiya etilgan tartib:** avval sinov tenanti (masalan `test`; umumiy `demo` 2026-10-26 da olib tashlandi — 05_PROVISIONING.md 6) → tekshiring → `ipack` → qolganlar. Har firma: zaxira (fayllar + `.bak`),
   pool to'xtaydi (1–3 daqiqa), migratsiya + seed shu firma bazasiga, `/health` (ilova + baza). Xato bo'lsa o'sha firma oldingi holatiga qaytadi,
   boshqa firmalarga ta'sir yo'q.
5. **Rollback** — oxirgi zaxiraga (fayllar). Firma uchun "Bazani ham zaxiradan tiklash" belgisi — zaxiradan keyin kiritilgan ma'lumotlar
   yo'qoladi; faqat yangi versiya bazani buzgan bo'lsa ishlating. **Bazani migratsiya qilish** — joriy versiyadagi migratsiyalarni qayta qo'llash
   (masalan, firma bazasi zaxiradan tiklangandan keyin).
6. **Yangi firma**: panelda yaratiladi (agent bazani `C:\tahsinERP\Database` — eng yangi o'rnatilgan ERP versiyasining skriptlari bilan yaratadi),
   so'ng **Instance yaratish** (shu versiya). Instance yaratilgunga qadar firma umumiy saytda ochiladi.

Bir serverda bir vaqtda bitta yangilash bajariladi (qolganlari navbatda). Zaxiralar: `C:\inetpub\_backup\<app|erp-slug>-<sana>`,
bazalar `C:\ProgramData\tahsinERP\Backups\update-<db>-<sana>.bak`, har ilova/firma uchun oxirgi 3 tasi.

## 5. RAM

Har ERP instance — alohida w3wp (300–600 MB). Pool'lar 20 daqiqa bo'sh tursa uxlaydi (`Erp:IdleTimeoutMinutes`, birinchi so'rov 5–15 s).
4 GB serverda taxminan 5–8 faol firma; ko'proq bo'lsa — 8 GB. Doim yoqiq kerak bo'lsa: `IdleTimeoutMinutes: 0` (barcha instance'lar uchun).

## 6. Fayllar va komponentlar

| Joy | Nima |
|---|---|
| `.github/workflows/release.yml` | CI: versiya `yyyy.M.d.<run>`, `Build-Release.ps1`, `gh release create` |
| `tools/ci/Build-Release.ps1` | Yig'ish (CI va lokal): `-Version 2026.10.22.1 -OutDir C:\temp\rel [-SkipErp] [-SkipTests]` |
| `tahsinERP.ReleaseTool` | `keygen`, `manifest`, `sign`, `verify` |
| `tahsinERP.Provisioning.Contracts/Releases.cs`, `UpdaterFiles.cs` | manifest, imzo, `ReleaseKeys`, agent↔panel DTO, agent↔Updater fayl formatlari |
| `tahsinERP.Updater` | Xizmat: `InboxWorker`, `UpdateExecutor`, `WebConfigMerger`, `IisOps.ps1`; CLI `status`, `split-erp` |
| `tahsinERP.ProvisioningAgent/UpdateRunner.cs` | Yuklash, inbox, kuzatish, inventar (`Updates:*` sozlamalari) |
| Panel `Services/UpdatesService.cs`, `ReleaseSource.cs`, `Pages/Updates`, `Api/AgentApi.cs` | Relizlar, navbat, sahifa, `/api/agent/updates/*` |
| ERP `Controllers/HealthController.cs` | `GET /health` — tenant + baza ulanishi (200/503) |
| `tahsinERP/tools/deploy/Install-Updater.ps1` | Updater o'rnatish (ASCII, PS 5.1) |

Updater sozlamalari (`C:\tahsinERP\Updater\appsettings.json`): `Domain`, `SqlServer`, `KeepBackups`, `HealthTimeoutSeconds`,
`Erp:IdleTimeoutMinutes`, ilova yo'llari (`Apps:panel:Path` va h.k. — standart qiymatlar prod tuzilishiga mos).

## 7. Muammolar

| Belgi | Sabab / yechim |
|---|---|
| Panelda "Updater o'rnatilmagan" | `Install-Updater.ps1`; xizmat holati `Get-Service tahsinERP.Updater`; agent 5 daqiqada inventar yuboradi |
| Job "Updater javob bermadi" | Updater xizmati to'xtagan — Event Viewer → Application |
| "Imzo mos kelmadi" / "Noma'lum imzo kaliti" | CI boshqa kalit bilan imzolagan yoki `ReleaseKeys` yangilanmagan |
| "GitHub token sozlanmagan" / GitHub 401/404 | 3.2-band; token muddati, repo huquqi |
| "Updater bajarish o'rtasida to'xtagan" | Server qayta yuklangan — holatni `tahsinERP.Updater.exe status` bilan tekshiring, kerak bo'lsa Rollback |
| Lease muddati o'tdi | Agent to'xtagan; Updater ishini tugatgan bo'lishi mumkin — inventar keyingi yuborilishida haqiqiy versiyani ko'rsatadi |

## 8. Hali real serverda sinalmagan

`IisOps.ps1` (sayt/sertifikat/env nusxasi), LocalSystem'dan `appcmd`/`robocopy`/`sc`, `split-erp` real firmalarda, GitHub asset proxy
(302 → S3), Actions'da ERP MSBuild. Birinchi marta: avval `demo` firmasida `split-erp demo`, keyin bitta reliz bilan demo'ni yangilang.
