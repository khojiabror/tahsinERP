# ERP ko'p-tenant rejimi (0-bosqich)

Bitta IIS ilova, har tashkilotga alohida baza. Tenant so'rov host'idan aniqlanadi: `<slug>.<bazaviy domen>`.

## Sozlama (serverda, git'ga kirmaydi)

| Nima | Qayerda |
|---|---|
| Tenant reestri | `TAHSINERP_TENANTS_FILE` env yoki `%ProgramData%\tahsinERP\tenants.json` |
| Bazaviy domen | `TAHSINERP_BASE_DOMAIN` env → fayldagi `baseDomain` → Web.config `TenantBaseDomain` |
| SQL server / login | avvalgidek `DbConfig` (`TAHSINERP_DB` env, `db.connection` fayl yoki shifrlangan blob); tenant uchun faqat `Initial Catalog` almashtiriladi |

```json
{
  "baseDomain": "tahsinerp.uz",
  "tenants": [
    { "slug": "firma1", "database": "DB_firma1", "companyId": 1 },
    { "slug": "firma2", "database": "DB_firma2", "hosts": ["erp.firma2.uz"] },
    { "slug": "firma3", "database": "DB_firma3", "connectionString": "Data Source=srv2;...", "enabled": false }
  ]
}
```

- `slug`: `a-z0-9-`, bitta daraja. `database`: `A-Za-z0-9_-.`.
- `companyId` ixtiyoriy (kalitdagi CompanyID); berilmasa Web.config `companyID`.
- `hosts` ixtiyoriy — mijozning o'z domeni.
- `connectionString` ixtiyoriy — tenant boshqa SQL serverda bo'lsa (`Servers` jadvali).
- Fayl 15 soniyada bir marta tekshiriladi: panel yangi tenant qo'shsa, ilovani qayta ishga tushirish shart emas.

## Xatti-harakat

| Holat | Natija |
|---|---|
| Fayl yo'q yoki `tenants` bo'sh | **Bitta-baza rejimi** — hamma narsa avvalgidek |
| Host reestrda yo'q | 404 "Tashkilot topilmadi" |
| Fayl buzilgan (birinchi o'qishda) | 503 — umumiy bazaga tushib qolmaydi |
| Fayl ishlab turganda buzilsa / o'chirilsa | oxirgi yaroqli reestr saqlanadi, logga yoziladi |

## Kod qoidalari

- Barcha `new DBTHSNEntities()` → `DbConfig.GetEntityConnectionString()` → `TenantContext.Current`.
- `TenantContext.Current` manbalari: `TenantContext.Use(t)` doirasi (AsyncLocal) → `HttpContext.Items` (BeginRequest).
  Ko'p-tenant rejimida tenant aniqlanmasa — `TenantNotResolvedException` (jimgina noto'g'ri bazaga ulanmaydi).
- **Fon ishi** (`Task.Run`, `QueueBackgroundWorkItem`, timer):
  ```csharp
  var tenant = TenantContext.Current;
  HostingEnvironment.QueueBackgroundWorkItem(ct => { using (TenantContext.Use(tenant)) { ... } });
  ```
- **Har qanday umumiy kesh** (MemoryCache, HttpRuntime.Cache, statik lug'at) kaliti `TenantContext.CacheKey(key)` yoki
  `TenantContext.CacheKeyPrefix` orqali. Hozir ulangan: `RoleHelper`, `LicenseService` (holat keshi, LICENSE_STATE bayrog'i),
  `LoginThrottle`, `DlocFifoSchema`.
- **Auth cookie** tenant'ga bog'langan: `FormsAuthenticationTicket.UserData` oxirida `\u001Ftenant=<slug>`
  (`TenantHttp.BindUserData`). Boshqa tenant cookie'si `PostAuthenticateRequest` da anonim qilinadi.
  To'liq ismni UserData'dan o'qishda `TenantHttp.StripTenant` ishlating.

## Ma'lum cheklovlar
- Session (InProc) tenant bo'yicha ajratilmagan: session cookie host'ga bog'langan, lekin qo'lda ko'chirilsa boshqa
  tenant session'i ochiladi. Session'da faqat UI qulayliklari saqlansin, huquq emas.
- `optimization/core` dagi `ReferenceDataCache` va `DbConfig` keshi merge'da shu qoidaga moslashtirilishi kerak
  (qarang: hisobot / side-session eslatmasi).
