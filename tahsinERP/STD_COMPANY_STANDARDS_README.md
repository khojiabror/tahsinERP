# Korxona Standartlari (STD_COMPANY_STANDARDS) Moduli

## Umumiy ma'lumot

Bu modul korxona standartlarini boshqarish uchun yaratilgan. Foydalanuvchilar Word hujjatlarini inline editor orqali tahrirlash, versiyalarni boshqarish va rasmlarni qo'shish imkoniyatiga ega.

## Xususiyatlar

### 1. CRUD Operatsiyalari
- **Index**: Barcha standartlarni ro'yxatini ko'rsatish (DataTable bilan)
- **Create**: Yangi standart yaratish (template asosida)
- **Edit**: Mavjud standartni tahrirlash (versiya avtomatik oshadi)
- **Details**: Standart batafsil ma'lumotlari va versiya tarixi
- **Delete**: Standartni o'chirish

### 2. Word Document Editor
- **OnlyOffice Document Server** integratsiyasi
- Inline Word hujjat tahrirlash
- Matn orasiga rasmlar qo'shish
- Template fayl asosida yangi hujjat yaratish

### 3. Versiya Boshqaruvi
- Har safar tahrirlashda versiya avtomatik oshadi (v1.00 → v1.01)
- Barcha versiyalar saqlanadi va ko'rish mumkin
- Eski versiyalarni yuklab olish imkoniyati

### 4. Rasm Boshqaruvi
- Word hujjatdan rasmlarni avtomatik ajratib olish
- Rasmlarni alohida saqlash va ko'rsatish
- Details sahifada rasmlarni galereya ko'rinishida ko'rsatish
- Rasmlarni kattalashtirish va yuklab olish

## O'rnatish

### 1. Database Migration

Avval database strukturasini yangilang:

```sql
-- 1. VersionNo ni decimal qilish
-- Database_Migrations/STD_COMPANY_STANDARDS_VersionNo_Update.sql faylini bajarish

-- 2. Permission modullarni yaratish
-- Database_Migrations/STDCompanyStandards_PermissionModules.sql faylini bajarish
```

### 2. Template Fayl

`App_Data/Templates/CompanyStandardTemplate.docx` faylini yarating:
- Microsoft Word'da korxona standartlari uchun template yarating
- Faylni `App_Data/Templates/` papkasiga qo'ying
- Agar template mavjud bo'lmasa, bo'sh hujjat yaratiladi

### 3. OnlyOffice Document Server (Ixtiyoriy)

Agar inline Word editor ishlatmoqchi bo'lsangiz:

1. OnlyOffice Document Server o'rnating
2. `Web.config` faylida URL ni sozlang:
```xml
<add key="OnlyOfficeDocumentServerUrl" value="http://localhost:8080" />
```

Agar OnlyOffice sozlanmagan bo'lsa, oddiy file upload ishlatiladi.

### 4. Permissionlar

Permission modullar yaratilgandan keyin:
1. Admin Panel > Rollar > Permissions ga kiring
2. Har bir rol uchun huquqlarni sozlang:
   - **ViewPermit**: Index, Details, DownloadFile, GetTemplate, GetImage
   - **ChangePermit**: Create, Edit, Delete, SaveDocument, UploadImage

## Foydalanish

### Yangi Standart Yaratish

1. **STDCompanyStandards/Index** sahifasiga kiring
2. "Yangi standart qo'shish" tugmasini bosing
3. Standart kodi, soha va eslatma kiriting
4. Word hujjatini tahrirlang (OnlyOffice yoki file upload)
5. Matn orasiga rasmlar qo'shing (agar OnlyOffice ishlatilsa)
6. "Saqlash" tugmasini bosing

### Standartni Tahrirlash

1. Standart ro'yxatidan tahrirlash kerak bo'lgan standartni tanlang
2. "Tahrirlash" tugmasini bosing
3. Ma'lumotlarni o'zgartiring
4. Versiya avtomatik oshadi (v1.00 → v1.01)
5. "Saqlash" tugmasini bosing

### Versiya Tarixini Ko'rish

1. Standartning Details sahifasiga kiring
2. "Versiya tarixi" bo'limida barcha versiyalarni ko'ring
3. Kerakli versiyani yuklab oling

### Rasmlarni Ko'rish

1. Standartning Details sahifasiga kiring
2. O'ng tomonda "Hujjatdagi rasmlar" bo'limida rasmlarni ko'ring
3. Rasmni bosib kattalashtiring
4. Rasmni yuklab oling

## Texnik Detallar

### Fayl Strukturasi

```
Controllers/
  └── STDCompanyStandardsController.cs
ViewModels/
  └── STDCompanyStandardsViewModel.cs
Views/
  └── STDCompanyStandards/
      ├── Index.cshtml
      ├── Create.cshtml
      ├── Edit.cshtml
      ├── Details.cshtml
      └── Delete.cshtml
Services/
  └── WordDocumentImageService.cs
Database_Migrations/
  ├── STD_COMPANY_STANDARDS_VersionNo_Update.sql
  └── STDCompanyStandards_PermissionModules.sql
App_Data/
  └── Templates/
      └── CompanyStandardTemplate.docx
```

### Database Jadvali

- **STD_COMPANY_STANDARDS**: Asosiy standart ma'lumotlari
- **DOC_DOCUMENTS**: Hujjat ma'lumotlari
- **DOC_FILES**: Word fayllar va rasmlar

### API Endpoints

- `GET /STDCompanyStandards/Index` - Ro'yxat
- `GET /STDCompanyStandards/Create` - Yaratish formasi
- `POST /STDCompanyStandards/Create` - Yaratish
- `GET /STDCompanyStandards/Edit/{id}` - Tahrirlash formasi
- `POST /STDCompanyStandards/Edit` - Tahrirlash
- `GET /STDCompanyStandards/Details/{id}` - Batafsil
- `GET /STDCompanyStandards/Delete/{id}` - O'chirish formasi
- `POST /STDCompanyStandards/Delete` - O'chirish
- `GET /STDCompanyStandards/DownloadFile/{fileId}` - Fayl yuklash
- `GET /STDCompanyStandards/GetImage/{fileId}` - Rasm ko'rsatish
- `GET /STDCompanyStandards/GetTemplate` - Template olish
- `POST /STDCompanyStandards/UploadImage` - Rasm yuklash (OnlyOffice)
- `POST /STDCompanyStandards/SaveDocument` - Document saqlash (OnlyOffice callback)

## Muammolarni Hal Qilish

### OnlyOffice ishlamayapti

- OnlyOffice Document Server URL ni tekshiring
- Server ishlayotganligini tekshiring
- Browser console'da xatoliklarni ko'ring
- Fallback: Oddiy file upload ishlatiladi

### Rasmlar ko'rinmayapti

- Word hujjatda rasmlar mavjudligini tekshiring
- Database'da DOC_FILES jadvalida rasm fayllarini tekshiring
- GetImage action ishlayotganligini tekshiring

### Versiya o'zgarmayapti

- Database'da VersionNo decimal(5,2) formatida ekanligini tekshiring
- Migration faylini bajarganingizni tekshiring

## Qo'shimcha Ma'lumotlar

- DocumentFormat.OpenXml paketi Word hujjatlarni qayta ishlash uchun ishlatiladi
- Rasmlar DOC_FILES jadvalida saqlanadi
- Har bir versiya uchun alohida fayl yaratiladi
- Eski versiyalar o'chirilmaydi, faqat yangi versiya yaratiladi

