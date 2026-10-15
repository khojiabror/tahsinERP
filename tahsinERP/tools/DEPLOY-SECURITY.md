# DB ulanish xavfsizligi — qo'llanma

Connection string (server IP, user, parol) endi **web.config'da OCHIQ turmaydi**.
U `Configuration/DbConfig.cs` ichida **AES bilan shifrlangan** holda turadi va
kompilyatsiyada `tahsinERP.dll` ichiga tushadi. Publish papkasida ochiq matn qolmaydi.

## Qaysi fayl nima uchun

| Fayl | Vazifasi |
|------|----------|
| `Configuration/DbConfig.cs` | Shifrlangan connection string + ochish (decrypt) mantiqi. `#if DEBUG` = lokal, `#else` = server. |
| `tools/Encrypt-ConnString.ps1` | Kredensial o'zgarganda yangi shifrlangan blob yasab beradi. |
| `Web.Release.config` | Publish (Release) paytida web.config'dan connection string'ni butunlay o'chiradi. |
| `web.config` → `connectionStrings > DBTHSNEntities` | Faqat EF dizayner (EDMX) uchun localhost. Runtime undan foydalanmaydi. |

## Kredensial o'zgarganda (server IP / user / parol yangilansa)

1. Yangi blob yasang (PowerShell):
   ```powershell
   cd tahsinERP\tools
   .\Encrypt-ConnString.ps1 -Verify -Plain "data source=192.168.100.200;initial catalog=DBTHSN-IPACK;user id=sa;password=YangiParol;trustservercertificate=True;MultipleActiveResultSets=True;App=EntityFramework"
   ```
2. Chiqqan Base64 satrni `Configuration/DbConfig.cs` ichidagi tegishli const ga qo'ying:
   - **Server uchun** → `#else` bo'limidagi `EncryptedSql`
   - **Lokal uchun** → `#if DEBUG` bo'limidagi `EncryptedSql`
3. **Rebuild** (Release) → **Publish**. Tamom. web.config'ga tegish shart emas.

## Ustuvor manba: muhit o'zgaruvchisi yoki tashqi fayl (TAVSIYA)

`DbConfig.GetSqlConnectionString()` connection string'ni quyidagi tartibda qidiradi:

1. **`TAHSINERP_DB`** muhit o'zgaruvchisi — ochiq SQL connection string.
2. **Tashqi fayl** — `TAHSINERP_DB_FILE` env'da ko'rsatilgan yo'l, bo'lmasa
   `%ProgramData%\tahsinERP\db.connection` (`C:\ProgramData\tahsinERP\db.connection`).
   Fayl ichida bitta qator: connection string. Webroot'dan tashqarida, git'ga kirmaydi.
3. Hech biri bo'lmasa — kod ichidagi shifrlangan blob (`#if DEBUG` = localhost,
   `#else` = 192.168.100.200).

Qiymat ilova ishga tushganda bir marta o'qiladi (o'zgartirgandan keyin app pool'ni recycle qiling).

### AWS (Lightsail / EC2 Windows + IIS)

1. SSM Parameter Store'da `SecureString` yarating, masalan `/tahsinerp/prod/db`.
2. Instance roliga `ssm:GetParameter` (+ KMS decrypt) ruxsatini bering.
3. Deploy skriptida qiymatni olib, faqat app pool uchun env sifatida yozing:
   ```powershell
   $cs = (Get-SSMParameter -Name /tahsinerp/prod/db -WithDecryption $true).Value
   & "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.applicationHost/applicationPools `
     "/-[name='tahsinERP'].environmentVariables.[name='TAHSINERP_DB']" /commit:apphost 2>$null
   & "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.applicationHost/applicationPools `
     "/+[name='tahsinERP'].environmentVariables.[name='TAHSINERP_DB',value='$cs']" /commit:apphost
   & "$env:windir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:tahsinERP
   ```
   (IIS 10+. Muqobil: qiymatni `C:\ProgramData\tahsinERP\db.connection` ga yozib,
   faylga faqat `IIS AppPool\tahsinERP` va Administrators o'qish huquqini qoldiring.)
4. Mashina darajasidagi (System) env o'zgaruvchisidan foydalanmang — boshqa jarayonlar ham ko'radi.

### Fayl usuli (oddiy Windows server)

```powershell
New-Item -ItemType Directory -Force C:\ProgramData\tahsinERP | Out-Null
Set-Content -Encoding utf8 C:\ProgramData\tahsinERP\db.connection "data source=...;initial catalog=...;user id=...;password=...;MultipleActiveResultSets=True;App=EntityFramework"
icacls C:\ProgramData\tahsinERP\db.connection /inheritance:r /grant:r "Administrators:F" "IIS AppPool\tahsinERP:R"
```

## Muhim eslatmalar

- `DbConfig.cs` va `Encrypt-ConnString.ps1` dagi `KeyPhrase`/`IvPhrase` **bir xil**
  bo'lishi shart. Ikkalasini birga o'zgartiring (o'zgartirsangiz, barcha blob'larni
  qayta yasashingiz kerak).
- **EDMX qayta generatsiya** qilinsa `Models/DBTHSNModel.Context.cs` konstruktori
  eski holiga (`base("name=DBTHSNEntities")`) qaytadi. Uni qaytadan
  `base(tahsinERP.Configuration.DbConfig.GetEntityConnectionString())` ga o'zgartiring.

## Xavfsizlik darajasi (halol baho)

- Kalit ham kod ichida bo'lgani uchun bu **obfuscation** darajasidagi himoya.
- ✅ To'xtatadi: serverga kirib papka/web.config titkilaydigan odam — ochiq parol topmaydi.
- ❌ To'xtatmaydi: `tahsinERP.dll`ni ataylab decompile (ILSpy/dnSpy) qiladigan odam.
- Shuning uchun **production'da env/fayl usulidan foydalaning** — unda parol DLL ichida
  bo'lmaydi va kodni bilish yetarli emas. Blob faqat zaxira (fallback).
- `sa` o'rniga faqat shu bazaga huquqli alohida SQL login ishlating.
