# tahsinERP — serverga o'rnatish bo'yicha yagona qo'llanma

Holat: 2026-10-21 (distributsiya yakuniy relizi — migratsiya 027 bilan — hisobga olingan). Bitta Windows server (variant A, `00_SERVER_PLAN.md`) uchun **hamma komponent bir joyda**: ERP, boshqaruv paneli
(`admin.`) va mijoz portali (`my.`), provisioning agenti, Telegram bot (distributsiya), sayt-katalog, SQL bazalar, domen va SSL.
Manbalar: `00_SERVER_PLAN.md`, `Docs/ControlPanel/04–08`, `Docs/Distribution/02–03`, `tahsinERP.Bot/README.md`,
`tahsinERP.Site/README.md`, `Database/README.md`, `tools/DEPLOY-SECURITY.md`. Ziddiyatlar shu qo'llanmada hal qilingan (oxirgi bo'lim).

Domen — **`tahsinerp.uz`** (sotib olingan, 2026-10-21). **Lightsail "SQL Server 2022 Express" blueprint'ida SQL standart nom bilan o'rnatilgan — ulanishda `.` ishlating** (qo'llanmadagi `.\SQLEXPRESS` o'rniga; xizmat `MSSQL$SQLEXPRESS` bo'lgan serverlarda — `.\SQLEXPRESS`). SQL — `.\SQLEXPRESS`.

> **Yangilash (2026-10-22 dan):** birinchi o'rnatishdan keyin barcha yangilanishlar — **panel → Yangilanishlar** tugmasi bilan
> (CI imzolagan GitHub relizi, serverdagi Updater xizmati, avtomatik zaxira va rollback). ERP har firma uchun alohida IIS instance'ida
> (`erp-<slug>`), versiya firma bo'yicha. To'liq: **`03_AUTO_UPDATE.md`**. Quyidagi qo'lda yangilash yo'llari — faqat favqulodda holat uchun.

---

## 0. Boshlashdan oldin qaror va tayyorgarlik

| # | Nima | Izoh |
|---|---|---|
| 1 | Domen | **`tahsinerp.uz`** — sotib olingan. DNS'ni Cloudflare'ga o'tkazing (4-qator). |
| 2 | Server joyi | Real mijozlar uchun **O'zbekistondagi** data-markaz (shaxsga doir ma'lumotlar qonuni, 27¹-modda — yurist bilan tasdiqlang). AWS — faqat demo. |
| 3 | Server | Boshlanish: **AWS Lightsail, blueprint "SQL Server 2022 Express"** (Windows Server 2022 + SQL Express tayyor), **2 vCPU / 4 GB / 80 GB SSD**, Static IP biriktirilgan; firewall — RDP faqat o'z IP'ingizdan (`/32`), 80/443 hamma uchun. 4 GB'da SQL xotirasini cheklang (3-bo'lim). Firmalar ko'payganda — snapshot orqali 8/16 GB tarifga. |
| 4 | Cloudflare | Domen DNS'ini Cloudflare'ga ulang; **API token faqat `Zone → DNS → Edit`** huquqi bilan. |
| 5 | Sirlar ombori | Parol menejeri (masalan Bitwarden/KeePass) — pastdagi 8-bo'limdagi sirlar faqat shu yerda va serverda saqlanadi, **chatga, repoga, pochtaga yozilmaydi**. |

### Bir xil bo'lishi SHART bo'lgan qiymatlar

| Qiymat | Qayerlarda |
|---|---|
| Asosiy domen `tahsinerp.uz` | ERP `TAHSINERP_BASE_DOMAIN` (yoki `tenants.json` → `baseDomain`), agent `Paths:BaseDomain`, panel `ControlPanel:Provisioning:BaseDomain` va `ControlPanel:Hosts:Domain` |
| Sayt domeni `sayt.tahsinerp.uz` | Site `Site__BaseDomain`, ERP `SiteBaseDomain` |
| Bot/sayt master kaliti (≥32 belgi) | ERP `TAHSINERP_BOT_KEY` = Bot `Bot__MasterKey` = Site `Site__MasterKey` |
| `tenants.json` yo'li | ERP va agent bir faylni o'qiydi: `C:\ProgramData\tahsinERP\tenants.json` |
| `Database\` papka versiyasi | Agent `Paths:DatabaseRoot` ERP relizi bilan bir xil bo'lishi kerak |

---

## 1. Server: dastlabki sozlash

1. RDP faqat o'z IP'ingizdan (provayder firewall'ida). Windows Firewall: kirishga faqat **80, 443** (va RDP — cheklangan).
2. Windows Update → hammasini o'rnating, qayta yuklang.
3. **Hammasi bitta skript bilan** — `tools\deploy\Install-ServerPrereqs.ps1` ni serverga ko'chiring: o'z RDP klientingiz (`mstsc`, Lightsail → Connect → "Connect using your own Remote Desktop client" — IP, `Administrator`, parol) Local Resources'da **Clipboard** va **Drives** yoqilgan holda fayl Ctrl+C/Ctrl+V bilan ko'chadi; brauzer RDP'da — matnni Notepad'ga qo'yib **UTF-8 with BOM** kodirovkasida saqlang. So'ng administrator PowerShell'da:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Install-ServerPrereqs.ps1 -SqlMaxMemoryMB 1024
```

   Skript o'rnatadi/sozlaydi: IIS rollari (WebSockets, compression, redirect) va .NET 4.8 tekshiruvi; **ASP.NET Core 10 Hosting Bundle**
   (eng oxirgi patch, rasmiy metadata'dagi SHA-512 va Microsoft imzosi bilan tekshiriladi); **IIS URL Rewrite 2.1**; **win-acme** +
   Cloudflare DNS plagini → `C:\tools\win-acme` (qayd qilingan versiya va SHA-256; papka faqat administratorlarga); ish papkalari;
   SQL xizmati nomini ko'rsatadi va `max server memory` ni cheklaydi. Qayta ishga tushirish xavfsiz (bor narsani o'tkazib yuboradi).
   Server qayta yuklanishni so'rasa — qayta yuklab, skriptni yana ishga tushiring.
4. **SQL Server Express** — Lightsail "SQL Server 2022 Express" blueprint'ida allaqachon o'rnatilgan. Skript chiqargan instance nomi
   (`MSSQL$SQLEXPRESS` → `.\SQLEXPRESS`; `MSSQLSERVER` → `.`) qo'llanmadagi `.\SQLEXPRESS` o'rniga ishlatiladi; nomi boshqacha bo'lsa
   `-SqlInstance` bilan qayta ishga tushiring. Autentifikatsiya: Mixed; `sa` ishlatilmaydi.
5. Ixtiyoriy: **Git** (relizni `git pull` bilan olish uchun) — yoki relizni ZIP ko'rinishida ko'chirish.

---

## 2. DNS va SSL

### 2.0 Domenni Cloudflare'ga ulash (bir marta)

1. Cloudflare → **Domains → Add domain** (eski interfeysda "Add a site") → `tahsinerp.uz` → DNS yozuvlari: quick scan yoki qo'lda → **Free** tarif. Cloudflare ikki nameserver beradi (`xxx.ns.cloudflare.com`).
2. `.uz` registrator kabinetida (domen sotib olingan joy) domen NS yozuvlarini shu ikkitasiga almashtiring. Yangilanish bir necha soatdan 24 soatgacha.
3. Cloudflare → My Profile → API Tokens → **Create token** → "Edit zone DNS" shabloni, Zone: `tahsinerp.uz` → token parol menejeriga (win-acme uchun).

### 2.1 Cloudflare yozuvlari (hammasi **DNS only — kulrang bulut**)

| Yozuv | Tur | Qiymat |
|---|---|---|
| `tahsinerp.uz` | A | server IP |
| `*.tahsinerp.uz` | A | server IP (firmalar ERP'si: `firma1.tahsinerp.uz`; `admin.`, `my.`, `bot.` ham shu wildcard'ga tushadi) |
| `*.sayt.tahsinerp.uz` | A | server IP (sayt-katalog: `firma1.sayt.tahsinerp.uz`) |
| `sites.tahsinerp.uz` | A | server IP (mijozning o'z domeni shu nomga CNAME qiladi) |

> To'q sariq bulut (Cloudflare proxy) yoqilsa — ERP `TrustedProxies` ga Cloudflare IP diapazonlarini yozish kerak. Boshlanishiga **proxy'siz** ishlang.

Band nomlar (firma nomi bo'la olmaydi): `admin, my, pay, www, api, app, mail, smtp, static, cdn, assets, status, panel, erp, bot, sayt, sites`.

### 2.2 Wildcard sertifikatlar (win-acme, Cloudflare DNS-01, avtomatik yangilanadi)

IIS'da ERP sayti yaratilgandan keyin (4-bo'lim) ishga tushiring:

```powershell
cd C:\tools\win-acme
.\wacs.exe --source manual --host "tahsinerp.uz,*.tahsinerp.uz" --validationmode dns-01 --validation cloudflare --cloudflareapitoken <TOKEN> --store certificatestore --certificatestore WebHosting --installation iis --installationsiteid <ERP sayt ID>
.\wacs.exe --source manual --host "*.sayt.tahsinerp.uz" --validationmode dns-01 --validation cloudflare --cloudflareapitoken <TOKEN> --store certificatestore --certificatestore WebHosting --installation none
.\wacs.exe --list
```

win-acme Task Scheduler'ga yangilash vazifasini o'zi qo'shadi (~60 kunda). Sinov: `.\wacs.exe --renew --force`.

---

## 3. SQL Server

0. **4 GB RAM'li serverda** SQL xotirasini cheklang (aks holda IIS ilovalari bilan xotira talashadi):

```sql
EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'max server memory (MB)', 1024; RECONFIGURE;
```

1. SQL login (ilovalar uchun, `sa` emas), masalan `tahsin_app`, kuchli parol bilan. Agent bazalar yaratadi — unga `dbcreator`.
2. Ilova pullari Windows autentifikatsiyasi bilan ulansa — IIS pool identity'lari uchun login:

```sql
CREATE LOGIN [IIS APPPOOL\tahsinERP.ControlPanel] FROM WINDOWS;
CREATE LOGIN [IIS APPPOOL\tahsinERP.Bot] FROM WINDOWS;
CREATE LOGIN [IIS APPPOOL\tahsinERP.Site] FROM WINDOWS;
CREATE LOGIN [NT AUTHORITY\NETWORK SERVICE] FROM WINDOWS;  -- agent
ALTER SERVER ROLE dbcreator ADD MEMBER [NT AUTHORITY\NETWORK SERVICE];
```

   Bazalar yaratilgach har biriga `db_owner` (migratsiya uchun) yoki `db_datareader` + `db_datawriter`.
3. Zaxira papkasiga SQL xizmati yozishi uchun (Express'da SID `MSSQL$SQLEXPRESS`):

```powershell
icacls C:\ProgramData\tahsinERP\Backups /grant "NT Service\MSSQL`$SQLEXPRESS:(OI)(CI)M"
```

Bazalar: har firma — `DB_<slug>`; markaziy — `tahsin_control` (panel), `tahsin_bot`, `tahsin_site` (o'zi yaratiladi).

---

## 4. ERP (`{firma}.tahsinerp.uz`)

> **Tayyor skript:** `tools\deploy\Install-Erp.ps1 -Domain tahsinerp.uz` (paket: `app\` + `database\` + skript) 4.2–4.4-bandlarni va
> 3-bo'limdagi ERP/agent SQL login'larini bitta qadamda bajaradi. ERP SQL'ga **Windows auth** bilan (`IIS APPPOOL\tahsinERP`, parolsiz)
> ulanadi; `database\` → `C:\tahsinERP\Database` (veb-papkadan tashqarida). Yangilash: `-KeepSecrets`.
> Quyidagi qo'lda qadamlar — skript nima qilishini tushunish uchun.
>
> **Firma instance'lari (`03_AUTO_UPDATE.md`):** `Install-Erp.ps1` yaratgan umumiy `tahsinERP` sayti (`*.<domen>`) fallback va HTTP→HTTPS
> uchun qoladi; har firma `tahsinERP.Updater.exe split-erp` yoki panel "Instance yaratish" bilan o'z saytiga (`erp-<slug>`, https `<slug>.<domen>`)
> o'tadi — IIS'da aniq host binding wildcard'dan ustun. Keyingi ERP relizlari firma bo'yicha paneldan o'rnatiladi.

### 4.1 Build (ishlab chiquvchi kompyuterida)

1. **Litsenziya k2 kaliti** (bir marta, 5.1-bandda panel kaliti yaratilgandan keyin): `print-public-key` chiqargan XML'ni
   `tahsinERP.Licensing/LicenseHelper.cs` → `K2PublicKeyXml` ga qo'ying. **Busiz panel bergan kalitlar ERP'da rad etiladi.**
2. Release build (VS 2022/18 MSBuild):

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" tahsinERP\tahsinERP.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:WebPublishMethod=FileSystem /p:PublishUrl=C:\publish\tahsinERP /p:DeleteExistingFiles=true
```

   Release transformatsiyasi: ulanish satri olib tashlanadi, `debug` o'chadi, cookie'lar faqat HTTPS, HSTS qo'shiladi — **sayt faqat HTTPS'da ishlaydi**.
   View'lar build'da tekshirilmaydi — relizdan oldin `Database\tools\RegressionProbe\Run-RazorCheck.ps1`.
3. `C:\publish\tahsinERP` → serverda `C:\inetpub\tahsinERP` (ichida `Database\` papkasi ham bo'lsin — agent undan foydalanadi).

### 4.2 Yangi machineKey (repodagi qiymat ishlab chiqarishda ISHLATILMAYDI)

```powershell
function New-Hex($n){ -join ((1..$n) | % { '{0:X2}' -f (Get-Random -Max 256) }) }
"validationKey=$(New-Hex 64)"; "decryptionKey=$(New-Hex 32)"
```

Chiqqan qiymatlarni serverdagi `C:\inetpub\tahsinERP\Web.config` → `<machineKey validationKey="..." decryptionKey="..." validation="HMACSHA256" decryption="AES" />` ga yozing va parol menejerida saqlang (har relizda shu qiymat qayta qo'yiladi — aks holda foydalanuvchilar tizimdan chiqib ketadi).

### 4.3 IIS sayti

```powershell
Import-Module WebAdministration
New-WebAppPool tahsinERP
Set-ItemProperty IIS:\AppPools\tahsinERP -Name managedRuntimeVersion -Value v4.0
Set-ItemProperty IIS:\AppPools\tahsinERP -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\tahsinERP -Name processModel.idleTimeout -Value 00:00:00
New-Website -Name tahsinERP -PhysicalPath C:\inetpub\tahsinERP -ApplicationPool tahsinERP -HostHeader "*.tahsinerp.uz" -Port 80
& $env:windir\system32\inetsrv\appcmd.exe set site /site.name:"tahsinERP" /+bindings.[protocol='https',bindingInformation='*:443:*.tahsinerp.uz',sslFlags='1']
```

HTTP → HTTPS: IIS URL Rewrite'da sayt darajasida "Redirect to HTTPS" qoidasi. Keyin 2.2-bandagi win-acme'ni ERP sayti ID'si bilan ishga tushiring.

### 4.4 Ulanish satri, tenantlar va boshqa sozlamalar

```powershell
# Ulanish satri (fayl; faqat Administrators va ERP pool o'qiydi). Initial Catalog — har firma uchun avtomatik almashtiriladi.
Set-Content -Encoding utf8 C:\ProgramData\tahsinERP\db.connection "data source=.\SQLEXPRESS;initial catalog=DB_demo;user id=tahsin_app;password=<PAROL>;MultipleActiveResultSets=True;App=EntityFramework"
icacls C:\ProgramData\tahsinERP\db.connection /inheritance:r /grant:r "Administrators:F" "IIS AppPool\tahsinERP:R"

# Pool muhit o'zgaruvchilari
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
& $appcmd set config -section:system.applicationHost/applicationPools "/+[name='tahsinERP'].environmentVariables.[name='TAHSINERP_BASE_DOMAIN',value='tahsinerp.uz']" /commit:apphost
& $appcmd set config -section:system.applicationHost/applicationPools "/+[name='tahsinERP'].environmentVariables.[name='TAHSINERP_LICENSE_PANEL_URL',value='https://admin.tahsinerp.uz']" /commit:apphost
& $appcmd set config -section:system.applicationHost/applicationPools "/+[name='tahsinERP'].environmentVariables.[name='TAHSINERP_BOT_KEY',value='<MASTER KALIT>']" /commit:apphost
& $appcmd recycle apppool /apppool.name:tahsinERP
```

- `tenants.json` (`C:\ProgramData\tahsinERP\tenants.json`) — **agent o'zi yozadi** (panelda "Bazani yaratish"). Qo'lda format:
  `{"baseDomain":"tahsinerp.uz","tenants":[{"slug":"firma1","database":"DB_firma1","enabled":true}]}` — 15 soniyada qayta o'qiladi.
  ERP pool uni o'qiy olishi, agent yoza olishi kerak.
- `Web.config` appSettings: `SiteBaseDomain` = `sayt.tahsinerp.uz`, `SiteCnameTarget` = `sites.tahsinerp.uz`; Cloudflare proxy bo'lsa `TrustedProxies`.
- Ulanish satri yoki muhit o'zgarsa — pool `recycle`.

---

## 5. Boshqaruv paneli va mijoz portali (`admin.tahsinerp.uz`, `my.tahsinerp.uz`)

### 5.1 Kalitlar (bir marta)

```powershell
cd <repo>\tahsinERP.ControlPanel
dotnet run -- gen-master-key            # ControlPanel__Security__MasterKey — zaxirasini bazalar zaxirasidan ALOHIDA saqlang
dotnet run -- keygen --out C:\ProgramData\tahsinERP\panel-signing-k2.json   # parol so'raladi → ControlPanel__Signing__Passphrase
dotnet run -- print-public-key          # XML → ERP K2PublicKeyXml (4.1-band)
```

### 5.2 O'rnatish (tayyor skript)

```powershell
.\tahsinERP.ControlPanel\deploy\Install-PanelIis.ps1 -HostName admin.tahsinerp.uz -PortalHostName my.tahsinerp.uz `
  -ConnectionString "Server=.\SQLEXPRESS;Database=tahsin_control;Trusted_Connection=True;TrustServerCertificate=True;Application Name=tahsinERP.ControlPanel" `
  -CertThumbprint <*.tahsinerp.uz sertifikat thumbprint>
```

Skript: publish → pool (No Managed Code, AlwaysRunning) → sayt 443 (admin + my SNI) → muhit o'zgaruvchilari (master kalit, imzo paroli,
panel Telegram token va webhook siri yashirin so'raladi) → huquqlar → `migrate`. Yangilashda: `-KeepSecrets`.
Tekshiruv: `https://admin.tahsinerp.uz/health` → `ok`.

### 5.3 Birinchi sozlash

```powershell
dotnet C:\inetpub\tahsinERP.ControlPanel\tahsinERP.ControlPanel.dll create-owner --email siz@pochta.uz --name "Ism Familiya"
```

So'ng `admin.tahsinerp.uz` da: kirish → TOTP 2FA ulash → **Sozlamalar/Vendor** (yuridik nom, INN, QQS kodi, bank, direktor, buxgalter — hisob-faktura uchun majburiy) → paketlar narxi.
`appsettings.Production.json` yoki pool env: `ControlPanel__Provisioning__BaseDomain=tahsinerp.uz`, `ControlPanel__Hosts__Domain=tahsinerp.uz`, `ControlPanel__Portal__OtpLogFallback=false`.

### 5.4 Panel Telegram boti (xodimlarga xabar, portal kirish kodi) — distributsiya botidan ALOHIDA

@BotFather → `/newbot` → token (Install skriptida kiritiladi). Webhook:

```powershell
curl.exe -s "https://api.telegram.org/bot<TOKEN>/setWebhook" -d "url=https://my.tahsinerp.uz/api/telegram/webhook" -d "secret_token=<SECRET>" -d "allowed_updates=[\"message\"]"
```

---

## 6. Provisioning agenti (Windows Service)

### 6.1 Skript bilan (prod'da 2026-10-22, `lightsail-fra-1` shunday o'rnatildi)

1. Paket (lokal): `dotnet publish tahsinERP.ProvisioningAgent -c Release -r win-x64 --self-contained false -o agent-pkg\app`
   + `tools\deploy\Install-Agent.ps1` → `agent-pkg.zip`. Serverda `C:\tahsinERP` ga ochiladi (`C:\tahsinERP\agent-pkg`).
2. `C:\tahsinERP\Database` **to'liq** bo'lishi kerak: `tools\New-TenantDb.ps1`, `Migrations\` (ERP relizi bilan bir xil), `Demo\`
   (demo tenant uchun). Tuzatish fayllarini **ustiga yozing** (`robocopy <manba> C:\tahsinERP\Database /E`), papkani almashtirmang —
   aks holda job "Skript topilmadi" bilan yiqiladi. Papka yangidan ochilsa:
   `icacls C:\tahsinERP\Database /grant "NT AUTHORITY\NETWORK SERVICE:(OI)(CI)RX"`.
3. Panelda: **Serverlar → server → "Agent kalitini chiqarish"** (Owner; kalit bir marta ko'rsatiladi) — Server ID va kalit.
4. Kalit Notepad'da `C:\tahsinERP\s.txt` ga: 1-qator kalit, 2-qator demo foydalanuvchi paroli (ixtiyoriy; bo'sh — `demo2026`).
   Brauzer-RDP'da yashirin maydonga joylash 1 belgi beradi — shuning uchun fayl; skript o'qigach o'chiradi.
5. Administrator PowerShell (Server ID — raqam, burchak qavslarsiz):

```powershell
cd C:\tahsinERP\agent-pkg
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Install-Agent.ps1 -Domain tahsinerp.uz -ServerId 1 -SecretsFile C:\tahsinERP\s.txt
# yangilash: .\Install-Agent.ps1 -Domain tahsinerp.uz -ServerId 1 -KeepSecrets
```

   Skript 6.2 dagi hamma qadamni bajaradi (fayllar → `C:\tahsinERP\Agent`, `appsettings.json`, huquqlar, xizmat, sirlar registry'da).
6. Tekshirish: `Get-Service tahsinERP.ProvisioningAgent` → Running; panelda server "oxirgi aloqa" yangilanadi. Job loglari —
   `C:\ProgramData\tahsinERP\Agent\logs\job-<id>.log` (Event log'ga faqat ogohlantirish/xatolar yoziladi).

### 6.2 Qo'lda (skriptsiz)

1. Panelda: **Serverlar → + Server → "Agent kalitini chiqarish"** (Owner; kalit bir marta ko'rsatiladi) — `ServerId` va sirni yozib oling.
2. O'rnatish:

```powershell
dotnet publish tahsinERP.ProvisioningAgent -c Release -r win-x64 --self-contained false -o C:\tahsinERP\Agent
sc.exe create tahsinERP.ProvisioningAgent binPath= "C:\tahsinERP\Agent\tahsinERP.ProvisioningAgent.exe" start= delayed-auto obj= "NT AUTHORITY\NETWORK SERVICE"
sc.exe failure tahsinERP.ProvisioningAgent reset= 86400 actions= restart/60000/restart/60000/restart/60000
# Sirlar — xizmat muhitida (REG_MULTI_SZ)
New-ItemProperty -Path HKLM:\SYSTEM\CurrentControlSet\Services\tahsinERP.ProvisioningAgent -Name Environment -PropertyType MultiString -Value @('TAHSIN_AGENT_Panel__Secret=<AGENT SIRI>','TAHSIN_AGENT_Demo__UserPassword=<DEMO PAROLI>') -Force
```

3. `C:\tahsinERP\Agent\appsettings.json`: `Panel:Url=https://admin.tahsinerp.uz`, `Panel:ServerId=<ID>`, `Sql:Server=.\SQLEXPRESS`,
   `Sql:AppLogin=IIS APPPOOL\tahsinERP` (har yangi/tiklangan firma bazasida ERP'ga db_owner beradi),
   `Paths:DatabaseRoot=C:\tahsinERP\Database`, `Paths:BaseDomain=tahsinerp.uz`, `Paths:BackupDir=C:\ProgramData\tahsinERP\Backups`.
4. Huquqlar: `NETWORK SERVICE` → `C:\ProgramData\tahsinERP` (yozish), `C:\tahsinERP\Database` (o'qish); SQL `dbcreator` — `Install-Erp.ps1` beradi.
5. Sinov: `C:\tahsinERP\Agent\tahsinERP.ProvisioningAgent.exe --once`, keyin `Start-Service tahsinERP.ProvisioningAgent`.
### 6.3 Demo tenant (`demo.tahsinerp.uz`)

1. Panelda **Firmalar → Sinov**: slug `demo` (= `Provisioning:DemoSlug`), server, "Namuna ma'lumotlar" ✓ → bir yo'la TRIAL obuna va
   "Bazani yaratish" job. `admin` vaqtinchalik paroli **bir marta** ko'rinadi (birinchi kirishda almashtiriladi).
2. Job: `DB_demo` (baseline + migratsiya + seed + `Database\Demo`), ERP SQL login'iga db_owner, `LICENSE_STATE.RegToken`, `tenants.json`.
   Demo tenantda namuna foydalanuvchisi `demo` faol qoladi (boshqa sinov firmalarida o'chiriladi): `demo` / `demo2026` yoki `s.txt` dagi parol.
3. ERP → Litsenziya: agent tokenni yozib qo'ygan ("Token saqlangan") — **"Hozir tekshirish"** bosiladi (bo'sh maydon bilan
   "Panelga ulash" — "token yaroqsiz"). Boshqa firmalarda ham shunday (token 7 kun amal qiladi).
4. Har kecha 03:00–06:00 (Toshkent) agent o'zi Reset rejalashtiradi: birinchi safar `Database\Demo` dan
   `C:\ProgramData\tahsinERP\Backups\DB_DEMO_TEMPLATE.bak` yasaladi, keyin shundan RESTORE → migratsiyalar → `demo` foydalanuvchi →
   yangi RegToken (ERP o'zi qayta ro'yxatdan o'tadi). Demo skriptlari o'zgarsa — `tahsinERP.ProvisioningAgent.exe build-demo-template`.
5. TRIAL 14 kun — muddat tugashidan oldin demo'ga uzoq muddatli obuna (KORP) qo'lda qo'shing, aks holda demo to'xtatiladi.

### 6.4 Birinchi o'rnatishda topilgan va tuzatilgan (2026-10-22)

| Muammo | Tuzatish |
|---|---|
| Lightsail SQL Express blueprint collation'i `SQL_Latin1_General_CP1_CI_AS`, firma bazasi `Cyrillic_General_CI_AS` — `#temp` jadvallar tempdb collation'ini oladi, 014 migratsiya "collation conflict" bilan yiqildi (lokal SQL Cyrillic bo'lgani uchun testlarda chiqmagan) | 014/015/017 va Demo skriptlarida `#temp` matn ustunlari `COLLATE DATABASE_DEFAULT`; runner `-- @supersedes-checksum: <sha>` — oldingi tahrir bilan qo'llangan bazalar (masalan `DB_ipack`) checksum xatosi bermaydi. Test: baza collation'i serverdan farqli. **Yangi migratsiyalarda `#temp` matn ustuniga doim `COLLATE DATABASE_DEFAULT`** |
| Demo tenant Create'da namuna `demo` foydalanuvchisi o'chirib qo'yilardi (birinchi kechagacha demo kirishi yo'q) | Demo tenantda Create ham Reset kabi `demo` ni faol qoldiradi |
| Tuzatish ZIP'i ochilganda `Database\tools` papkasi almashtirildi → "Skript topilmadi" | 6.1-2: ustiga yozish / `robocopy /E` |
| `-ServerId <ID>` burchak qavslari bilan joylandi → PowerShell parse xatosi | Misolda haqiqiy raqam |

### 6.5 Updater

**Updater** (avto-yangilash, LocalSystem): `tools\deploy\Install-Updater.ps1 -Domain tahsinerp.uz` (paket `app\` = `updater.zip`).
   Agent `Updates:*` sozlamalari standart qiymatlar bilan ishlaydi. Tafsilot — `03_AUTO_UPDATE.md` (3-bo'lim).

Qo'lda (agentsiz) firma bazasi kerak bo'lsa:
`.\Database\tools\New-TenantDb.ps1 -Server .\SQLEXPRESS -DbName DB_firma1 -CompanyName '"Firma 1" MChJ'` (oxirgi qatorda `RESULT {...}` — vaqtinchalik admin paroli),
keyin `tenants.json` ga qo'shing. Relizdan keyin barcha firmalarga migratsiya **va seed** (yangi ruxsat modullari seed'da keladi,
masalan 027 bilan `DstAudit`; seed idempotent — qayta ishlatish xavfsiz):
`.\Database\tools\Invoke-DbMigrations.ps1 -Server .\SQLEXPRESS -AllTenants -TenantsFile C:\ProgramData\tahsinERP\tenants.json -Seed` (avval `-DryRun`).
Hozirgi oxirgi migratsiya — `027_DST_FindingsFix.sql`.

---

## 7. Telegram bot (`bot.tahsinerp.uz`) va sayt-katalog (`{firma}.sayt.tahsinerp.uz`)

Ikkalasi ham ASP.NET Core 10 ilovasi; IIS'da panel kabi o'rnatiladi (repoda Windows skripti hali yo'q — qo'lda):

```powershell
dotnet publish tahsinERP.Bot -c Release -o C:\inetpub\tahsinERP.Bot
dotnet publish tahsinERP.Site -c Release -o C:\inetpub\tahsinERP.Site
foreach ($n in 'tahsinERP.Bot','tahsinERP.Site') {
  New-WebAppPool $n; Set-ItemProperty IIS:\AppPools\$n -Name managedRuntimeVersion -Value ''
  Set-ItemProperty IIS:\AppPools\$n -Name startMode -Value AlwaysRunning
  Set-ItemProperty IIS:\AppPools\$n -Name processModel.idleTimeout -Value 00:00:00 }
New-Website -Name tahsinERP.Bot  -PhysicalPath C:\inetpub\tahsinERP.Bot  -ApplicationPool tahsinERP.Bot  -HostHeader bot.tahsinerp.uz -Port 443 -Ssl -SslFlags 1
New-Website -Name tahsinERP.Site -PhysicalPath C:\inetpub\tahsinERP.Site -ApplicationPool tahsinERP.Site -HostHeader "*.sayt.tahsinerp.uz" -Port 443 -Ssl -SslFlags 1
```

Sertifikatlarni bog'lang (`*.tahsinerp.uz` → bot, `*.sayt.tahsinerp.uz` → sayt). Pool muhit o'zgaruvchilari (4.4-banddagi `appcmd` usulida):

| Ilova | O'zgaruvchilar |
|---|---|
| Bot | `ASPNETCORE_ENVIRONMENT=Production`, `Bot__MasterKey=<MASTER KALIT>`, `Bot__PublicBaseUrl=https://bot.tahsinerp.uz`, `ConnectionStrings__Bot=Server=.\SQLEXPRESS;Database=tahsin_bot;Trusted_Connection=True;TrustServerCertificate=True`, `Bot__FilesRoot=C:\ProgramData\tahsinERP\Bot\files`, ixtiyoriy `Bot__Voice__GroqApiKey` |
| Sayt | `ASPNETCORE_ENVIRONMENT=Production`, `Site__MasterKey=<MASTER KALIT>`, `Site__BaseDomain=sayt.tahsinerp.uz`, `ConnectionStrings__Site=Server=.\SQLEXPRESS;Database=tahsin_site;Trusted_Connection=True;TrustServerCertificate=True`, `Site__DataRoot=C:\ProgramData\tahsinERP\Site\cache` |

`FilesRoot`/`DataRoot` papkalariga tegishli pool'ga yozish huquqi bering. Tekshiruv: `https://bot.tahsinerp.uz/health`, `https://demo.sayt.tahsinerp.uz/health`.

### Har firma uchun bot (distributsiya)

1. @BotFather → `/newbot` → token; `/setdomain` → `bot.tahsinerp.uz` (Mini App uchun).
2. **Panel** → Tashkilotlar → firma → **«Telegram bot va sayt-katalog»**: tokenni kiritib «Botni ulash» (token tekshiriladi,
   bot bazasida shifrlanadi, webhook o'rnatiladi; panelda saqlanmaydi), «Saytni yoqish» — sayt-katalog. Bu uchun panel pool'ida
   `ControlPanel__Apps__BotUrl/SiteUrl/AdminKey` bo'lishi kerak — `Install-BotSite.ps1` yozadi (AdminKey = master kalitning HMAC hosilasi).
   Zaxira yo'l — serverda:

```powershell
cd C:\inetpub\tahsinERP.Bot
dotnet tahsinERP.Bot.dll tenant add firma1 https://firma1.tahsinerp.uz/ <TOKEN> --erp-tenant firma1
dotnet tahsinERP.Bot.dll webhook set firma1
```

3. ERP → **Distributsiya → Sozlamalar**: "Telegram bot nomi" va **"Bot ochiq manzili"** = `https://bot.tahsinerp.uz`
   (usiz selfi, tashrif fotosi, yetkazish rasmi va imzo ERP sahifalarida ko'rinmaydi).
4. Ovozli buyurtma (ixtiyoriy): bot pool'ida `Bot__Voice__GroqApiKey` + ERP → **Mashinalar → Yetkazish sozlamalari → "Botda ovozli buyurtma"**.
5. Bot hujjat PDF'lari (yetkazish, hisob-faktura) uchun shrift: Windows'da Arial bor — sozlash shart emas; Linux'ga ko'chirilsa `Bot__FontsDir` (DejaVu/Liberation).
6. Foydalanuvchilar uchun to'liq tartib (agent/haydovchi ulash, Mini App) — `Docs/Distribution/Qollanma/06_TELEGRAM.md`; sinov — `Docs/Distribution/UAT/`.

### Har firma uchun sayt

```powershell
cd C:\inetpub\tahsinERP.Site
dotnet tahsinERP.Site.dll tenant add firma1 https://firma1.tahsinerp.uz/ --erp-tenant firma1
dotnet tahsinERP.Site.dll snapshot sync firma1
```

Firmaning o'z domeni (`shop.firma1.uz`, `firma1.uz`) — panel → firma → «O'z domeni»: DNS tekshiruvi, agent win-acme HTTP-01 bilan
sertifikat va IIS binding qiladi. Bir martalik sozlash (80-port, `ControlPanel__Apps__SiteServerIp`) — `Docs/ControlPanel/09_SITE_CUSTOM_DOMAIN.md`.

---

## 8. Sirlar ro'yxati (bir marta yaratiladi, parol menejerida saqlanadi)

| Sir | Qayerga |
|---|---|
| SQL `tahsin_app` paroli | `db.connection` fayli |
| ERP `machineKey` | serverdagi `Web.config` (har relizda saqlab qolinadi) |
| Bot/sayt master kaliti | `TAHSINERP_BOT_KEY`, `Bot__MasterKey`, `Site__MasterKey` |
| Panel master kaliti | `ControlPanel__Security__MasterKey` (yo'qolsa — firmalar qayta ulanadi, xodimlar 2FA'ni qayta ulaydi) |
| Panel imzo fayli + paroli | `panel-signing-k2.json`, `ControlPanel__Signing__Passphrase` (litsenziya kalitlari shu bilan imzolanadi) |
| Panel Telegram tokeni + webhook siri | Install skripti |
| Agent siri | xizmat muhiti |
| Har firma bot tokeni | `Bot.dll tenant add` (bazada shifrlanadi) |
| Cloudflare API tokeni | win-acme |
| Reliz imzo kaliti (ECDSA, private) | faqat GitHub secret `RELEASE_SIGNING_KEY` (environment `release`); nusxa — parol menejeri |
| GitHub token (read-only, reliz o'qish) | panel pool env `ControlPanel__Updates__GitHubToken` |

---

## 9. Zaxira va kuzatuv

1. **Zaxira** — **`04_BACKUP.md`**: `Install-Backup.ps1` har kecha 01:30 da barcha bazalar (`.bak.gz`) va sozlamalarni
   (`config.zip`) Lightsail bucket'iga yuboradi; 14 kun + oylik nusxa 180 kun, tiklash `Backup-Databases.ps1 -Download`.
   Lightsail Windows instance'da avtomatik snapshot yo'q — shu zaxira yagona himoya. Eng muhimi `tahsin_control` (to'lov tarixi).
2. **Kuzatuv**: UptimeRobot (bepul) — `https://admin.tahsinerp.uz/health`, `https://bot.tahsinerp.uz/health`, `https://demo.sayt.tahsinerp.uz/health`, `https://demo.tahsinerp.uz/Account/Login`.
3. **Relizlar** — panel → **Yangilanishlar** (`03_AUTO_UPDATE.md`): ish vaqtidan tashqarida, avval `demo`, keyin boshqa firmalar.
   Updater o'zi zaxira oladi (fayllar + firma bazasi `.bak`), migratsiya + seed qiladi, `/health` tekshiradi va xatoda qaytaradi.
   Favqulodda qo'lda yo'l (Updater ishlamasa): ERP fayllarini almashtirish (serverdagi `Web.config` machineKey'ni saqlab) →
   `Invoke-DbMigrations -Database <db> -Seed` → pool recycle; panel — `Install-PanelIis.ps1 -KeepSecrets`; bot/sayt — `Install-BotSite.ps1 -KeepSecrets`.
4. **Kuzatuv**ga firma instance'lari: `https://<slug>.tahsinerp.uz/health` (200 — ilova va baza ishlayapti).

---

## 10. Ishga tushgandan keyingi tekshiruv ro'yxati

- [ ] `https://demo.tahsinerp.uz` — login, bosh sahifa, bir nechta modul (UAT hujjati bo'yicha)
- [ ] `http://` → `https://` yo'naltiriladi; noma'lum firma nomi → 404
- [ ] `admin.tahsinerp.uz` — 2FA bilan kirish; `my.tahsinerp.uz` — mijoz kirishi (Telegram kodi)
- [x] Panelda yangi firma → "Bazani yaratish" → agent bajardi → yangi subdomen ochiladi (2026-10-22, `demo`)
- [ ] Panel bergan litsenziya kaliti ERP'da qabul qilinadi (k2 kaliti ERP'ga qo'yilgan)
- [ ] Bot: `/start`, Mini App ochiladi, buyurtma ERP'ga tushadi; selfi/foto ERP'da ko'rinadi ("Bot ochiq manzili")
- [ ] Distributsiya UAT (`Docs/Distribution/UAT/`, 135 test) asosiy ssenariylari real Telegram bilan
- [ ] Sayt: `demo.sayt.tahsinerp.uz` katalog ko'rinadi
- [ ] Zaxira fayllari paydo bo'ldi va serverdan tashqariga ko'chdi; bitta bazani sinov tiklash

---

## 11. Hujjatlardagi ziddiyatlar — shu qo'llanmadagi qaror

| Masala | Qaror |
|---|---|
| Sayt domeni: `{slug}.tahsinerp.uz` (Site README) yoki `{slug}.sayt.tahsinerp.uz` (server rejasi) | **`sayt.tahsinerp.uz`** — aks holda ERP firmalari subdomeni bilan to'qnashadi. `Site__BaseDomain` va ERP `SiteBaseDomain` = `sayt.<domen>`. |
| SQL instance: skriptlar `localhost`/`.` deb oladi | Express'da **`.\SQLEXPRESS`** — barcha ulanish satrlari va `-Server` parametrlarida. |
| Zaxira papkasi huquqi `NT Service\MSSQLSERVER` | Express'da `NT Service\MSSQL$SQLEXPRESS`. |
| Bot va sayt: faqat Linux (systemd/nginx/Caddy) fayllari bor | Variant A — IIS (7-bo'lim, qo'lda). Linux fayllari variant B uchun. |
| `machineKey` yaratish `DEPLOY-SECURITY.md` da yo'q | 4.2-band. |
| Panel hujjatida AWS (KMS/S3/SES), server rejasida O'zbekiston VPS | O'zbekiston VPS; imzo — `Local` provayder (KMS hali yo'q), zaxira — 9-bo'lim. |
| `02_MULTI_TENANT.md` dagi band nomlar ro'yxati eski | Koddagi 17 ta nom (2.1-band) to'g'ri. |
| ERP publish yo'li hujjatlanmagan | ERP — `C:\inetpub\tahsinERP`; SQL skriptlar veb-papkadan tashqarida — `C:\tahsinERP\Database` (agent `DatabaseRoot`). |
| Yangi firma bazasiga ERP'ning SQL huquqi hech qayerda berilmasdi | Agent `Sql:AppLogin` — yaratish/tiklashdan keyin `db_owner` (ERP ish vaqtida DDL ham bajaradi). |

## 12. Hali real serverda sinalmagan (birinchi o'rnatishda e'tibor bering)

`Install-PanelIis.ps1` (faqat sintaksis tekshirilgan), admin/my ikki host va `__Host-` cookie'lar,
bot va Mini App real Telegram'da, sayt real domenda, ERP sahifalari HTTPS ostida, Didox/Eskiz/SMTP. Zaxiradan arxivni tiklash amali
panelda yo'q (qo'lda `RESTORE DATABASE`).
