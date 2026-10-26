# Sayt-katalog: firmaning o'z domeni

Holat: 2026-10-25, branch `feature/site-custom-domain`. Loyihalar: `tahsinERP.ControlPanel`, `tahsinERP.Site`,
`tahsinERP.ProvisioningAgent`, `tahsinERP.Provisioning.Contracts`. EF migratsiya: `20261024193658_SiteCustomDomain`.
Testlar: `tahsinERP.ControlPanel.Tests/SiteDomainTests.cs` (17), `tahsinERP.Site.Tests/CustomDomainTests.cs` (+12).

Firma sayti standart manzilda — `<slug>.sayt.tahsinerp.uz`. Nom uzun bo'lsa yoki firma o'z brendida ishlashni xohlasa, panelda
**o'z domeni** ulanadi: `shop.firma.uz` yoki `firma.uz`. Standart manzil ham ishlashda davom etadi (xohlansa — o'z domeniga 301).

## 1. Oqim

```
Panel (firma sahifasi)            Sayt ilovasi (tahsinERP.Site)          Agent (serverda, LocalSystem)
──────────────────────            ─────────────────────────────          ─────────────────────────────
1. Domen kiritiladi ───PUT /admin/api/tenants/{slug}/domain──►  host → tenant (darhol, http)
   format, band emasligi,             {host, redirect:false}
   sayt yoqilganligi
2. DNS tekshiruvi (A/AAAA = server IP)
   to'g'ri ──► ProvisioningJob SiteDomain ──── lease (faqat "site-domain" imkoniyatli agent) ──►
                                                                       3. IIS: http binding host:80
                                                                          win-acme HTTP-01 (selfhosting)
                                                                          → sertifikat WebHosting, https SNI binding
4. Job tugadi → Active ─PUT …/domain {host, redirect}──►  301: <slug>.sayt… → https://host (tanlangan bo'lsa)
```

Olib tashlash: panel saytdan domenni **darhol** uzadi (`host: null`), so'ng agent job — win-acme yangilanishini bekor qiladi,
IIS https/http binding'larini, http.sys `sslcert` yozuvini va sertifikatni (`FriendlyName` = `tahsin-site-<host> @ sana`) o'chiradi.
Domen almashtirilsa eski host shu jobning `SiteDomainRemove` ro'yxatiga tushadi (avval tozalash, keyin yangisini ulash).

## 2. Panel (`Services/SiteDomains.cs`, firma sahifasi → «Telegram bot va sayt-katalog» → «O'z domeni»)

- **Kiritish/o'zgartirish:** `SiteDomainRules.Normalize` (kichik harf, oxirgi nuqtasiz, IDN → punycode; sxema, yo'l, port, IP, bitta
  bo'lak — rad). Bizning domen va subdomenlari (`ControlPanel:Provisioning:BaseDomain`, sayt bazaviy domeni) band. Domen panel bo'yicha
  noyob (`SiteDomains.Host` unique, firmaga bitta — `TenantId` unique). Sayt-katalog yoqilgan bo'lishi shart.
- **«Ulangach … yo'naltirish (301)»** — faqat holat `Active` bo'lganda saytga yuboriladi (sertifikatsiz domenga yo'naltirib qo'ymaslik uchun).
- **DNS tekshiruvi:** domenning *barcha* A/AAAA manzillari kutilgan manzillar ichida bo'lishi kerak (Let's Encrypt AAAA bo'lsa IPv6'ni
  afzal ko'radi — begona AAAA ham xato). Kutilgan manzil — `ControlPanel__Apps__SiteServerIp` (vergul bilan bir nechta), bo'sh bo'lsa
  `<slug>.sayt.<domen>` ning DNS manzillari. CNAME (`shop → <slug>.sayt.tahsinerp.uz`) ham shu manzillarga ochiladi — alohida tekshirilmaydi.
  Saqlashda tekshiruv avtomatik; DNS keyin sozlansa — «DNS'ni tekshirish va ulash».
- **Holatlar:** `PendingDns` → `Binding` (agent job) → `Active`; `Failed` (agent xatosi — matn sahifada, «Tekshirish» qayta job qo'yadi);
  `Removing` (tozalash jobi; xato bo'lsa «Olib tashlashni qayta urinish»). Job bekor qilinsa (Provisioning → Bekor qilish) — `Failed`.
- Firmada faol job bo'lsa (baza yaratish, to'xtatish …) domen amali rad etiladi — bitta tenantda bitta faol job qoidasi.
- **Audit:** `tenant.site-domain-set`, `tenant.site-domain-check`, `tenant.site-domain-remove`, job uchun `provisioning.*`.
- Sayt ilovasidagi qiymat paneldagidan farq qilsa, sahifada ogohlantirish chiqadi — «Saqlash» qayta yuboradi.

## 3. Sayt ilovasi (`tahsinERP.Site`)

- `SiteTenants.CustomDomain` (noyob filtrlangan indeks) va `RedirectToDomain` — `Schema.sql` ishga tushishda o'zi qo'shadi.
- `PUT /admin/api/tenants/{slug}/domain` `{host, redirect}` — boshqa tenantdagi domen va `*.{BaseDomain}` rad etiladi; host xaritasi
  darhol yangilanadi. `GET /admin/api/tenants/{slug}` javobida `domain`, `redirectToDomain`.
- Host → tenant: panel ulagan domen ERP sozlamasidagi (`Site` moduli, nusxadagi `domains`) bir xil hostdan ustun; `www.<domen>` ham topiladi.
- `TenantMiddleware.DomainRedirect`: `RedirectToDomain` bo'lsa boshqa hostlar → `https://<domen>`; o'z domenining http so'rovi → https
  (`Site__RedirectHttpToHttps`, standart true). `/.well-known/` tegilmaydi. Kanonik URL/sitemap — `PrimaryHost` (yo'naltirish yoqilsa o'z domeni).

## 4. Agent (`SiteDomainOps.cs`, `JobKind.SiteDomain`)

Domen Cloudflare'da emas, shuning uchun wildcard (DNS-01) emas — **HTTP-01**. win-acme `selfhosting` validatsiyasi http.sys orqali
`http://+:80/.well-known/acme-challenge/` ni o'zi beradi — IIS ham http.sys'da, bir-biriga xalal bermaydi; IIS'da domen uchun alohida sayt
yoki fayl kerak emas.

Bind (idempotent):
1. `appcmd list site tahsinERP.Site /text:bindings` → `http/*:80:<host>` yo'q bo'lsa qo'shiladi (sayt http → https yo'naltiradi).
2. `wacs.exe --source manual --host <host> --friendlyname tahsin-site-<host> --validation selfhosting --store certificatestore
   --certificatestore WebHosting --installation iis --installationsiteid <id> --accepttos [--emailaddress …]` — win-acme sertifikat oladi,
   `*:443:<host>` SNI binding qo'yadi va yangilanishni o'zining rejalashtirilgan vazifasiga qo'shadi (~60 kunda, xuddi wildcard kabi).
3. `https/*:443:<host>` paydo bo'lganini tekshiradi. Xato bo'lsa job `Failed`, win-acme chiqishining oxiri panelda ko'rinadi.

Unbind: `wacs --cancel --friendlyname tahsin-site-<host>` → appcmd `/-bindings` (https, http) → `netsh http delete sslcert hostnameport=<host>:443`
→ `LocalMachine\WebHosting` dan FriendlyName'i aniq mos sertifikatlar (wildcard'larga tegmaydi).

Moslik: agent lease so'rovida `capabilities: ["site-domain"]` yuboradi; panel `SiteDomain` job'ni faqat shunday agentga beradi —
eski agent (yangilanmagan) uni ololmaydi, job navbatda kutadi. `SiteDomains:Enabled=false` — imkoniyat e'lon qilinmaydi.

Sozlama (`appsettings.json` → `SiteDomains`, env `TAHSIN_AGENT_SiteDomains__*`):

| Kalit | Standart |
|---|---|
| `Enabled` | `true` |
| `WacsExe` | `C:\tools\win-acme\wacs.exe` |
| `IisSite` | `tahsinERP.Site` |
| `CertificateStore` | `WebHosting` |
| `Email` | bo'sh (ACME hisobi wildcard o'rnatishda allaqachon ochilgan) |
| `TimeoutMinutes` | `10` |

## 5. Prod'ga o'rnatish (Lightsail Windows, bir marta)

Kod relizi odatdagidek: `main` → CI imzolangan reliz → panel «Yangilanishlar». Panel migratsiyasi Updater `migrate` bilan, sayt sxemasi
ilova ishga tushganda o'zi qo'llanadi. Agent ham shu reliz bilan yangilanadi. Qo'shimcha — serverda bir marta (Administrator PowerShell):

1. **80-port ochiq** (HTTP-01 shu portda): Lightsail → Networking → IPv4 firewall'da `HTTP 80` bor-yo'qligini tekshiring (HTTP→HTTPS
   uchun odatda bor). Windows Firewall: `Get-NetFirewallRule -DisplayGroup "World Wide Web Services (HTTP)" | Select DisplayName,Enabled`.
2. **Panelga server IP** (DNS tekshiruvi shu manzilga solishtiradi):

   ```powershell
   $appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
   & $appcmd set config -section:system.applicationHost/applicationPools "/+[name='tahsinERP.ControlPanel'].environmentVariables.[name='ControlPanel__Apps__SiteServerIp',value='63.183.64.107']" /commit:apphost
   & $appcmd recycle apppool /apppool.name:tahsinERP.ControlPanel
   ```

   (yoki `Install-BotSite.ps1 ... -KeepSecrets -SiteServerIp 63.183.64.107` — paket yonida bo'lsa).
3. **win-acme** `C:\tools\win-acme\wacs.exe` da bor (wildcard o'rnatishda). Agent LocalSystem — win-acme sozlamasi va ACME hisobi
   `%ProgramData%\win-acme` da umumiy. Tekshiruv: `C:\tools\win-acme\wacs.exe --list` (wildcard yangilanishlari ko'rinadi).
4. Reliz o'rnatilgach panel → Serverlar → server: agent versiyasi yangilanganini ko'ring.
5. **Sinov:** o'zingizning sinov domeningizda (masalan `test.<sizning domen>`) A yozuv → `63.183.64.107` (Cloudflare bo'lsa *DNS only*),
   panelda sayti yoqilgan firmaga ulang → holat `Active` → `https://test.<domen>/health` va bosh sahifa.
   Olib tashlang → `wacs.exe --list` da `tahsin-site-test...` qolmaganini, IIS'da binding yo'qligini tekshiring.

### Mijozga ko'rsatma (panel sahifasida ham bor)

- Subdomen (`shop.firma.uz`): `CNAME shop → <slug>.sayt.tahsinerp.uz` **yoki** `A shop → 63.183.64.107`.
- Asosiy domen (`firma.uz`): faqat `A @ → 63.183.64.107` (apex'da CNAME bo'lmaydi). `www.firma.uz` alohida domen — kerak bo'lsa
  uni ulang (hozircha firmaga bitta domen).
- Shu nom uchun boshqa A/AAAA yozuvlar o'chirilsin; Cloudflare proxy (to'q sariq bulut) o'chiq bo'lsin; domenda CAA bo'lsa
  `letsencrypt.org` ruxsat etilsin.

### Qo'lda yo'l (agent ishlamasa yoki tenant arxivlangan)

```powershell
# ulash
cd C:\tools\win-acme
& "$env:windir\System32\inetsrv\appcmd.exe" set site tahsinERP.Site "/+bindings.[protocol='http',bindingInformation='*:80:shop.firma.uz']"
.\wacs.exe --source manual --host shop.firma.uz --friendlyname tahsin-site-shop.firma.uz --validation selfhosting --store certificatestore --certificatestore WebHosting --installation iis --installationsiteid (Get-Website tahsinERP.Site).id --accepttos
# saytga domenni yetkazish: panel sahifasidagi «Saqlash» (yoki /admin/api PUT .../domain)

# tozalash
.\wacs.exe --cancel --friendlyname tahsin-site-shop.firma.uz
& "$env:windir\System32\inetsrv\appcmd.exe" set site tahsinERP.Site "/-bindings.[protocol='https',bindingInformation='*:443:shop.firma.uz']"
& "$env:windir\System32\inetsrv\appcmd.exe" set site tahsinERP.Site "/-bindings.[protocol='http',bindingInformation='*:80:shop.firma.uz']"
netsh http delete sslcert hostnameport=shop.firma.uz:443
Get-ChildItem Cert:\LocalMachine\WebHosting | Where-Object FriendlyName -like 'tahsin-site-shop.firma.uz @*' | Remove-Item
```

Arxivlangan/o'chirilgan firmaning domeni panelda olib tashlansa agent jobi qo'yilmaydi (agent yo'q) — server tomonini shu buyruqlar bilan tozalang.

## 6. Lokal sinov

```powershell
# sayt ilovasi: bazasi lokal SQL'da, http yo'naltirishsiz
$env:ConnectionStrings__Site = "Server=.;Database=tahsin_site_dev;Trusted_Connection=True;TrustServerCertificate=True"
$env:Site__MasterKey = "<kamida 32 belgi>"; $env:Site__BaseDomain = "sayt.localtest.me"; $env:Site__RedirectHttpToHttps = "false"
dotnet run --project tahsinERP.Site -- tenant add firma https://firma.tahsinerp.uz/   # yoki snapshot import
dotnet run --project tahsinERP.Site --urls http://localhost:5080
# domen: Authorization: Bearer <SiteTenantAdmin.DeriveAdminKey(master)>
#   PUT http://localhost:5080/admin/api/tenants/firma/domain  {"host":"shop.firma.test","redirect":false}
curl.exe -H "Host: shop.firma.test" http://localhost:5080/uz/
curl.exe -H "Host: firma.sayt.localtest.me" http://localhost:5080/uz/
```

Panelda: `ControlPanel:Apps:SiteUrl=http://api.sayt.localtest.me:5080`, `AdminKey`, `SiteServerIp=127.0.0.1`; sinov domeni uchun
`hosts` faylida `127.0.0.1 shop.firma.test`. Agent jobi lokal IIS'siz `Failed` bo'ladi — bu kutilgan (job testlari soxta runner bilan).

## 7. Cheklovlar va keyingi qadamlar

- Firmaga bitta domen; `www` + apex juftligi uchun ikkinchi domen (yoki apex → www yo'naltirish) — keyin.
- Sertifikat yangilanishi win-acme vazifasida; muvaffaqiyatsiz yangilanish panelga hali kelmaydi (win-acme o'z logi/email'i) —
  keyin agent inventariga `wacs --list` holatini qo'shish mumkin.
- DNS tekshiruvi panel serverining DNS keshidan; yangi yozuv tarqalguncha bir necha daqiqa «topilmadi» bo'lishi normal.
