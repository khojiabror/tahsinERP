# Boshqaruv paneli — 2-bosqich (Provisioning)

Holat: 2026-10-19. Bog'liq: `00_PLAN.md` (7), `02_MULTI_TENANT.md` (tenants.json), `03_DB_PROVISIONING.md` (skriptlar kontrakti),
`04_PANEL_MVP.md` (panel). Kod: `tahsinERP.ControlPanel` (navbat, UI, `/api/agent`), `tahsinERP.ProvisioningAgent` (ERP serveridagi agent),
`tahsinERP.Provisioning.Contracts` (ikkala tomon uchun umumiy DTO + HMAC), `tahsinERP/Database/Demo` (demo seed).

## 1. Umumiy oqim

```
 Xodim (panel)                          Panel (tahsin_control)                 Agent (ERP serveri, lokal SQL)
 ─ «Sinov ochish» / «Bazani yaratish» → ProvisioningJobs: Pending        ←──── POST /api/agent/lease   (HMAC, har 15 s)
                                       (reg_token + vaqtinchalik parol         New-TenantDb.ps1 → LICENSE_STATE.RegToken
                                        shifrlangan holda job'da)              → tenants.json (temp + rename) → tekshiruv
                                       Succeeded: tenant provisioned    ←──── POST /api/agent/jobs/{id}/complete
                                       + Telegram xush kelibsiz xabari
```

- Panel SQL serverga **ulanmaydi**. Agent panelga o'zi keladi (pull), HTTPS + server siri bilan HMAC.
- Bir tenantda bir vaqtda bitta faol job. Xuddi shu turdagi takroriy so'rov mavjud job'ni qaytaradi (idempotent); boshqa turdagisi rad etiladi.
- Har amal `AuditLog` ga yoziladi: `provisioning.enqueue / succeeded / failed / retry / requeue / cancel`, `server.agent-secret`, `tenant.server`.

## 2. Job turlari

| Tur | Kim | Shart | Agent nima qiladi | Muvaffaqiyatda panel |
|---|---|---|---|---|
| Create | Sales/Owner | SaaS, `DbName`, baza hali yaratilmagan | baza bor-yo'qligini tekshiradi → `New-TenantDb.ps1` (parol env orqali; payload `Modules` bo'lsa `-Modules` — faqat shu modullar sozlamasi, 11-bo'lim; `SampleData` bo'lsa `-Demo` — Database/Demo namuna ma'lumotlari (faqat tanlangan modullarniki), so'ng demo skripti yaratgan `demo/demo2026` foydalanuvchisi o'chirib qo'yiladi va paroli tasodifiy qilinadi) → `LICENSE_STATE.RegToken` → `tenants.json` upsert → bazaga ulanib tekshiradi | `ProvisionedAtUtc`, `RegistryEnabled`; Telegram'ga URL/login/parol |
| Suspend | Sales/Owner yoki avtomatik | baza yaratilgan | `enabled: false` | `RegistryEnabled=false` |
| Resume | Sales/Owner yoki avtomatik | faol obuna bor | `enabled: true` | `RegistryEnabled=true` |
| Archive | **Owner**, sabab majburiy | baza yaratilgan | `enabled:false` → `BACKUP ... COPY_ONLY, CHECKSUM` + `RESTORE VERIFYONLY` → `IBackupStore` (hozir lokal papka) → `DROP DATABASE` | `Status=Archived`, `ArchiveLocation` |
| Delete | **Owner**, ikki bosqichli tasdiq (slug yozish + yakuniy sahifa) | faqat `Archived` | baza (qolgan bo'lsa) va arxivlarni o'chiradi, `tenants.json` dan yozuvni olib tashlaydi | `DeletedAtUtc` (hisob-fakturalar/to'lovlar/audit qoladi) |
| EndTrial | Sales/Owner, sabab + slug qayta yoziladi; faqat pullik obunasi bo'lmagan sinov tenanti (Trial yoki Suspended) | `tenants.json` dan chiqaradi → (standart: `.bak` arxiv, `IBackupStore`) → `DROP DATABASE` | darhol: TRIAL obunasi Cancelled, tenant Suspended; job tugagach Archived + `TrialEndedAtUtc`; arxivsiz bo'lsa `DeletedAtUtc` ham. Baza yaratilmagan bo'lsa job yo'q — shu zahoti Archived. Pullik tenant — Archive → Delete. |
| Reset | demo tenant (`Provisioning:DemoSlug`); agent har kecha o'zi rejalashtiradi | demo, baza yaratilgan | `DB_DEMO_TEMPLATE.bak` dan RESTORE → `Invoke-DbMigrations.ps1` → demo foydalanuvchi → yangi RegToken → `enabled:true` | `InstallationId` tozalanadi (ERP qayta register qiladi) |

**Avtomatik Suspend/Resume.** Agent har lease so'rovida panel `ReconcileAsync` qiladi: tenant holati (Trial/Active/Grace → `enabled:true`,
Suspended → `false`) bilan oxirgi qo'llangan `RegistryEnabled` farq qilsa, tizim nomidan Suspend/Resume job qo'shiladi. Shuning uchun kunlik
hayot sikli (Trial tugadi → Suspended), to'lov (→ Active) yoki holatni qo'lda o'zgartirish qayerda bo'lmasin, ERP reestri yaqinlashadi.
Bazasi yaratilgan SaaS tenantni holat ro'yxatidan «Arxiv» qilib bo'lmaydi — faqat Archive amali orqali.

**Holatlar va qayta urinish.** `Pending → Running (lease 30 daq, agent uzun amalda /extend qiladi) → Succeeded | Failed | Cancelled`.
Agent natijasi `Retry` bo'lsa (SQL'ga ulanib bo'lmadi — kod 3, applock — 6, admin xatosi — 8, fayl band) — `1, 2, 4, 8…` daqiqa pauza bilan
qayta navbatga, ko'pi bilan `MaxAttempts` (5). Agent yiqilsa lease muddati o'tgach job qayta beriladi (eski lease tokeni bilan natija 409).
`Failed` job'ni xodim «Qayta urinish» bilan qaytaradi (Create'da o'sha parol va reg_token saqlanadi), Pending/Failed — «Bekor qilish».
Running job bekor qilinmaydi (yarim holat qolmasin).

**Idempotentlik (agent).** Create: baza allaqachon bor va u shu oqim yaratgan tahsinERP bazasi bo'lsa (seed + admin) — davom etadi,
admin parolini panel ko'rsatgan parolga tenglaydi (faqat hali almashtirilmagan bo'lsa); begona baza bo'lsa — to'xtaydi.
`tenants.json` o'zgarish bo'lmasa yozilmaydi. Archive: baza yo'q, arxiv bor — oldingi urinish bajarilgan deb hisoblanadi. Delete: yo'q narsa — o'tkazib yuboriladi.

**Maxfiy qiymatlar.** reg_token va vaqtinchalik parol job'da `SecretProtector` (AES-GCM, master key) bilan shifrlangan; Succeeded/Cancelled'da
o'chiriladi. Agent ularni buyruq satriga emas, env'ga (`TAHSINERP_ADMIN_PASSWORD`) beradi; log va natijadan olib tashlanadi
(`tempPassword` maydoni ham). `Notifications` jadvaliga parolsiz matn yoziladi. Parol xodimga **bir marta** — job qo'shilgan sahifada ko'rsatiladi.

## 3. Agent API (`/api/agent`, kontrakt: `tahsinERP.Provisioning.Contracts/AgentProtocol.cs`)

Sarlavhalar: `X-Agent-Server` (Servers.Id), `X-Agent-Timestamp` (unix s, ±300), `X-Agent-Nonce` (32 hex, 10 daq ichida bir marta —
`ApiNonces`, kalit `agent:<id>`), `X-Agent-Version`, `X-Agent-Signature` = Base64(HMAC-SHA256(secret, `METHOD\npath\ntimestamp\nnonce\nsha256hex(body)`)).

| So'rov | Javob |
|---|---|
| `POST /lease` | 200 `JobLease` (payload + secrets) / 204 navbat bo'sh. Faqat shu serverga biriktirilgan tenantlar. |
| `POST /jobs/{id}/extend` `{leaseToken}` | 200 / 409 lease yo'qolgan |
| `POST /jobs/{id}/complete` `{leaseToken, outcome: Succeeded/Failed/Retry, log, result, error, exitCode, archiveLocation, registryEnabled}` | 200 / 409 / 404 |
| `POST /schedule` `{type: Reset, slug, key}` | 200 `{jobId, created}` / 422 (demo emas, boshqa serverda) |
| xato imzo / soat farqi / replay | 401 `bad_signature` / `clock_skew` / `replay` |

Agent siri: panel → Serverlar → server → «Agent kalitini chiqarish» (faqat Owner, bir marta ko'rsatiladi, bazada shifrlangan; almashtirilsa eskisi darhol bekor).

## 4. Agentni o'rnatish (ERP serveri)

Talab: .NET 10 Runtime (yoki self-contained publish), Windows PowerShell 5.1, lokal SQL Server. Agent xizmat hisobiga SQL'da
`dbcreator` (+ yaratgan bazalarida db_owner — creator avtomatik egasi) yoki lokal `sysadmin`. `Database/` papkasi ERP release bilan bir xil versiyada.

```bash
dotnet publish tahsinERP.ProvisioningAgent -c Release -r win-x64 --self-contained false -o C:\tahsinERP\Agent
```

Sozlama: `C:\tahsinERP\Agent\appsettings.json` (sirsiz) + env (`TAHSIN_AGENT_` prefiksi, `__` = bo'lim):

| Kalit | Ma'nosi |
|---|---|
| `Panel:Url`, `Panel:ServerId` | `https://admin.<domen>`, panel Servers.Id |
| `Panel:Secret` | **env** `TAHSIN_AGENT_Panel__Secret` — panel chiqargan siri |
| `Sql:Server` (+ `User`/`Password` env) | lokal instance; bo'sh login — Windows auth (xizmat hisobi). Panelda yozilgan SqlInstance faqat logda ko'rinadi. |
| `Paths:DatabaseRoot` | `...\tahsinERP\Database` (tools/ ichida) |
| `Paths:TenantsFile` | bo'sh — `TAHSINERP_TENANTS_FILE` env yoki `%ProgramData%\tahsinERP\tenants.json` (ERP bilan **bir xil** fayl) |
| `Paths:BaseDomain` | tanlangan domen (fayl yangi bo'lsa `baseDomain` yoziladi). Domen yo'q va `TAHSINERP_BASE_DOMAIN` ham yo'q bo'lsa agent tenants.json yozmaydi (ERP uni buzilgan deb 503 berardi). |
| `Paths:BackupDir` | SQL xizmati yoza oladigan papka (vaqtinchalik .bak, demo shabloni). `icacls <papka> /grant "NT Service\MSSQLSERVER:(OI)(CI)M"` |
| `Paths:ArchiveDir` | `LocalBackupStore` ildizi (`<slug>\DB_x_yyyyMMdd_HHmmss.bak` + `.sha256`) |
| `Paths:StateDir` | agent holati (demo reset sanasi) va `logs\job-<id>.log` |
| `Demo:*` | `Slug` (panel `Provisioning:DemoSlug` bilan bir xil), `ResetHour` 3, `ResetWindowHours` 3, `TimeZone` Asia/Tashkent, `UserLogin` demo, `UserPassword` (**env**; bo'sh — Database/Demo skriptidagi `demo2026` qoladi) |

Windows Service (Administrator):

```bash
sc.exe create tahsinERP.ProvisioningAgent binPath= "C:\tahsinERP\Agent\tahsinERP.ProvisioningAgent.exe" start= delayed-auto obj= "NT AUTHORITY\NETWORK SERVICE"
```
```bash
sc.exe failure tahsinERP.ProvisioningAgent reset= 86400 actions= restart/60000/restart/60000/restart/60000
```

Xizmat env'i: `HKLM\SYSTEM\CurrentControlSet\Services\tahsinERP.ProvisioningAgent` → `Environment` (REG_MULTI_SZ) ga
`TAHSIN_AGENT_Panel__Secret=...`, `TAHSIN_AGENT_Demo__UserPassword=...` qatorlari. Xizmat hisobi `tenants.json` papkasiga yozish,
`Database\` ga o'qish huquqiga ega bo'lsin; ERP app pool esa `tenants.json` ni o'qiy olsin.

Qo'lda tekshirish: `tahsinERP.ProvisioningAgent.exe --once` (bitta job va chiqish), `build-demo-template` (shablonni qayta yaratish —
demo skriptlari o'zgarganda).

## 5. tenants.json ga yozish (02_MULTI_TENANT.md bilan moslik)

- Lock fayl (`tenants.json.lock`, FileShare.None) → o'qish → o'zgartirish → `tenants.json.tmp-*` ga yozish (WriteThrough + Flush) →
  `File.Move(..., overwrite)` (bir disk ichida atomik almashtirish). Avvalgi nusxa — `tenants.json.prev`.
- ERP faylni 15 soniyada `LastWriteTimeUtc` bo'yicha qayta o'qiydi: yangi faylning vaqti eskisidan qat'iy katta qilib qo'yiladi
  (ERP «o'zgarmagan» deb o'tkazib yubormasin). Ilovani qayta ishga tushirish shart emas.
- Boshqa tenantlar, `hosts`, `connectionString` va noma'lum maydonlar saqlanadi. ERP qoidalari (slug regex, rezerv nomlar, baza nomi,
  takroriy slug) yozishdan oldin tekshiriladi — buzilgan fayl yozilmaydi; mavjud fayl buzilgan bo'lsa job Failed (qo'lda tuzatiladi).
- Testlarda ERP'ning haqiqiy `TenantRegistry.cs` fayli ulanib, agent yozgan fayl u bilan o'qiladi (`ProvisioningTests.cs`).

## 6. Demo tenant (ixtiyoriy — 2026-10-26 dan o'chiq)

**Holat.** Umumiy ko'rgazma `demo.tahsinerp.uz` olib tashlandi: saytda unga havola yo'q edi (landing «umumiy ko'rgazma emas, shaxsiy
demo» deydi), sinov endi tanlangan modullar va ularning namuna ma'lumoti bilan ochiladi (11-bo'lim), demo esa serverda alohida
`erp-demo` instance, baza va tungi tiklashni band qilib turardi. Standart sozlama: panel `Provisioning:DemoSlug = ""`, agent
`Demo:Enabled = false`, `Demo:Slug = ""` — hech bir tenant demo hisoblanmaydi, Reset rejalashtirilmaydi. Kod qoldirildi; qayta yoqish —
pastdagi sozlash va ikkala slug bir xil.

Prod'dan olib tashlash tartibi:
1. Panel → Tashkilotlar → `demo` → **Arxiv** (Owner, sabab) → job tugagach **O'chirish** (baza, arxiv, tenants.json yozuvi).
2. RDP, Administrator PowerShell: `tools\deploy\Remove-ErpInstance.ps1 -Slug demo -Demo -RemoveFiles` — IIS `erp-demo` sayt/pool,
   Updater `state.json` dagi instance, `C:\inetpub\erp\demo`, `DB_DEMO_TEMPLATE.bak`, agentdagi tungi tiklash.
3. Relizlarni avval sinash uchun (03_AUTO_UPDATE.md 4-qadam) — oddiy sinov tenanti (masalan `test`).

Yoqilgan bo'lsa (eski tavsif):

- Panelda bir marta: SaaS tenant `demo` (yoki `Provisioning:DemoSlug`), uzoq muddatli obuna (masalan KORP, Owner qo'lda — Trial 14 kundan
  keyin to'xtatib qo'yadi) → «Bazani yaratish».
- Har kecha Toshkent 03:00–06:00 oralig'ida agent `POST /schedule {Reset, demo, <sana>}` yuboradi (panel kalit bo'yicha bir marta yaratadi),
  so'ng job'ni bajaradi: `DB_DEMO_TEMPLATE.bak` (yo'q bo'lsa `New-TenantDb.ps1 -Demo -NoAdmin -BackupTo` bilan `Database/Demo/*.sql` dan
  yaratiladi) → RESTORE (REPLACE) → keyingi migratsiyalar → `demo` foydalanuvchi (Admin roli, parol almashtirish majburiy EMAS) → yangi RegToken.
- Agent o'chiq bo'lib, oyna o'tib ketsa — o'sha kuni reset qilinmaydi (kunduzi mehmon ma'lumoti to'satdan yo'qolmasin).
- Login sahifasida demo login/parolni ko'rsatish — ERP UI ishi, hali qilinmagan (7-bo'lim).

## 7. Wildcard DNS / TLS / IIS (server sozlash, kod emas)

Domen nomi hali tanlanmagan — quyida `<domen>`. Tanlangach: ERP `TAHSINERP_BASE_DOMAIN` (yoki tenants.json `baseDomain`), agent
`Paths:BaseDomain`, panel `ControlPanel:Provisioning:BaseDomain` — uchalasi bir xil qiymat.

### 7.1 DNS
- `A  <domen>          → <ERP server IP>` (ixtiyoriy, landing)
- `A  *.<domen>        → <ERP server IP>` — barcha tenant subdomenlari (yangi tenant uchun DNS o'zgarmaydi)
- `admin.`, `my.`, `pay.` — panel bir serverda bo'lsa wildcard yetarli; alohida serverda bo'lsa aniq A-yozuv (aniq yozuv wildcard'dan ustun).
- DNS provayder API'si bo'lishi kerak (Cloudflare, Route 53 va h.k.) — DNS-01 sertifikat uchun.

### 7.2 Wildcard TLS — win-acme (Let's Encrypt, DNS-01)
1. `https://www.win-acme.com` dan win-acme (pluginli build, masalan Cloudflare yoki Route53) — `C:\tools\win-acme`.
2. Sertifikat (Administrator):
   ```bash
   wacs.exe --source manual --host "<domen>,*.<domen>" --validationmode dns-01 --validation cloudflare --cloudflareapitoken <token> --store certificatestore --certificatestore WebHosting --installation iis --installationsiteid <ERP sayt ID>
   ```
   Route 53 uchun: `--validation route53 --route53accesskeyid ... --route53secretaccesskey ...` (yoki EC2/Lightsail IAM roli).
   Token faqat `_acme-challenge` TXT yozuvini o'zgartira oladigan minimal huquqli bo'lsin.
3. win-acme Windows Task Scheduler'ga yangilash vazifasini o'zi qo'shadi (har kuni tekshiradi, ~60 kunda yangilaydi) va IIS binding'dagi
   sertifikatni almashtiradi. Tekshirish: `wacs.exe --list`, `wacs.exe --renew --force` (sinov).

### 7.3 IIS binding
- ERP sayti (bitta ilova, bitta app pool):
  ```bash
  %windir%\system32\inetsrv\appcmd.exe set site /site.name:"tahsinERP" /+bindings.[protocol='https',bindingInformation='*:443:*.<domen>',sslFlags='1']
  ```
  (`sslFlags=1` — SNI; IIS 10+ wildcard host header `*.<domen>` ni qo'llaydi.) HTTP → HTTPS: `*:80:*.<domen>` binding + URL Rewrite qoidasi.
- Panel sayti alohida: `admin.<domen>` aniq binding (aniq host wildcard'dan ustun, `Install-PanelIis.ps1 -HostName admin.<domen>`).
- Sertifikat ikkala saytda bitta (`<domen>, *.<domen>`) bo'lishi mumkin.
- ERP app pool `TAHSINERP_TENANTS_FILE` (yoki standart `%ProgramData%\tahsinERP\tenants.json`) ni o'qiy olsin; agent shu faylga yozadi.
- Tekshirish: yangi tenant yaratilgach ~15 soniyada `https://<slug>.<domen>` ochiladi (yangi DNS/IIS amali kerak emas);
  `enabled:false` — 404 «Tashkilot topilmadi».

## 8. Lokal ishga tushirish (dev)

1. Panel: `04_PANEL_MVP.md` 2-bo'lim (`migrate`, `create-owner`, `dotnet run`). `appsettings.Development.json` yoki user-secrets'da
   `ControlPanel:Provisioning:BaseDomain` = masalan `localtest.me` (`*.localtest.me` → 127.0.0.1).
2. Panel → Serverlar → «+ Server» (Host `localhost`, SQL `.`) → «Agent kalitini chiqarish».
3. Agent (user-secrets yoki env):
   ```bash
   cd tahsinERP.ProvisioningAgent
   ```
   ```bash
   dotnet user-secrets set "Panel:Secret" "<siri>"
   ```
   `appsettings.Development.json` yoki env: `Panel:Url=https://localhost:7243`, `Panel:ServerId`, `Paths:DatabaseRoot=..\tahsinERP\Database`,
   `Paths:TenantsFile` (dev uchun alohida fayl), `Paths:BaseDomain`, `Paths:BackupDir` (SQL yoza oladigan, yuqoridagi `icacls`),
   `Demo:UserPassword`.
   ```bash
   dotnet run --environment Development
   ```
4. Panel → «+ Sinov ochish» → modullarni belgilang → parol ko'rsatiladi → ~30 soniyada job «Bajarildi».
5. ERP'ni ko'p-tenant rejimida sinash: ERP app pool/IIS Express'ga `TAHSINERP_TENANTS_FILE` = agent yozgan fayl.

Testlar (lokal SQL'da vaqtinchalik bazalar, oxirida o'chiriladi; `C:\ProgramData\tahsinERP-test` ga SQL xizmati uchun ruxsat beriladi):

```bash
dotnet test tahsinERP.ControlPanel.Tests
```

## 9. Ma'lum cheklovlar / keyingi qadamlar

- `S3BackupStore` yo'q — `IBackupStore` interfeysi tayyor; AWS SDK chaqiruvi hali yo'q. Arxiv faqat lokal papkada (zaxirasini alohida oling).
- Arxivdan tiklash (Restore from archive) amali yo'q (00_PLAN 6: «S3'dan .bak → yangi kalit») — keyingi bosqich.
- Shablon bazadan RESTORE bilan yangi tenant (00_PLAN 7.2) emas, to'g'ridan-to'g'ri `New-TenantDb.ps1` (~15 s) ishlatiladi (03 hujjatdagi «hozircha oddiyroq yo'l»).
- Demo foydalanuvchini ERP login sahifasida ko'rsatish — ERP UI o'zgarishi kerak.
- Bir server = bitta agent. Bir nechta agent bitta serverda ishlasa ham xavfsiz (lease RowVersion bilan), lekin kerak emas.
- Agent Windows Service sifatida haqiqiy serverda hali ishga tushirilmagan (testlar konsol/in-process).

## 10. Foydalanuvchi faolligi (Faollik) va maxfiylik

Maqsad: sotuv bosqichida sinov/demo tenant ishlatilyaptimi — ko'rish (oxirgi kirish, kunlik kirish/amallar, modullar, so'nggi yozuvlar).
Arxitektura o'zgarmaydi: **panel tenant bazalariga ulanmaydi**.

- Agent har `Usage:CheckMinutes` (2) daqiqada `POST /api/agent/usage/targets` so'raydi; panel muddati kelgan tenantlarni beradi
  (`ControlPanel:Usage:TrialIntervalMinutes` 15 — sinov va demo, `IntervalMinutes` 60 — qolganlar) va kursorlarni (oxirgi ID).
- Agent tenant bazasidan faqat **SELECT** qiladi: `USERS(ID, Uname, IsActive, IsDeleted)`, `USER_ENTRIES(ID, UserID, DateTime)`,
  `USERLOGS(ID, UserID, DateTime, ControllerName, ActionName)` — kursordan keyingi, ko'pi bilan `BatchLimit` (2000) qator, oxirgi 90 kun.
  ERP vaqtni server lokal vaqtida yozadi — agent (ERP bilan bir serverda) UTC'ga o'giradi. `POST /api/agent/usage` ga yuboradi.
- Demo reset: bazadagi MAX(ID) kursordan kichik bo'lsa `ResetDetected` — kursorlar boshidan; panel yozuvlari (TenantId, Tur, ID, vaqt) bo'yicha takrorlanmaydi.
- Panel: `UsageStates` (kursor, oxirgi kirish/amal, xato), `UsageUsers` (login + holat), `UsageEvents` (90 kun, `Usage:RetentionDays`;
  kunlik ish eskilarini o'chiradi). Tenant sahifasida «Faollik» bloki → `/Tenants/Activity/{id}`: kunlik grafik (7/30/90 kun),
  foydalanuvchilar, modullar, so'nggi 200 yozuv filtr bilan (foydalanuvchi, modul, tur, matn). Dashboard: «Faol emas sinovlar» —
  baza yaratilganiga va oxirgi kirishga `InactiveTrialDays` (3) kundan oshgan sinov tenantlari (demo kirmaydi).

**Maxfiylik (nima yuborilMAYDI):** `USERS.Password`, `HashCode`, `FullName`, `Email`; `USER_ENTRIES.IP/MAC`; `USERLOGS.IP/MacAddr` —
agent ularni o'qimaydi ham. `ActionName` erkin matn (hujjat/kontragent nomi bo'lishi mumkin): agentda email → `[email]`,
9+ xonali raqam/telefon (STIR, karta, hisob raqami) → `[raqam]`, 200 belgigacha qisqartiriladi; butunlay o'chirish —
agent `Usage:IncludeActionText=false` (faqat modul va vaqt). Ma'lumot faqat xodimlarga (barcha rollar o'qiydi), 90 kundan keyin o'chadi.
Mijoz bilan shartnoma/maxfiylik siyosatida «xizmat sifati uchun foydalanish statistikasi yig'iladi» bandi bo'lishi kerak.

## 11. Sinov modullari (2026-10-25)

Sinov ochishda hamma modul uchun demo ochilmaydi: xodim formada modullarni belgilaydi (SRM, CRM, Topshiriqlar, MES, Muhandislik,
Moliya, HR, Logistika, Hujjatlar, Distributsiya, +Sayt-katalog — `EditionModules.ProductModule`). Kamida bitta modul majburiy;
MES tanlansa Muhandislik avtomatik qo'shiladi (`EditionModules.RequiredBy`). tahsinerp.uz demo so'rovidan ochilsa — so'rovdagi modullar
oldindan belgilangan.

**Kalit (litsenziya).** Alohida paket kodi yoki yangi kalit maydoni o'rniga mavjud Edition qatori kengaytirildi:
`TRIAL:Srm,Crm,Tasks` (+ Sayt tanlansa `+SITE`, xuddi pullik xizmat obunasi kabi). Sabab: v2 imzolangan payload formati va ERP'ning
`EditionGate` → `EditionModules.IsModuleAllowed` yo'li o'zgarmaydi, menyu/controller/API cheklovi shu bitta xaritadan ishlaydi;
`PlanCodes.Trial` va hayot sikli (14 kun, 5 foydalanuvchi, Suspended) ham o'zgarmaydi. Ruxsat = paket modullari ∩ ro'yxat (kalit
paketdan kengaytira olmaydi). Hamma modul tanlansa ro'yxat yozilmaydi (`TRIAL`). `:` ni bilmaydigan eski ERP bunday qatorni noma'lum
paket deb hamma modulni ochadi — yopib qo'ymaydi (sinov firmalari doim joriy versiyada yaratiladi).

| Joy | Nima saqlanadi |
|---|---|
| `Tenants.Modules` | baza qaysi modullar uchun tayyorlangan (`Srm,Crm,Website`; null — hammasi: eski tenantlar, demo) — Create payload'iga |
| `Subscriptions.Modules` | sinov obunasidagi modullar — `LicenseIssuer.ComposeEdition(plan, xizmatlar, modules)` → kalitdagi Edition. Pullik obunaga o'tilganda yangi obuna (Modules = null) paketning hamma modullarini ochadi |
| `JobPayload.Modules` | agentga: `New-TenantDb.ps1 -Modules Srm,Crm,...` (agent nomlarni `EditionModules` bilan tekshiradi; noma'lum — Failed) |
| tenant bazasi `dbo.SETUP_MODULES` (032) | tanlangan modullar; `Setup/300_ModuleProfile.sql` va `Demo/*.sql` shunga qarab ishlaydi (bo'sh — hammasi) |

**Baza (Database/README.md «Demo ma'lumot», «Modul profili»).** Spravochniklar (valyuta, o'lchov birligi, ТНВЭД, moliya kategoriyalari...)
Core — hammaga to'liq. Modulga bog'liq sozlamalar: har tanlangan modulga rol (faqat shu modul controllerlariga huquq), tanlanmagan modul
omborlari o'chirilgan, namuna ma'lumotsiz bazada boshlang'ich kategoriyalar. Namuna ma'lumot (`SampleData`) — faqat tanlangan modullar
fayllari; bog'liq ssenariy yetishmasa (masalan CRM bor, MES yo'q) boshlang'ich qoldiq kirimi bilan to'ldiriladi.

**Rollar va bosh sahifa.** Har tanlangan modulga rol: o'z moduli — ko'rish+o'zgartirish (C), qo'shni modullarning kerakli bo'limlari
va spravochniklar — faqat ko'rish (V), o'chirish — faqat Admin; to'liq jadval — Database/README.md «Rollar va ruxsatlar». ERP bosh
sahifasi (KPI, grafiklar, signallar) va modul plitkalari faqat kalitdagi modullar va foydalanuvchi roli ko'ra oladigan bo'limlar bilan
chiqadi (`HomeSections`) — sinov mijozi birinchi kirishda o'zi tanlagan modullar ma'lumotini ko'radi.

**Versiya mosligi.** Yangi panel + eski agent: `Modules` e'tiborsiz — baza hamma modul bilan (kalit baribir tanlanganlarni ochadi).
Agent `Database/` papkasi ERP reliz bilan keladi (Updater `/MIR` — eski demo fayllari qolmaydi); demo shabloni `DB_DEMO_TEMPLATE.bak`
eski fayllar bilan yaratilgan bo'lsa `build-demo-template` bilan qayta yarating.

Tekshirish: `TrialModulesUnitTests` (Edition qatori, agent argumentlari, SQL xaritasi `EditionModules` bilan bir xilligi),
`ProvisioningIntegrationTests.TrialWithModules_DatabaseHasOnlySelectedModulesSetupAndSampleData` (lokal SQL'da to'liq oqim),
`WebUiProvisioningTests` (forma).
