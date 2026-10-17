using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service to generate User Manual chapters
    /// </summary>
    public static class UserManualProvider
    {
        // Kontent 100% statik — avval har so'rovda (GetChapter'da ham butun qo'llanma) ~1400 qatorlik
        // StringBuilder qayta qurilardi (MAN-P01). Natija umumiy: chaqiruvchilar uni o'zgartirmasin.
        private static readonly Lazy<UserManualPageViewModel> Manual = new Lazy<UserManualPageViewModel>(Build);

        public static UserManualPageViewModel GetManual()
        {
            return Manual.Value;
        }

        private static UserManualPageViewModel Build()
        {
            var chapters = new List<UserManualChapterViewModel>();

            // 1. Kirish (Introduction)
            chapters.Add(CreateIntroductionChapter());

            // 2. Foydalanuvchilar, rollar va ruxsatlar (VERY DETAILED)
            chapters.Add(CreatePermissionsChapter());

            // 3. CRUD va modul strukturalari bo'yicha qo'llanma
            chapters.Add(CreateCrudPatternsChapter());

            // 4. Sales / Sotuvlar
            chapters.Add(CreateSalesChapter());

            // 5. Purchase / Xarid
            chapters.Add(CreatePurchaseChapter());

            // 6. Warehouse / Omborxona
            chapters.Add(CreateWarehouseChapter());

            // 7. Production / Ishlab chiqarish
            chapters.Add(CreateProductionChapter());

            // 8. Finance / Moliya
            chapters.Add(CreateFinanceChapter());

            // 9. HR / Kadrlar
            chapters.Add(CreateHRChapter());

            // 10. Settings / Sozlamalar
            chapters.Add(CreateSettingsChapter());

            // 11. Engineering / Muhandislik
            chapters.Add(CreateEngineeringChapter());

            // 12. Logistics / Logistika
            chapters.Add(CreateLogisticsChapter());

            // 13. Tasks / Vazifalar
            chapters.Add(CreateTasksChapter());

            // Ro'yxat allaqachon kerakli tartibda; Order qiymatlari takrorlanadi (CRUD va Sotuv — ikkalasi 3),
            // shuning uchun teng Order'da qo'shilish tartibi saqlanadi (MAN-B01)
            return new UserManualPageViewModel
            {
                Chapters = chapters.Select((c, i) => new { c, i }).OrderBy(x => x.c.Order).ThenBy(x => x.i).Select(x => x.c).ToList(),
                InitialChapterId = chapters.First().Id
            };
        }

        private static UserManualChapterViewModel CreateCrudPatternsChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>CRUD va modul arxitekturasi bo'yicha qo'llanma</h2>");
            content.AppendLine("<p>Ushbu bo'lim tahsinERP tizimidagi barcha modul va CRUD amallari uchun <strong>umumiy qoliplar</strong> va <strong>praktik foydalanish yo'riqnomasi</strong>ni beradi.</p>");

            content.AppendLine("<h3>1. Umumiy arxitektura</h3>");
            content.AppendLine("<p>Tizim klassik ASP.NET MVC arxitekturasida qurilgan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Models</strong> – EF modeli (<code>DBTHSNEntities</code>) va jadval klasslari (masalan, <code>FIN_ACCOUNTS</code>, <code>PRODUCTS</code>, <code>EXPENSES</code>).</li>");
            content.AppendLine("<li><strong>Controllers</strong> – har bir modul bo'yicha papkalar va controllerlar (masalan, <code>Finance</code>, <code>SRM</code>, <code>MES</code>, <code>HR</code>).</li>");
            content.AppendLine("<li><strong>Views</strong> – har bir controller uchun mos papkada <code>.cshtml</code> fayllar (CRUD ko'rinishlari).</li>");
            content.AppendLine("<li><strong>ViewModels</strong> – murakkab ekranga maxsus modellar (hisobotlar, dashboardlar).</li>");
            content.AppendLine("<li><strong>Services</strong> – umumiy biznes logika (masalan, <code>UnitConversionService</code>, <code>UserManualProvider</code>).</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>2. Standart CRUD qolipi (hamma modul uchun)</h3>");
            content.AppendLine("<p>Tizimdagi deyarli barcha controllerlar bir xil CRUD pattern bo'yicha ishlaydi. Quyidagi misol <code>FinAccountsController</code> asosida keltirilgan, lekin qolip butun tizim bo'ylab bir xil:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Index (GET)</strong> – ro'yxat, qidirish, filtr, sort, pagination.");
            content.AppendLine("<li><strong>Details (GET)</strong> – tanlangan yozuvning to'liq ma'lumoti.</li>");
            content.AppendLine("<li><strong>Create (GET/POST)</strong> – yangi yozuv yaratish.</li>");
            content.AppendLine("<li><strong>Edit (GET/POST)</strong> – mavjud yozuvni tahrirlash.</li>");
            content.AppendLine("<li><strong>Delete (GET/POST)</strong> – odatda <em>soft delete</em> (IsActive/IsDeleted bayrog'i bilan).</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.1. Index action – ro'yxat, qidirish va filtrlash</h4>");
            content.AppendLine("<p>Misol: <code>Finance/FinAccounts/Index</code>.</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Ruxsat: <code>RoleHelper.IsViewPermitted(User.Identity.Name, \"FinAccounts\")</code>.</li>");
            content.AppendLine("<li>Query: <code>db.FIN_ACCOUNTS.Where(a =&gt; a.IsActive)</code> ustida <strong>search</strong>, <strong>filter</strong>, <strong>sort</strong>, <strong>pagination</strong>.</li>");
            content.AppendLine("<li>Natija: ro'yxat view (<code>Views/FinAccounts/Index.cshtml</code>) va <code>ViewBag</code> orqali filter dropdownlar.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.2. Details action – bitta yozuv kartasi</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>URL: <code>/Finance/FinAccounts/Details/{id}</code>.</li>");
            content.AppendLine("<li>Null va mavjud emasligi uchun <code>BadRequest</code> va <code>HttpNotFound</code> qaytariladi.</li>");
            content.AppendLine("<li><code>Include(...)</code> bilan navigatsion propertylar (valyuta, foydalanuvchi va boshqalar) yuklanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.3. Create (GET/POST) – yangi yozuv yaratish</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>GET</strong> – formani ko'rsatish, <code>ViewBag</code> orqali dropdownlar (<code>CURRENCIES</code>, <code>FIN_BALANCE_CATEGORIES</code> va hokazo) to'ldiriladi.</li>");
            content.AppendLine("<li><strong>POST</strong> – <code>ModelState.IsValid</code> va biznes qoidalar (masalan, <code>Code</code> unikal bo'lishi) tekshiriladi.</li>");
            content.AppendLine("<li>Audit maydonlari: <code>CreatedByUserID = UserHelper.GetUserId(User.Identity.Name)</code>, <code>CreatedDT = DateTime.Now</code>.</li>");
            content.AppendLine("<li>Log yozuvi: <code>LogHelper.LogToDatabase(User.Identity.Name, \"FinAccounts\", \"Create\", ...)</code>.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.4. Edit (GET/POST) – mavjud yozuvni tahrirlash</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>GET</strong> – <code>Find(id)</code> orqali yozuv olinadi, dropdownlar to'ldiriladi.</li>");
            content.AppendLine("<li><strong>POST</strong> – <code>Code</code> kabi unikal maydonlar boshqa yozuvlar bilan to'qnashmasligi tekshiriladi.</li>");
            content.AppendLine("<li>Faqat ruxsat berilgan maydonlar yangilanadi (masalan, <code>Name</code>, <code>CurrencyID</code>, <code>IsCash</code> va hokazo).</li>");
            content.AppendLine("<li>Log yozuvi: <code>LogHelper.LogToDatabase(..., \"Edit\", ...)</code>.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.5. Delete – soft delete</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>GET</strong> – foydalanuvchiga ogohlantirish sahifasi ko'rsatiladi, ko'pincha <code>HasTransactions</code> yoki boshqa bog'liqlik tekshiriladi.</li>");
            content.AppendLine("<li><strong>POST (DeleteConfirmed)</strong> – odatda <em>soft delete</em>: <code>IsActive = false</code> yoki <code>IsDeleted = true</code> bayrog'i qo'yiladi.</li>");
            content.AppendLine("<li>Log yozuvi: <code>LogHelper.LogToDatabase(..., \"Delete\", ...)</code>.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>3. Umumiy helper va servislar</h3>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>DBTHSNEntities</strong> – barcha modul uchun yagona EF <code>DbContext</code>. Barcha jadvallar shu yerda.</li>");
            content.AppendLine("<li><strong>RoleHelper</strong> – <code>IsViewPermitted</code> va <code>IsChangePermitted</code> orqali ruxsat tekshiradi.</li>");
            content.AppendLine("<li><strong>UserHelper</strong> – joriy foydalanuvchi ID sini beradi (<code>GetUserId(User.Identity.Name)</code>).</li>");
            content.AppendLine("<li><strong>LogHelper</strong> – CRUD amallarini bazaga log qiladi.</li>");
            content.AppendLine("<li><strong>UnitConversionService</strong> – ishlab chiqarish va ombor hisobotlarida birlik konvertatsiyasi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>4. Modul bo'yicha qisqacha xarita</h3>");
            content.AppendLine("<h4>4.1. Moliya (Finance)</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>FinAccounts</strong> – kassa/bank/boshqa hisoblar CRUD.</li>");
            content.AppendLine("<li><strong>FinTransactions</strong> – moliyaviy tranzaksiyalar CRUD (kirim/chiqim, o'tkazmalar).</li>");
            content.AppendLine("<li><strong>FinBalanceCategories</strong>, <strong>FinCategories</strong> – balans va P&amp;L kategoriyalar.</li>");
            content.AppendLine("<li><strong>FinPlItems</strong>, <strong>FinPlPlans</strong> – P&amp;L moddalar va reja.</li>");
            content.AppendLine("<li><strong>OpeningBalance</strong> – boshlang'ich qoldiqlarni kiritish.</li>");
            content.AppendLine("<li><strong>Shareholder</strong> – aksiyadorlar ro'yxati.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>4.2. Sotuvlar (CRM / Sales)</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Customer</strong> / <strong>CustomerBase</strong> – mijozlar CRUD.</li>");
            content.AppendLine("<li><strong>CompanyOrders</strong> – buyurtmalar (order) CRUD, hisob-faktura va yetkazib berish bilan bog'liq.</li>");
            content.AppendLine("<li><strong>SalesInvoices</strong>, <strong>SalesSF</strong>, <strong>SalesTTNK</strong> – sotuv invoyslari va soliq hujjatlari.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>4.3. Xarid va SRM</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Supplier</strong> / <strong>SupplierBase</strong> – ta'minotchilar CRUД.</li>");
            content.AppendLine("<li><strong>POrders</strong>, <strong>PContracts</strong>, <strong>PInvoices</strong>, <strong>PRequisitions</strong> – xarid buyurtmalari va shartnomalar.</li>");
            content.AppendLine("<li><strong>SRMDashboard</strong> – ta'minot bo'yicha ko'rsatkichlar.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>4.4. Omborxona va logistika</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Warehouse</strong> – omborlar CRUD.</li>");
            content.AppendLine("<li><strong>Incomes</strong>, <strong>Expenses</strong> – ombor kirim/chiqim hujjatlari.</li>");
            content.AppendLine("<li><strong>Inventory</strong>, <strong>InventoryReports</strong> – qoldiq va analitik hisobotlar.</li>");
            content.AppendLine("<li><strong>Forwarder</strong>, <strong>FContract</strong>, <strong>Waybill</strong> – logistika (yuk tashish, yo'l varaqalari).</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>4.5. Ishlab chiqarish va MES</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>ProductPlan</strong>, <strong>ProductProduction</strong>, <strong>MESDashboard</strong> – ishlab chiqarish rejalari va faktlari.</li>");
            content.AppendLine("<li><strong>BOM</strong>, <strong>Norm</strong>, <strong>Tannarx</strong>, <strong>TannarxAnalytics</strong> – retseptlar va tannarx.</li>");
            content.AppendLine("<li><strong>QualityInspection</strong>, <strong>DamagedPart</strong>, <strong>DamagedProduct</strong>, <strong>DefectType</strong> – sifat va brak bilan ishlash.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>4.6. HR va vazifalar</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>HrOrgUnits</strong>, <strong>HrEmployees</strong>, <strong>HrPositions</strong>, <strong>HrSalaryGrades</strong> – tashkiliy struktura va xodimlar.</li>");
            content.AppendLine("<li><strong>HrTimesheets</strong>, <strong>HrPayroll</strong>, <strong>HrPieceRateTimesheets</strong> – tabel va ish haqi.</li>");
            content.AppendLine("<li><strong>Tasks</strong>, <strong>TaskDashboard</strong>, <strong>TaskKanban</strong>, <strong>TaskChangeStatus</strong> – vazifalar boshqaruvi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>5. Lot va DLOC (ombor ichidagi joylar)</h3>");
            content.AppendLine("<p>SRM modulida <strong>DLOC</strong> (ombor joylashuvi), <strong>lot</strong> va <strong>FIFO</strong> chiqim qoidalari joriy qilingan. Batafsil qo'llanma:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><a href=\"/HelpDocs/Download?file=DLOC_FIFO_User_Guide.pdf\" target=\"_blank\"><strong>DLOC va FIFO qo'llanmasi (PDF)</strong></a></li>");
            content.AppendLine("<li><a href=\"/HelpDocs/Download?file=DLOC_FIFO_User_Guide.docx\" target=\"_blank\">Word (.docx)</a> — tahrirlash uchun</li>");
            content.AppendLine("<li>Manba: <code>Docs/DLOC_FIFO_User_Guide.md</code> (versiya 1.0)</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Asosiy ekranlar:</strong> Sozlamalar → Ombor joylashuvlari; Kirimlar (har qator DLOC); Chiqimlar (FIFO); Ombor qoldig'i → Joylar bo'yicha qoldiq.</p>");

            content.AppendLine("<h3>6. Amaliy ishlash tartibi (administrator va power user uchun)</h3>");
            content.AppendLine("<ol>");
            content.AppendLine("<li><strong>Model va jadvalni aniqlash:</strong> kerakli jadval EF modelida (<code>DBTHSNEntities</code>) mavjudligini tekshiring.</li>");
            content.AppendLine("<li><strong>Controller va View papkasini topish:</strong> modulga mos controller (<code>Controllers/Finance</code>, <code>Controllers/SRM</code> va hokazo) va <code>Views</code> ichidagi mos papkani toping.</li>");
            content.AppendLine("<li><strong>CRUD qolipidan foydalanish:</strong> mavjud controllerlardan (masalan, <code>FinAccountsController</code>) nusxa olib yangi modulga moslashtiring.</li>");
            content.AppendLine("<li><strong>Ruxsatni sozlash:</strong> <code>PModule</code> orqali modulni ro'yxatga oling va rol uchun <code>ViewPermit</code> va <code>ChangePermit</code> ni belgilab chiqing.</li>");
            content.AppendLine("<li><strong>Loglarni yoqish:</strong> barcha muhim <code>Create/Edit/Delete</code> amallariga <code>LogHelper.LogToDatabase</code> qo'shilganiga ishonch hosil qiling.</li>");
            content.AppendLine("</ol>");

            return new UserManualChapterViewModel
            {
                Id = "crud-architecture",
                Title = "CRUD va modul arxitekturasi",
                ModuleCode = "CRUD",
                ContentHtml = content.ToString(),
                Order = 3
            };
        }

        private static UserManualChapterViewModel CreateIntroductionChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Kirish</h2>");
            content.AppendLine("<p>Xush kelibsiz! Bu qo'llanma tahsinERP / iPack ERP tizimidan foydalanish bo'yicha batafsil ko'rsatmalar beradi.</p>");
            content.AppendLine("<p>Qo'llanma quyidagi bo'limlarni o'z ichiga oladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Foydalanuvchilar, rollar va ruxsatlar tizimi</li>");
            content.AppendLine("<li>Sotuvlar moduli (CRM)</li>");
            content.AppendLine("<li>Xarid moduli</li>");
            content.AppendLine("<li>Omborxona boshqaruvi</li>");
            content.AppendLine("<li>Ishlab chiqarish rejalashtirish</li>");
            content.AppendLine("<li>Moliya moduli</li>");
            content.AppendLine("<li>Kadrlar boshqaruvi (HR)</li>");
            content.AppendLine("<li>Vazifalar boshqaruvi (Tasks)</li>");
            content.AppendLine("<li>Sozlamalar</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Chap panelda bo'limlar ro'yxatini ko'rasiz. Qidiruv orqali kerakli bo'limni tezda topishingiz mumkin.</p>");

            return new UserManualChapterViewModel
            {
                Id = "introduction",
                Title = "Kirish",
                ModuleCode = "",
                ContentHtml = content.ToString(),
                Order = 1
            };
        }

        private static UserManualChapterViewModel CreatePermissionsChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Foydalanuvchilar, rollar va ruxsatlar (USER–ROLE–PERMISSION)</h2>");
            
            content.AppendLine("<h3>1. Tizimga kirish (Authentication)</h3>");
            content.AppendLine("<p>Tizimdan foydalanish uchun avval tizimga kirishingiz kerak. Login oynasida username va parolingizni kiriting.</p>");
            content.AppendLine("<p><strong>Rasm-1: Login oynasi</strong></p>");
            content.AppendLine("<p>❗ Bu yerda tizimning aniq xulq-atvori bo'yicha qo'shimcha aniqlik kiritish kerak (TODO).</p>");

            content.AppendLine("<h3>2. Foydalanuvchi (User) nima?</h3>");
            content.AppendLine("<p>Foydalanuvchi - bu tizimdan foydalanadigan har bir shaxs. Har bir foydalanuvchi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Benzersiz username (login) ga ega</li>");
            content.AppendLine("<li>Parol orqali himoyalangan</li>");
            content.AppendLine("<li>To'liq ism (FullName) bilan identifikatsiya qilinadi</li>");
            content.AppendLine("<li>Kamida bitta rolga biriktirilgan bo'lishi kerak</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Foydalanuvchilar Admin Panel → Foydalanuvchilar bo'limidan yaratiladi va boshqariladi.</p>");
            content.AppendLine("<p><strong>Rasm-2: Foydalanuvchilar ro'yxati</strong></p>");

            content.AppendLine("<h3>3. Rol (Role) nima?</h3>");
            content.AppendLine("<p>Rol - bu foydalanuvchilarga beriladigan huquqlar to'plami. Masalan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Admin</strong> - barcha huquqlarga ega</li>");
            content.AppendLine("<li><strong>Sales Manager</strong> - sotuvlar bo'limi bilan ishlash huquqi</li>");
            content.AppendLine("<li><strong>Warehouse Manager</strong> - omborxona operatsiyalari huquqi</li>");
            content.AppendLine("<li><strong>Viewer</strong> - faqat ko'rish huquqi</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Hozirgi tizimda har bir foydalanuvchi bir nechta rolga ega bo'lishi mumkin. Lekin asosiy ish jarayonida foydalanuvchining birinchi roli hisobga olinadi.</p>");
            content.AppendLine("<p>Rollar Admin Panel → Rollar bo'limidan yaratiladi va boshqariladi.</p>");
            content.AppendLine("<p><strong>Rasm-3: Rollar ro'yxati</strong></p>");

            content.AppendLine("<h3>4. Permission / Ruxsat nima? PMODULE nimani anglatadi?</h3>");
            content.AppendLine("<p><strong>PERMISSIONMODULE (PMODULE)</strong> - bu tizimdagi har bir funksional modul, sahifa yoki amalni ifodalovchi yozuv.</p>");
            content.AppendLine("<p>Har bir PMODULE quyidagi maydonlarga ega:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Module</strong> - modul nomi (masalan: \"SOrder\", \"POrder\", \"Warehouse\")</li>");
            content.AppendLine("<li><strong>Controller</strong> - controller nomi (masalan: \"SOrderController\", \"POrderController\")</li>");
            content.AppendLine("<li><strong>Action</strong> - action nomi (masalan: \"Index\", \"Create\", \"Edit\")</li>");
            content.AppendLine("<li><strong>Parameter</strong> - qo'shimcha parametr (ixtiyoriy)</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Masalan, \"SOrder\" moduli uchun PMODULE yozuvi quyidagicha bo'lishi mumkin:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Module: \"SOrder\"</li>");
            content.AppendLine("<li>Controller: \"SOrder\"</li>");
            content.AppendLine("<li>Action: \"Index\"</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>PMODULE yozuvlari Admin Panel → PModule bo'limidan boshqariladi.</p>");
            content.AppendLine("<p><strong>Rasm-4: PMODULE ro'yxati</strong></p>");

            content.AppendLine("<h3>5. ViewPermit - Ko'rish huquqi</h3>");
            content.AppendLine("<p><strong>ViewPermit</strong> - bu foydalanuvchining ma'lum bir modul yoki sahifani ko'rish huquqi.</p>");
            content.AppendLine("<p>ViewPermit = true bo'lsa:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Foydalanuvchi sahifani ko'ra oladi</li>");
            content.AppendLine("<li>Ma'lumotlarni o'qiy oladi</li>");
            content.AppendLine("<li>Ro'yxatlarni ko'ra oladi</li>");
            content.AppendLine("<li>Lekin o'zgartirish, yaratish yoki o'chirish huquqi bo'lmasligi mumkin</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Misol:</strong> Agar foydalanuvchining \"SOrder\" moduli uchun faqat ViewPermit = true bo'lsa:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>✅ Sotuv buyurtmalari ro'yxatini ko'ra oladi</li>");
            content.AppendLine("<li>✅ Buyurtma tafsilotlarini ko'ra oladi</li>");
            content.AppendLine("<li>❌ Yangi buyurtma yarata olmaydi</li>");
            content.AppendLine("<li>❌ Mavjud buyurtmani tahrirlay olmaydi</li>");
            content.AppendLine("<li>❌ Buyurtmani o'chira olmaydi</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Kodda ViewPermit tekshiruvi <code>RoleHelper.IsViewPermitted()</code> metod orqali amalga oshiriladi.</p>");

            content.AppendLine("<h3>6. ChangePermit - O'zgartirish huquqi</h3>");
            content.AppendLine("<p><strong>ChangePermit</strong> - bu foydalanuvchining ma'lum bir modul yoki sahifada o'zgartirishlar qilish huquqi.</p>");
            content.AppendLine("<p>ChangePermit = true bo'lsa:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Foydalanuvchi yangi yozuvlar yarata oladi (Create)</li>");
            content.AppendLine("<li>Mavjud yozuvlarni tahrirlay oladi (Edit)</li>");
            content.AppendLine("<li>Yozuvlarni o'chira oladi (Delete)</li>");
            content.AppendLine("<li>Ma'lum amallarni tasdiqlay oladi (Approve, Confirm va hokazo)</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Muhim:</strong> ChangePermit ishlashi uchun ViewPermit ham true bo'lishi kerak. Agar ViewPermit = false bo'lsa, ChangePermit ham ishlamaydi.</p>");
            content.AppendLine("<p><strong>Misol:</strong> Agar foydalanuvchining \"SOrder\" moduli uchun ViewPermit = true va ChangePermit = true bo'lsa:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>✅ Sotuv buyurtmalari ro'yxatini ko'ra oladi</li>");
            content.AppendLine("<li>✅ Buyurtma tafsilotlarini ko'ra oladi</li>");
            content.AppendLine("<li>✅ Yangi buyurtma yarata oladi</li>");
            content.AppendLine("<li>✅ Mavjud buyurtmani tahrirlay oladi</li>");
            content.AppendLine("<li>✅ Buyurtmani o'chira oladi (agar shunday funksiya mavjud bo'lsa)</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Kodda ChangePermit tekshiruvi <code>RoleHelper.IsChangePermitted()</code> metod orqali amalga oshiriladi.</p>");
            content.AppendLine("<p>Controller'larda bu tekshiruv quyidagicha amalga oshiriladi:</p>");
            content.AppendLine("<pre><code>if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values[\"controller\"].ToString(), RouteData.Values[\"action\"].ToString()))\n{\n    return RedirectToAction(\"Index\", \"ViewNotAuthorized\");\n}</code></pre>");

            content.AppendLine("<h3>7. Ruxsat berish tartibi</h3>");
            content.AppendLine("<p>Ruxsatlar quyidagi tartibda beriladi:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li><strong>PMODULE yaratish:</strong> Admin Panel → PModule bo'limiga o'ting va yangi modul yarating (yoki mavjud modullarni ko'ring).</li>");
            content.AppendLine("<li><strong>Rolga ruxsat berish:</strong> Admin Panel → Admin Panel → Permissions bo'limiga o'ting, rol tanlang va unga kerakli modullar uchun ViewPermit va ChangePermit belgilang.</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-5: Rolga ruxsat berish oynasi</strong></p>");
            content.AppendLine("<p>Ruxsat berish oynasida:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Har bir modul uchun <strong>ViewPermit</strong> checkbox mavjud - bu ko'rish huquqini belgilaydi</li>");
            content.AppendLine("<li>Har bir modul uchun <strong>ChangePermit</strong> checkbox mavjud - bu o'zgartirish huquqini belgilaydi</li>");
            content.AppendLine("<li>O'zgarishlarni saqlash uchun \"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Faqat ko'rish mumkin (Read-only) qilish:</strong> ViewPermit = true, ChangePermit = false</p>");
            content.AppendLine("<p><strong>To'liq huquq (Full access) berish:</strong> ViewPermit = true, ChangePermit = true</p>");

            content.AppendLine("<h3>8. Authorization jarayoni (qanday ishlaydi?)</h3>");
            content.AppendLine("<p>Tizimda har bir so'rov quyidagi bosqichlardan o'tadi:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li><strong>Authentication (Tizimga kirish):</strong> Foydalanuvchi username va parol bilan tizimga kiradi.</li>");
            content.AppendLine("<li><strong>User-Role bog'lanishi:</strong> Tizim foydalanuvchining rollarini oladi (USERS → ROLES jadvalidan).</li>");
            content.AppendLine("<li><strong>Role-Permission bog'lanishi:</strong> Tizim foydalanuvchining roli orqali uning ruxsatlarini topadi (PERMISSIONS jadvalidan).</li>");
            content.AppendLine("<li><strong>PMODULE tekshiruvi:</strong> Tizim so'ralayotgan Controller va Action uchun mos PMODULE ni topadi.</li>");
            content.AppendLine("<li><strong>Ruxsat tekshiruvi:</strong> Tizim foydalanuvchining ushbu PMODULE uchun ViewPermit yoki ChangePermit ga ega ekanligini tekshiradi.</li>");
            content.AppendLine("<li><strong>Natija:</strong> Agar ruxsat bo'lsa - sahifa ko'rsatiladi, aks holda \"Ruxsat yo'q\" sahifasiga yo'naltiriladi.</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p>Bu jarayon <code>RoleHelper</code> klassining metodlari orqali amalga oshiriladi va natijalar cache'ga saqlanadi (10 daqiqa).</p>");
            content.AppendLine("<p><strong>Rasm-6: Authorization jarayoni diagrammasi</strong></p>");

            content.AppendLine("<h3>9. Amaliy misollar</h3>");
            content.AppendLine("<p><strong>Misol 1: Sales Manager roli</strong></p>");
            content.AppendLine("<p>Sales Manager roli uchun quyidagi ruxsatlar berilgan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>SOrder: ViewPermit = true, ChangePermit = true</li>");
            content.AppendLine("<li>Customer: ViewPermit = true, ChangePermit = true</li>");
            content.AppendLine("<li>POrder: ViewPermit = true, ChangePermit = false (faqat ko'rish)</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Natija: Sales Manager sotuv buyurtmalarini yaratishi, tahrirlashi mumkin, lekin xarid buyurtmalarini faqat ko'ra oladi.</p>");

            content.AppendLine("<p><strong>Misol 2: Viewer roli</strong></p>");
            content.AppendLine("<p>Viewer roli uchun barcha modullarda ViewPermit = true, ChangePermit = false.</p>");
            content.AppendLine("<p>Natija: Viewer barcha ma'lumotlarni ko'ra oladi, lekin hech narsani o'zgartira olmaydi.</p>");

            content.AppendLine("<p><strong>Misol 3: Warehouse Manager roli</strong></p>");
            content.AppendLine("<p>Warehouse Manager roli uchun:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Warehouse: ViewPermit = true, ChangePermit = true</li>");
            content.AppendLine("<li>Incomes: ViewPermit = true, ChangePermit = true</li>");
            content.AppendLine("<li>Expenses: ViewPermit = true, ChangePermit = true</li>");
            content.AppendLine("<li>Inventory: ViewPermit = true, ChangePermit = false</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Natija: Warehouse Manager ombor kirim-chiqimlarini boshqara oladi, lekin ombor qoldiqlarini faqat ko'ra oladi.</p>");

            return new UserManualChapterViewModel
            {
                Id = "permissions",
                Title = "Foydalanuvchilar, rollar va ruxsatlar",
                ModuleCode = "PMODULE",
                ContentHtml = content.ToString(),
                Order = 2
            };
        }

        private static UserManualChapterViewModel CreateSalesChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Sotuvlar moduli (CRM)</h2>");
            content.AppendLine("<p>Sotuvlar moduli mijozlar bilan ishlash, buyurtmalar va shartnomalarni boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Sotuvlar (CRM)</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-7: Sotuvlar bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Xaridorlar (Customers) – ma'lumotnoma CRUD</h3>");
            content.AppendLine("<p>Xaridorlar (Customers/CustomerBase) – CRMning <strong>asosiy ma'lumotnoma</strong>si. Bu yerda yaratilgan mijoz kartalari keyin buyurtma, shartnoma, invoys va boshqa hujjatlarda tanlanadi.</p>");
            content.AppendLine("<h4>Yangi xaridor yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Sotuvlar → Xaridorlar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Formada quyidagi maydonlarni to'ldiring:");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Nomi (Name) - xaridorning to'liq nomi</li>");
            content.AppendLine("<li>Manzil, telefon, email va boshqa ma'lumotlar</li>");
            content.AppendLine("</ul></li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-8: Xaridor yaratish formasi</strong></p>");

            content.AppendLine("<h4>Xaridorni ko'rish (View) va tahrirlash (Edit):</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Ro'yxatdagi satrga bosganda yoki \"Ko'rish\" tugmasi orqali xaridor kartasi ochiladi.</li>");
            content.AppendLine("<li>\"Tahrirlash\" tugmasi orqali ism, aloqa ma'lumotlari, INN va boshqa maydonlarni o'zgartirish mumkin.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>Xaridorni o'chirish (Delete):</h4>");
            content.AppendLine("<p>O'chirish odatda <strong>soft delete</strong> tamoyiliga asoslangan – yozuv o'chirilgan deb belgilangan bo'ladi va ro'yxat filtrlari orqali faqat faol mijozlar ko'rsatiladi.</p>");

            content.AppendLine("<h3>2. Sotuv buyurtmalari (SOrder)</h3>");
            content.AppendLine("<p>Sotuv buyurtmalari – mijozdan kelgan talabni rasmiylashtiruvchi asosiy hujjat. Ko'pincha ishlab chiqarish va yetkazib berish rejalari aynan shu hujjatdan boshlanadi.</p>");
            content.AppendLine("<h4>Asosiy maydonlar:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>OrderNo</strong> - buyurtma raqami</li>");
            content.AppendLine("<li><strong>Customer</strong> - xaridor</li>");
            content.AppendLine("<li><strong>Contract</strong> - shartnoma (ixtiyoriy)</li>");
            content.AppendLine("<li><strong>IssuedDate</strong> - buyurtma sanasi</li>");
            content.AppendLine("<li><strong>Products</strong> - buyurtma mahsulotlari</li>");
            content.AppendLine("<li><strong>Amount</strong> - jami summa</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.1. Yangi buyurtma yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Sotuvlar → Buyurtmalar bo'limiga o'ting</li>");
            content.AppendLine("<li>Yuqoridagi \"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Xaridorni tanlang</li>");
            content.AppendLine("<li>Shartnomani tanlang (agar mavjud bo'lsa)</li>");
            content.AppendLine("<li>Buyurtma sanasini kiriting</li>");
            content.AppendLine("<li>Mahsulotlarni qo'shing (\"Qo'shish\" tugmasi orqali):");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Mahsulotni tanlang</li>");
            content.AppendLine("<li>Miqdorni kiriting</li>");
            content.AppendLine("<li>Narxni kiriting</li>");
            content.AppendLine("<li>O'lchov birligini tanlang</li>");
            content.AppendLine("</ul></li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-9: Sotuv buyurtmasi yaratish</strong></p>");

            content.AppendLine("<h4>2.2. Mavjud buyurtmani ko'rish (View) va tahrirlash (Edit):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Buyurtmalar ro'yxatidan tahrirlash kerak bo'lgan buyurtmani toping</li>");
            content.AppendLine("<li>Buyurtma ustiga bosing yoki \"Tahrirlash\" tugmasini bosing</li>");
            content.AppendLine("<li>Ma'lumotlarni o'zgartiring</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h4>2.3. Buyurtmani o'chirish (Delete):</h4>");
            content.AppendLine("<p>Odatda buyurtmalarni to'liq o'chirish faqat:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>U bilan bog'langan invoys, ombor chiqimi va boshqa hujjatlar yo'q bo'lsa;</li>");
            content.AppendLine("<li>Yoki buyurtma hali <strong>Draft</strong>/<strong>Yangi</strong> statusida bo'lsa;</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Agar bog'liq hujjatlar mavjud bo'lsa, buyurtma ko'pi bilan <strong>Bekor qilingan</strong> statusiga o'tkaziladi, lekin tarix saqlanib qoladi.</p>");

            content.AppendLine("<h4>Qidirish va filtr:</h4>");
            content.AppendLine("<p>Buyurtmalar ro'yxatida quyidagi filtrlardan foydalanishingiz mumkin:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Buyurtma raqami bo'yicha qidirish</li>");
            content.AppendLine("<li>Xaridor bo'yicha filtr</li>");
            content.AppendLine("<li>Sana oralig'i bo'yicha filtr</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Rasm-10: Buyurtmalar ro'yxati va filtrlari</strong></p>");

            content.AppendLine("<h3>3. Sotuv shartnomalari (SContract)</h3>");
            content.AppendLine("<p>Shartnomalar – mijoz bilan o'rtadagi uzoq muddatli kelishuvni ifodalaydi. Buyurtmalar ko'pincha shartnomaga bog'lanadi.</p>");
            content.AppendLine("<p>Shartnoma yaratish, tahrirlash va o'chirish jarayoni buyurtmalarga o'xshash, lekin shartnoma odatda <strong>asosiy hujjat</strong>, buyurtmalar esa uning ichidagi realizatsiyalar hisoblanadi.</p>");
            content.AppendLine("<p><strong>Rasm-11: Shartnomalar ro'yxati</strong></p>");

            content.AppendLine("<h3>4. Sotuv invoyslari (SalesInvoices)</h3>");
            content.AppendLine("<p>Sotuv invoyslari – mijozga chiqariladigan rasmiy hisob-faktura (moliyaviy hujjat). U ko'pincha sotuv buyurtmasi yoki shartnoma asosida yaratiladi.</p>");
            content.AppendLine("<h4>4.1. Yangi invoys yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Sotuvlar → Invoyslar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Xaridorni tanlang</li>");
            content.AppendLine("<li>Buyurtma yoki shartnomani tanlang (agar mavjud bo'lsa)</li>");
            content.AppendLine("<li>Invoys sanasini kiriting</li>");
            content.AppendLine("<li>Mahsulotlarni qo'shing</li>");
            content.AppendLine("<li>Jami summa, QQS va boshqa ma'lumotlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-12: Sotuv invoysi yaratish</strong></p>");
            content.AppendLine("<p>Invoysni buyurtmadan ham yaratishingiz mumkin - \"Invoys yaratish\" tugmasi orqali.</p>");

            content.AppendLine("<h4>4.2. Ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> ro'yxatdan invoysga kirganda barcha rekvizitlar va pozitsiyalar ko'rinadi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> faqat hali moliya yoki soliq tizimiga yuborilmagan invoyslarda ayrim maydonlarni tahrirlash mumkin.</li>");
            content.AppendLine("<li><strong>Delete:</strong> odatda invoys o'rniga tuzatish/korrektirovka kiritish tavsiya etiladi; to'liq o'chirish faqat xatolik va bog'liq tranzaksiyalar bo'lmasa amalga oshiriladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>5. Счёт-фактура (SalesSF)</h3>");
            content.AppendLine("<p>Счёт-фактура – soliq to'lovchilar uchun <strong>elektron soliq hujjati</strong>. U faqat tasdiqlangan invoys asosida yaratiladi.</p>");
            content.AppendLine("<h4>5.1. Счёт-фактура yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Sotuvlar → Счёт-фактура bo'limiga o'ting</li>");
            content.AppendLine("<li>Invoysni tanlang</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Ma'lumotlarni to'ldiring va \"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-13: Счёт-фактура yaratish</strong></p>");

            content.AppendLine("<h3>6. ТТНК (SalesTTNK)</h3>");
            content.AppendLine("<p>ТТНК (Transportno-Tovarno-Nakladnaya Karta) – yukni tashish paytida mashinaga beriladigan <strong>transport tovar nakladnoy</strong>.</p>");
            content.AppendLine("<p>ТТНК invoysdan yoki buyurtmadan yaratiladi va ombordan chiqim bilan bog'lanadi.</p>");
            content.AppendLine("<p><strong>Rasm-14: ТТНК yaratish</strong></p>");

            content.AppendLine("<h3>7. Sotuvlar bo'yicha oylik va analitik hisobotlar</h3>");
            content.AppendLine("<p>Sotuvlar bo'yicha oylik hisobotlar va analitikalar alohida <strong>SalesReports</strong> bo'limida to'plangan.</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Oylik sotuv hisobotlari</strong> – menyudan: <em>Reports → Sales Reports → Monthly Report</em> (view: <code>Views/SalesReports/MonthlyReport.cshtml</code>).</li>");
            content.AppendLine("<li><strong>Mahsulotlar kesimida sotuvlar</strong> – <em>Reports → Sales Reports → Sales by Product</em>.</li>");
            content.AppendLine("<li><strong>Mijozlar kesimida sotuvlar</strong> – <em>Reports → Sales Reports → Sales by Customer</em>.</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Bu sahifalarda sana oralig'i, mijoz, mahsulot yoki kategoriya bo'yicha filtrlar orqali oylik tahlil qilinadi. Natijalarni ko'rish (View) rejimida bo'lib, bu yerda CRUD emas, faqat analitika mavjud.</p>");

            return new UserManualChapterViewModel
            {
                Id = "sales",
                Title = "Sotuvlar moduli (CRM)",
                ModuleCode = "SOrder",
                ContentHtml = content.ToString(),
                Order = 3
            };
        }

        private static UserManualChapterViewModel CreatePurchaseChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Xarid moduli</h2>");
            content.AppendLine("<p>Xarid moduli ta'minotchilar bilan ishlash, xarid buyurtmalari va shartnomalarni boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Ta'minot (SRM)</strong> yoki <strong>Xarid</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-15: Xarid bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Ta'minotchilar (Suppliers) – ma'lumotnoma CRUD</h3>");
            content.AppendLine("<p>Ta'minotchilar – barcha xarid jarayonlari uchun asos bo'ladigan ma'lumotnoma. Bu yerda yaratilgan supplierlar direct va indirect xaridlarda, shartnomalarda, invoyslarda ishlatiladi.</p>");
            content.AppendLine("<h4>1.1. Yangi ta'minotchi yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ta'minot → Ta'minotchilar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Formada kerakli maydonlarni to'ldiring</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h4>1.2. Ko'rish, tahrirlash, o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> ro'yxatdan supplier kartasiga kirib barcha rekvizitlarni ko'rish.</li>");
            content.AppendLine("<li><strong>Edit:</strong> aloqa ma'lumotlari, to'lov shartlari, INN va h.k.ni o'zgartirish.</li>");
            content.AppendLine("<li><strong>Delete:</strong> odatda soft delete – faqat faol bo'lmagan ta'minotchilar filtrlanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>2. Xarid buyurtmalari (POrder) – Direct va Indirect</h3>");
            content.AppendLine("<p>Xarid buyurtmalari (POrders) ikkiga bo'linadi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Direct Purchase</strong> – to'g'ridan-to'g'ri ishlab chiqarish/xomashyo uchun xarid.</li>");
            content.AppendLine("<li><strong>Indirect Purchase</strong> – xizmatlar, ofis xarajatlari, yordamchi materiallar va h.k.</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<h4>Asosiy maydonlar:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>OrderNo</strong> - buyurtma raqami</li>");
            content.AppendLine("<li><strong>Supplier</strong> - ta'minotchi</li>");
            content.AppendLine("<li><strong>Contract</strong> - shartnoma</li>");
            content.AppendLine("<li><strong>Date</strong> - buyurtma sanasi</li>");
            content.AppendLine("<li><strong>Products</strong> - buyurtma mahsulotlari</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.1. Yangi xarid buyurtmasi yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Xarid → Buyurtmalar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Ta'minotchini tanlang</li>");
            content.AppendLine("<li>Buyurtma turini tanlang (Direct yoki Indirect, agar alohida maydon bo'lsa)</li>");
            content.AppendLine("<li>Mahsulot yoki xizmatlarni qo'shing</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-16: Xarid buyurtmasi yaratish</strong></p>");

            content.AppendLine("<h4>2.2. Ko'rish (View), tahrirlash (Edit), o'chirish (Delete):</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> ro'yxatdan buyurtma kartasini ochib barcha pozitsiyalar, status, bog'langan shartnoma va invoyslarni ko'rish.</li>");
            content.AppendLine("<li><strong>Edit:</strong> tasdiqlanmagan (Draft) buyurtmalarda miqdor, narx, sana, ta'minotchini tahrirlash mumkin.</li>");
            content.AppendLine("<li><strong>Delete/Cancel:</strong> tasdiqlangan va omborga kirim bo'lgan buyurtmalar odatda to'liq o'chirilmaydi, balki \"Bekor qilingan\" statusiga o'tkaziladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>3. Xarid shartnomalari (PContract) – Direct/Indirect shartnomalar</h3>");
            content.AppendLine("<p>Xarid shartnomalari – ta'minotchi bilan uzoq muddatli kelishuv: narxlar, yetkazib berish shartlari, Direct/Indirect xarid turlari shu yerda belgilanadi.</p>");
            content.AppendLine("<p><strong>Rasm-17: Xarid shartnomalari ro'yxati</strong></p>");

            content.AppendLine("<p>POrder yaratishda siz shartnomani tanlasangiz, narx va boshqa shartlar avtomatik tortilishi mumkin.</p>");

            content.AppendLine("<h3>4. Xarid invoyslari (PInvoice) va omborga kirim</h3>");
            content.AppendLine("<p>Xarid invoyslari – ta'minotchidan kelgan rasmiy hisob-fakturalar. Ular orqali moliya va ombor xaridlari bog'lanadi.</p>");
            content.AppendLine("<h4>4.1. Xarid invoysini yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>SRM/Xarid → Invoyslar (PInvoices) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>POrder (xarid buyurtmasi)ni tanlang yoki to'g'ridan-to'g'ri supplier va mahsulotlarni kiriting</li>");
            content.AppendLine("<li>Invoys raqami, sanasi, summa, QQS va boshqa rekvizitlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-18: Xarid invoysi</strong></p>");

            content.AppendLine("<h4>4.2. Omborga kirim qilish – invoys bilan va invoyssiz</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Invoys bilan kirim:</strong> Invoys tasdiqlangandan so'ng, \"Omborga kirim yaratish\" tugmasi orqali tegishli omborga INCOME hujjati yaratiladi. Mahsulotlar, miqdor va narx invoysdan tortiladi.</li>");
            content.AppendLine("<li><strong>Invoyssiz kirim (pre-receipt):</strong> Ombor kirimida ta'minotchi va POrder tanlanadi, ammo invoys raqami hali noma'lum bo'lishi mumkin. Keyinchalik PInvoice kiritilganda shu kirimga bog'lanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>4.3. Bir ombordan boshqasiga o'tkazish (transfer) va chiqim</h4>");
            content.AppendLine("<p>SRM va Ombor modullari birga ishlaydi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Warehouse transfer:</strong> Bitta ombordan ikkinchisiga o'tkazish uchun maxsus \"Ichki o'tkazma\" (transfer) hujjati yaratiladi – ichida bitta omborda chiqim, boshqasida kirim avtomatik hosil bo'ladi.</li>");
            content.AppendLine("<li><strong>Chiqarish (Expense):</strong> Ishlab chiqarish, sotuv yoki boshqa maqsadlar uchun ombordan chiqim qilish <strong>Expenses</strong> moduli orqali amalga oshiriladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<p>Bu jarayonlarning barchasida <strong>View/Edit/Delete</strong> qoidalari bir xil: faqat hali bog'liq hujjatlari yo'q yoki tasdiqlanmagan hujjatlar to'liq tahrirlanadi va o'chiriladi, qolganlari esa faqat ko'rish yoki bekor qilish (cancel) rejimida ishlaydi.</p>");

            return new UserManualChapterViewModel
            {
                Id = "purchase",
                Title = "Xarid moduli",
                ModuleCode = "POrder",
                ContentHtml = content.ToString(),
                Order = 4
            };
        }

        private static UserManualChapterViewModel CreateWarehouseChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Omborxona boshqaruvi</h2>");
            content.AppendLine("<p>Omborxona moduli ombor operatsiyalari, kirim-chiqimlar va qoldiqlarni boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Omborxona</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-19: Omborxona bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Omborlar (Warehouses)</h3>");
            content.AppendLine("<p>Omborlar - mahsulotlar saqlanadigan joylar.</p>");
            content.AppendLine("<h4>Yangi ombor yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Omborxona → Omborlar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Ombor nomi va boshqa ma'lumotlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h3>2. Ombor kirimlari (Incomes)</h3>");
            content.AppendLine("<p>Ombor kirimlari - omborga mahsulotlar kiritilganda yaratiladigan hujjatlar.</p>");
            content.AppendLine("<h4>2.1. Yangi kirim yaratish (umumiy holat):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Omborxona → Ombor kirimlari bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Omborni tanlang</li>");
            content.AppendLine("<li>Mahsulotlarni va miqdorlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-20: Ombor kirimi yaratish</strong></p>");

            content.AppendLine("<h4>2.2. Kirimni invoys bilan bog'lash:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Agar kirim <strong>PInvoice</strong> orqali yaratilsa, narx va summa invoysdan avtomatik tortiladi.</li>");
            content.AppendLine("<li>Agar invoyssiz kirim qilingan bo'lsa, keyinchalik PInvoice kiritilganda shu kirimga bog'lash mumkin.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.3. Kirimni ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> kirim kartasini ochib pozitsiyalar, miqdor va narxlarni ko'rish.</li>");
            content.AppendLine("<li><strong>Edit:</strong> faqat hali balansga yopilmagan yoki keyingi jarayonlar (chiqim, ishlab chiqarish) bilan bog'liq bo'lmagan kirimlarda.</li>");
            content.AppendLine("<li><strong>Delete/Cancel:</strong> noto'g'ri kirimlarda – odatda bekor qilish statusi bilan, tarix saqlangan holda.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>3. Ombor chiqimlari (Expenses)</h3>");
            content.AppendLine("<p>Ombor chiqimlari - ombordan mahsulotlar chiqarilganda yaratiladigan hujjatlar.</p>");
            content.AppendLine("<h4>3.1. Yangi chiqim yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Omborxona → Ombor chiqimlari bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Omborni tanlang</li>");
            content.AppendLine("<li>Mahsulotlarni va chiqariladigan miqdorlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-21: Ombor chiqimi yaratish</strong></p>");

            content.AppendLine("<h4>3.2. Boshqa omborga o'tkazish (Transfer):</h4>");
            content.AppendLine("<p>Bir ombordan ikkinchisiga o'tkazishda ikki tomonlama operatsiya amalga oshadi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Chiqayotgan omborda – <strong>Expense</strong> hujjati (chiqim).</li>");
            content.AppendLine("<li>Qabul qilayotgan omborda – avtomatik yoki qo'lda <strong>Income</strong> hujjati (kirim).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Maxsus \"Ichki transfer\" ekranida manba va qabul qiluvchi omborlar, mahsulotlar va miqdor kiritiladi – tizim kerakli kirim/chiqimni yaratadi.</p>");

            content.AppendLine("<h4>3.3. Chiqimni ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> chiqim kartasi, bog'langan ishlab chiqarish, sotuv yoki transfer hujjatini ko'rish.</li>");
            content.AppendLine("<li><strong>Edit:</strong> hali balansga yopilmagan va ishlab chiqarish/tannarx hisobiga kirmagan operatsiyalar uchun.</li>");
            content.AppendLine("<li><strong>Delete/Cancel:</strong> noto'g'ri chiqqan hujjatlarni bekor qilish – natijada qoldiq qayta hisoblanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>4. Ombor qoldiqlari (Inventory)</h3>");
            content.AppendLine("<p>Ombor qoldiqlari - har bir omborda qancha mahsulot qolganini ko'rsatadi.</p>");
            content.AppendLine("<p>Qoldiqlar avtomatik ravishda kirim-chiqim operatsiyalari asosida hisoblanadi.</p>");
            content.AppendLine("<p><strong>Rasm-22: Ombor qoldiqlari ro'yxati</strong></p>");

            content.AppendLine("<h3>5. Ombor bo'yicha oylik hisobotlar</h3>");
            content.AppendLine("<p>Ombor va zaxiralar bo'yicha oylik hisobotlar <strong>InventoryReports</strong> bo'limida to'plangan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Dashboard</strong> – umumiy ko'rsatkichlar va grafiklar (view: <code>Views/InventoryReports/Dashboard.cshtml</code>).</li>");
            content.AppendLine("<li><strong>Monthly Report</strong> – oylar kesimida kirim, chiqim va qoldiq dinamikasi (view: <code>Views/InventoryReports/MonthlyReport.cshtml</code>).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Bu hisobotlarga menyudan odatda <em>Reports → Inventory Reports</em> orqali kiriladi.</p>");

            content.AppendLine("<h3>6. Seriyalar, partiyalar va DLOC (lot + joylashuv)</h3>");
            content.AppendLine("<p>Kelgusida omborlar uchun <strong>lot tracking</strong> va <strong>Direct Location (DLOC)</strong> tizimi joriy qilinmoqda. Asosiy g'oya:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Har bir kirim partiyasi alohida <strong>lot</strong> sifatida ro'yxatdan o'tkaziladi (<code>INCOME_LOTS</code> jadvali).</li>");
            content.AppendLine("<li>Chiqimlar qaysi lotdan olingani bo'yicha bog'lanadi (<code>EXPENSE_PRODUCTS.LotID</code>).</li>");
            content.AppendLine("<li>Qoldiq lot darajasida yuritiladi (<code>PRODUCT_STOCK_LOTS</code> yoki <code>PRODUCT_STOCKS.LotID</code>).</li>");
            content.AppendLine("<li><strong>DLOC</strong> – ombor ichidagi yacheykalar (<code>WAREHOUSE_LOCATIONS</code>) va lotlarning joylashuvi (<code>LOT_LOCATIONS</code>).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Kirish va chiqim formalariga <strong>Lot</strong> va <strong>Location</strong> maydonlari qo'shilgach, foydalanuvchi har bir partiya va uning ombordagi joyini aniq ko'rishi va boshqarishi mumkin bo'ladi.</p>");

            return new UserManualChapterViewModel
            {
                Id = "warehouse",
                Title = "Omborxona boshqaruvi",
                ModuleCode = "Warehouse",
                ContentHtml = content.ToString(),
                Order = 5
            };
        }

        private static UserManualChapterViewModel CreateProductionChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Ishlab chiqarish rejalashtirish</h2>");
            content.AppendLine("<p>Ishlab chiqarish moduli mahsulot ishlab chiqarish rejalarini boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Ishlab chiqarish</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-26: Ishlab chiqarish bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Mahsulot rejasi (ProductPlan) – sotuv buyurtmasidan reja</h3>");
            content.AppendLine("<p>Mahsulot rejasi – kelajak davrlar uchun ishlab chiqarish hajmini belgilovchi hujjat. Reja ko'pincha <strong>SOrder</strong> (sotuv buyurtmalari) va forecastlardan kelib chiqib shakllanadi.</p>");
            content.AppendLine("<h4>1.1. Yangi reja yaratish (qo'lda):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ishlab chiqarish → Rejalashtirish bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Mahsulotni tanlang</li>");
            content.AppendLine("<li>Rejalashtirilgan miqdorni kiriting</li>");
            content.AppendLine("<li>Sana va boshqa parametrlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-27: Mahsulot rejasi yaratish</strong></p>");

            content.AppendLine("<h4>1.2. Sotuv buyurtmalaridan reja shakllantirish:</h4>");
            content.AppendLine("<p>Ba'zi konfiguratsiyada ProductPlan quyidagicha avtomatlashtiriladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Ma'lum davr uchun tasdiqlangan SOrderlar tanlanadi;</li>");
            content.AppendLine("<li>Ularning mahsulotlari va miqdorlari <strong>PRODUCTPLANS_DAILY</strong> jadvaliga kunlar bo'yicha taqsimlanadi;</li>");
            content.AppendLine("<li>Natijada Ishlab chiqarish → Rejalashtirish sahifasida har kun bo'yicha ishlab chiqarish rejalari paydo bo'ladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>2. BOM (Bill of Materials) va Norma-sarf (NORM)</h3>");
            content.AppendLine("<p>BOM – tayyor mahsulotning <strong>tarkibi</strong>, ya'ni qaysi xomashyodan qancha kerakligi. NORM – aynan shu BOM asosida ishlab chiqarishda ishlatiladigan <strong>sarf normalari</strong> bo'lib, amalda qancha xomashyo chiqishi kerakligini aniqlab beradi.</p>");
            content.AppendLine("<h4>2.1. BOM yaratish (Engineering → BOM):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Engineering → BOM bo'limiga o'ting</li>");
            content.AppendLine("<li>Asosiy mahsulotni (finished good) tanlang</li>");
            content.AppendLine("<li>Har bir komponent uchun xomashyo mahsuloti, miqdori va birlikni kiriting</li>");
            content.AppendLine("<li>Agar kerak bo'lsa sex/operatsiya bo'yicha bo'ling</li>");
            content.AppendLine("<li>\"Saqlash\" va \"Aktiv\" qilish (IsActive = true) tugmalari orqali tasdiqlang</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-28: BOM ro'yxati</strong></p>");

            content.AppendLine("<h4>2.2. Norma-sarf (NORM) – kiritish, tahrirlash, o'chirish (CRUD):</h4>");
            content.AppendLine("<p>Norma-sarf yozuvlari odatda <strong>Norm</strong> yoki <strong>Tannarx/Norm</strong> bo'limida boshqariladi.</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ishlab chiqarish yoki Engineering menyusidan <strong>Norma-sarf (NORM)</strong> bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosib, tayyor mahsulot va sex/operatsiyani tanlang</li>");
            content.AppendLine("<li>BOMdagi xomashyolar ro'yxatidan foydalanib, har biri uchun <strong>nazariy sarf</strong> (bir birlik mahsulotga qancha sarf bo'lishi kerak)ni kiriting</li>");
            content.AppendLine("<li>Agar kerak bo'lsa, yo'qotish foizi (%) va boshqa texnologik koeffitsientlarni belgilang</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing va NORM yozuvini <strong>aktiv</strong> holatga o'tkazing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p>Keyinchalik NORM yozuvini:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Ko'rish (View):</strong> mahsulot kartasidan yoki NORM ro'yxatidan ochish mumkin;</li>");
            content.AppendLine("<li><strong>Tahrirlash (Edit):</strong> texnologiya o'zgarsa, sarf miqdorlari va yo'qotish foizlari yangilanadi;</li>");
            content.AppendLine("<li><strong>O'chirish/Deaktiv qilish (Delete/Deactivate):</strong> eskirgan normativlar deaktiv qilinadi, yangi versiya yaratiladi – tarix saqlanib qoladi.</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Rasm-23: Norma-sarf</strong></p>");

            content.AppendLine("<h3>3. Ishlab chiqarish (ProductProduction) va avtomatik xomashyo chiqimi</h3>");
            content.AppendLine("<p>Ishlab chiqarish (PRODUCT_PRODUCTION) – fakt ishlab chiqarilgan mahsulotlar ro'yxati. Bu modul orqali tayyor mahsulotlar omborga kiradi va xomashyo avtomatik chiqimi ishlashi mumkin.</p>");
            content.AppendLine("<h4>3.1. Ishlab chiqarish yozuvini yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ishlab chiqarish → Ishlab chiqarish bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Mahsulotni tanlang</li>");
            content.AppendLine("<li>Sexni tanlang</li>");
            content.AppendLine("<li>Sana va smenani kiriting</li>");
            content.AppendLine("<li>Kunduzgi va kechki smena miqdorlarini kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-24: Ishlab chiqarish yozuvi</strong></p>");

            content.AppendLine("<h4>3.2. Avtomatik xomashyo chiqimi qanday ishlaydi?</h4>");
            content.AppendLine("<p>Agar modul sozlamalarida \"avtomatik xomashyo chiqimi\" yoqilgan bo'lsa, har bir fakt ishlab chiqarish yozuvi quyidagilarni bajaradi:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Tayyor mahsulot uchun aktiv BOM va NORM topiladi.</li>");
            content.AppendLine("<li>Ishlab chiqarilgan miqdor × NORM asosida xomashyo sarfi hisoblanadi.</li>");
            content.AppendLine("<li>Kerakli xomashyolar <strong>raw ombor</strong>dan avtomatik <strong>Expense</strong> (chiqim) hujjati orqali chiqariladi.</li>");
            content.AppendLine("<li>Natijada tayyor mahsulot ishlab chiqarish omboriga kirim, xomashyo omboridan chiqim yoziladi.</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p>Bu jarayon world-class ERP tamoyillariga mos ravishda BOM va NORMga tayanadi.</p>");

            content.AppendLine("<h4>3.3. Boshqa omborga chiqim (Production → Sales transfer):</h4>");
            content.AppendLine("<p>Ishlab chiqarilgan tayyor mahsulotni ishlab chiqarish omboridan sotuv omboriga o'tkazish uchun maxsus \"ProductionToSalesTransfer\" ekrani ishlatiladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Manba ombor – ishlab chiqarish ombori;</li>");
            content.AppendLine("<li>Qabul qiluvchi ombor – sotuv ombori;</li>");
            content.AppendLine("<li>Mahsulotlar va miqdorlar – fakt ishlab chiqarishdan kelib chiqib belgilanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>4. Sifat nazorati (QualityInspection)</h3>");
            content.AppendLine("<p>Sifat nazorati - ishlab chiqarilgan mahsulotlarning sifatini tekshirish.</p>");
            content.AppendLine("<h4>Sifat nazorati yozuvini yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ishlab chiqarish → Sifat nazorati bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Ishlab chiqarish yozuvini tanlang</li>");
            content.AppendLine("<li>Sifat ko'rsatkichlarini kiriting</li>");
            content.AppendLine("<li>Natijalarni saqlang</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-25: Sifat nazorati</strong></p>");

            content.AppendLine("<h3>5. Ishlab chiqarish va sifat bo'yicha oylik hisobotlar</h3>");
            content.AppendLine("<p>Ishlab chiqarish va sifat ko'rsatkichlari bo'yicha oylik hisobotlar <strong>MESReports</strong> bo'limida to'plangan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Production Monthly</strong> – oylar kesimida ishlab chiqarish hajmi (view: <code>Views/MESReports/ProductionMonthly.cshtml</code>).</li>");
            content.AppendLine("<li><strong>Quality Monthly</strong> – oylik sifat ko'rsatkichlari va brak tahlili (view: <code>Views/MESReports/QualityMonthly.cshtml</code>).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Bu hisobotlar odatda menyudan <em>Reports → MES Reports</em> yoki Ishlab chiqarish bo'limidagi \"Hisobotlar\" menyusi orqali ochiladi.</p>");

            content.AppendLine("<h3>6. Tannarx (Tannarx/TannarxAnalytics) – modul nimadan iborat va qayerdan ma'lumot oladi?</h3>");
            content.AppendLine("<p><strong>Tannarx</strong> moduli tayyor mahsulotlar <strong>birlik tannarxini</strong> (cost per unit) va umumiy ishlab chiqarish tannarxini hisoblash uchun xizmat qiladi. U quyidagi manbalardan ma'lumot tortadi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>SRM/Xarid:</strong> POrder, PInvoice va ombor kirimlari bo'yicha xomashyo va materiallar narxi (purchase price).</li>");
            content.AppendLine("<li><strong>Ombor (Warehouse):</strong> Incomes/Expenses va PRODUCT_STOCKS bo'yicha xomashyo qoldiqlari va sarfi.</li>");
            content.AppendLine("<li><strong>Ishlab chiqarish (MES):</strong> PRODUCT_PRODUCTION bo'yicha fakt ishlab chiqarilgan hajm, ProductPlan bo'yicha reja.</li>");
            content.AppendLine("<li><strong>BOM va NORM:</strong> har bir tayyor mahsulot uchun tarkib va sarf normasi.</li>");
            content.AppendLine("<li><strong>Finance/HR:</strong> ish haqi, energiya, umumsex va bosh ofis xarajatlari – FinTransactions va HRPayroll ma'lumotlari asosida.</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Tannarx hisobida har bir tayyor mahsulot uchun odatda quyidagi bosqichlar bajariladi:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Tanlangan davr va sex/ombor bo'yicha fakt ishlab chiqarilgan hajm (PRODUCT_PRODUCTION) olinadi.</li>");
            content.AppendLine("<li>BOM + NORM asosida bu hajmga to'g'ri keladigan xomashyo sarfi aniqlanadi va xarid narxlari (PInvoice) bilan ko'paytiriladi.</li>");
            content.AppendLine("<li>Qo'shimcha xarajatlar (ish haqi, energiya, amortizatsiya va h.k.) umumiy bazaga nisbatan mahsulotlarga taqsimlanadi (kg, soat, summa ulushi yoki boshqa bazaga qarab).</li>");
            content.AppendLine("<li>Natijada har bir mahsulot uchun <strong>bir birlik tannarx</strong> va umumiy tannarx shakllanadi; bu ma'lumot TannarxAnalytics hisobotlarida ko'rsatiladi.</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p>Tannarx modulining <strong>CRUD</strong> qismi asosan sozlamalarga tegishli (qaysi xarajat moddasi qaysi bazaga taqsimlanishi, qaysi sex/omborlar hisobga olinishi), natijalar esa hisobot rejimida (View) ko'rinadi.</p>");

            return new UserManualChapterViewModel
            {
                Id = "production",
                Title = "Ishlab chiqarish rejalashtirish",
                ModuleCode = "ProductPlan",
                ContentHtml = content.ToString(),
                Order = 6
            };
        }

        private static UserManualChapterViewModel CreateFinanceChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Moliya moduli</h2>");
            content.AppendLine("<p>Moliya moduli <strong>hisoblar (accounts)</strong>, <strong>tranzaksiyalar</strong>, <strong>balans</strong>, <strong>P&amp;L</strong> va boshqa moliyaviy hisobotlarni boshqarish uchun mo'ljallangan. U sotuv, xarid, ombor va ishlab chiqarish modullari bilan bevosita bog'langan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Moliya</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-29: Moliya bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Hisoblar (FinAccounts) – kassa/bank va boshqa hisoblar</h3>");
            content.AppendLine("<p>Hisoblar – barcha kassa, bank va boshqa moliyaviy hisoblarni ifodalaydi. Har bir tranzaksiya albatta biror hisob bilan bog'langan bo'ladi.</p>");
            content.AppendLine("<h4>1.1. Yangi hisob yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Moliya → Hisoblar (FinAccounts) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Kod (Code), nom (Name), valyuta (Currency), turi (kassa/bank/other) va boshqa maydonlarni kiriting</li>");
            content.AppendLine("<li>Agar kerak bo'lsa, balans kategoriyasini tanlang (BalanceCategory)</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-30: Hisoblar ro'yxati</strong></p>");

            content.AppendLine("<h4>1.2. Hisobni ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> hisob kartasida balans, valyuta, bog'langan tranzaksiyalar soni va boshqa rekvizitlar ko'rinadi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> kod, nom, valyuta va kategoriya faqat shu hisob bilan ishlaydigan muhim tranzaksiyalar bo'lmasa ehtiyotkorlik bilan o'zgartiriladi.</li>");
            content.AppendLine("<li><strong>Delete (soft):</strong> \"o'chirish\" amali odatda <code>IsActive = false</code> orqali amalga oshiriladi – hisob tarixda qoladi, lekin yangi tranzaksiyalar uchun tanlab bo'lmaydi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>2. Tranzaksiyalar (FinTransactions) – kirim/chiqim va o'tkazmalar</h3>");
            content.AppendLine("<p>Tranzaksiyalar – barcha pul harakatlari: kassaga/tomondan tushum, bank kirim-chiqimlari, hisoblar orasidagi o'tkazmalar va boshqa moliyaviy operatsiyalar.</p>");
            content.AppendLine("<h4>2.1. Qo'lda tranzaksiya yaratish (manual Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Moliya → Tranzaksiyalar (FinTransactions) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Hisobni tanlang (kassa/bank yoki boshqa)</li>");
            content.AppendLine("<li>Tranzaksiya turini tanlang (kirim, chiqim, o'tkazma va h.k.)</li>");
            content.AppendLine("<li>Summani, valyutani, kursni (agar kerak bo'lsa) va sana/izohni kiriting</li>");
            content.AppendLine("<li>Agar kerak bo'lsa, kontragent (mijoz/ta'minotchi) va hujjat raqamini bog'lang</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-31: Tranzaksiya yaratish</strong></p>");

            content.AppendLine("<h4>2.2. Boshqa modullardan avtomatik yaratiladigan tranzaksiyalar:</h4>");
            content.AppendLine("<p>Ko'plab tranzaksiyalar qo'lda emas, balki quyidagi modullardan <strong>avtomatik</strong> yaratiladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>SalesInvoices / SalesSF:</strong> mijozdan tushgan to'lov kiritilganda yoki qarzdorlik yopilganda tegishli hisob bo'yicha tranzaksiya paydo bo'ladi.</li>");
            content.AppendLine("<li><strong>PInvoices:</strong> ta'minotchiga to'lov qilganda yoki qarzdorlik yopilganda bank/kassa bo'yicha chiqim tranzaksiyalari shakllanadi.</li>");
            content.AppendLine("<li><strong>OpeningBalance:</strong> boshlang'ich qoldiqlarni kiritishda ham FinTransactions orqali balansga boshlang'ich yozuvlar kiritiladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>2.3. Tranzaksiyani ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> har bir tranzaksiya kartasida hisob, summa, sana, kontragent va bog'langan hujjat (invoys, order) ko'rinadi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> faqat hali davr yopilmagan va hisobotlarga tasdiqlanmagan davrlar uchun ayrim maydonlarni tahrirlashga ruxsat beriladi (masalan, izoh yoki sana).</li>");
            content.AppendLine("<li><strong>Delete/Reverse:</strong> ko'pincha tranzaksiyani to'liq o'chirish o'rniga teskari yozuv (reverse entry) qilish tavsiya etiladi, shunda audit izlari (log) saqlanib qoladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>3. Balans, P&amp;L va boshqa hisobotlar (FinDashboard)</h3>");
            content.AppendLine("<p>Moliya modulida barcha asosiy hisobotlar <strong>FinDashboard</strong> bo'limida jamlangan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Financial Overview</strong> – umumiy moliyaviy ko'rinish (view: <code>Views/FinDashboard/FinancialOverview.cshtml</code>).</li>");
            content.AppendLine("<li><strong>Cashflow</strong> – pul oqimlari hisoboti (view: <code>Views/FinDashboard/Cashflow.cshtml</code>).</li>");
            content.AppendLine("<li><strong>PL (Profit &amp; Loss)</strong> – foyda va zarar hisobotlari (view: <code>Views/FinDashboard/PL.cshtml</code>).</li>");
            content.AppendLine("<li><strong>Balance Sheet</strong> – balans (view: <code>Views/FinDashboard/BalanceSheet.cshtml</code> va qo'shimcha detallash sahifalari).</li>");
            content.AppendLine("<li><strong>Account Balances</strong> – hisoblar bo'yicha joriy qoldiqlar.</li>");
            content.AppendLine("<li><strong>Account Analytics</strong>, <strong>Category Analytics</strong>, <strong>Income/Expense Analysis</strong> – daromad va xarajatlarni chuqur tahlil qilish.</li>");
            content.AppendLine("<li><strong>Liabilities Dashboard</strong> – kreditor va debitor qarzdorliklari bo'yicha ko'rinish.</li>");
            content.AppendLine("<li><strong>Shareholder Value</strong> – aksiyadorlar qiymati va daromadliligi (view: <code>Views/FinDashboard/ShareholderValue.cshtml</code>).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Bu hisobotlarga menyudan odatda <em>Finance → Dashboard</em> yoki <em>Reports → Finance</em> orqali kiriladi. Har bir sahifada sana oralig'i, hisob, kategoriya va boshqa filtrlardan foydalanib oylik yoki davriy tahlil qilish mumkin.</p>");

            content.AppendLine("<h3>4. P&amp;L va balans kategoriyalari (FinPlItems, FinPlPlans, FinBalanceCategories)</h3>");
            content.AppendLine("<p>P&amp;L va balans hisobotlari quyidagi ma'lumotomalar yordamida moslashtiriladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>FinPlItems:</strong> daromad va xarajat moddalari (sotuv daromadi, xomashyo xarajatlari, ish haqi, soliq va h.k.).</li>");
            content.AppendLine("<li><strong>FinPlPlans:</strong> P&amp;L bo'yicha reja (oylar/kvartallar kesimida rejalashtirilgan daromad va xarajatlar).</li>");
            content.AppendLine("<li><strong>FinBalanceCategories:</strong> balans bo'yicha aktiv/passiv bo'limlar (asosiy vositalar, aylanma aktivlar, qisqa muddatli majburiyatlar va h.k.).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Ularning CRUD ishlashi ham standart: ma'lumotnomani yaratish, ko'rish, tahrirlash va (soft) o'chirish. Keyinchalik P&amp;L va balans hisobotlari aynan shu tasniflar asosida shakllanadi.</p>");

            content.AppendLine("<h3>5. Boshlang'ich balans (OpeningBalance)</h3>");
            content.AppendLine("<p>OpeningBalance moduli – tizimni ishga tushirishda yoki yangi yil boshida <strong>boshlang'ich qoldiqlarni</strong> kiritish uchun ishlatiladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Hisoblar bo'yicha boshlang'ich balans;</li>");
            content.AppendLine("<li>Kontragentlar (debitor/kreditorlar) bo'yicha qarzdorlik;</li>");
            content.AppendLine("<li>Ombor qoldiqlari (Inventory) bilan bog'liq moliyaviy qoldiqlar.</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Bu jarayon bir martalik bo'lib, keyinchalik oddiy FinTransactions va boshqa modullar orqali odatdagi operatsiyalar yuritiladi.</p>");

            content.AppendLine("<h3>6. Aksiyadorlar (Shareholders) va ShareholderValue</h3>");
            content.AppendLine("<p>Aksiyadorlar bo'limida kompaniya aksiyadorlari, ularning ulushi va dividend siyosati boshqariladi. <strong>ShareholderValue</strong> hisobotida kompaniya qiymati va aksiyadorlar ulushi bo'yicha tahlil ko'rsatiladi.</p>");
            content.AppendLine("<p><strong>Rasm-33: Aksiyadorlar ro'yxati</strong></p>");

            return new UserManualChapterViewModel
            {
                Id = "finance",
                Title = "Moliya moduli",
                ModuleCode = "FinAccounts",
                ContentHtml = content.ToString(),
                Order = 7
            };
        }

        private static UserManualChapterViewModel CreateHRChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Kadrlar boshqaruvi (HR)</h2>");
            content.AppendLine("<p>HR moduli tashkiliy struktura, xodimlar, lavozimlar, tariflar, tabel va ish haqini boshqarish uchun mo'ljallangan. U moliya, ishlab chiqarish va tannarx modullari bilan uzviy bog'langan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Kadrlar (HR)</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-34: HR bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Tashkiliy struktura (HrOrgUnits) – bo'limlar va sexlar</h3>");
            content.AppendLine("<p>Tashkiliy struktura – kompaniya bo'limlari, sexlar, uchastkalar va boshqa org. birliklarni ifodalaydi. Har bir xodim aynan bir org. birlikka biriktiriladi.</p>");
            content.AppendLine("<h4>1.1. Yangi org. birlik yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Kadrlar → Tashkiliy struktura (HrOrgUnits) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Bo'lim/seksiyaning nomi, qisqa kodi va yuqori turuvchi bo'limni (Parent) tanlang</li>");
            content.AppendLine("<li>Aktivligini belgilang va kerak bo'lsa tartib raqamini kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-35: Tashkiliy struktura</strong></p>");

            content.AppendLine("<h4>1.2. Org. birlikni ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> daraxt ko'rinishida barcha bo'limlar va ularning ichki tuzilishi ko'rinadi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> nom, kod va parent bo'lim o'zgartirilishi mumkin (ehtiyotkorlik bilan – bu xodimlar va rejalarga ta'sir qiladi).</li>");
            content.AppendLine("<li><strong>Delete/Deactivate:</strong> odatda <em>soft delete</em> qo'llaniladi – bo'lim ishlatilmasa deaktiv qilinadi, lekin tarixda saqlanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>2. Lavozimlar (HrPositions) va darajalar (HrSalaryGrades)</h3>");
            content.AppendLine("<p>Lavozimlar – xodimning rasmiy pozitsiyasi (masalan, operator, ustaxona boshlig'i, bo'lim rahbari). SalaryGrades – maosh diapazonlari va tarif toifalarini ifodalaydi.</p>");
            content.AppendLine("<h4>2.1. Lavozimlar CRUD (HrPositions):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Kadrlar → Lavozimlar (HrPositions) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" orqali yangi lavozim nomi, kodi, bog'liq org. birlik (default) va boshqa parametrlarni kiriting</li>");
            content.AppendLine("<li>Saqlang; keyinchalik xodim kartasida shu lavozim tanlanadi</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p>Tahrirlash va o'chirish (deaktiv qilish) jarayoni ham standart CRUD tamoyiliga asoslangan.</p>");

            content.AppendLine("<h4>2.2. Tarif darajalari CRUD (HrSalaryGrades):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Kadrlar → Tarif darajalari (HrSalaryGrades) bo'limiga o'ting</li>");
            content.AppendLine("<li>Yangi daraja yaratishda kod, nom va maosh diapazonini (min/max) kiriting</li>");
            content.AppendLine("<li>Agar soatbay yoki akkord tizimi ishlatilsa, shu yerda qo'shimcha koeffitsientlar ham belgilanadi</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h3>3. Xodimlar (HrEmployees) – personal kartalari</h3>");
            content.AppendLine("<p>Xodimlar bo'limi kompaniyadagi barcha ishchilar ro'yxatini saqlaydi. Har bir kartada shaxsiy ma'lumotlar, org. birlik, lavozim, tarif darajasi va boshqa HR ma'lumotlar bor.</p>");
            content.AppendLine("<h4>3.1. Yangi xodim yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Kadrlar → Xodimlar (HrEmployees) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>F.I.Sh., tug'ilgan sana, shaxsiy ma'lumotlar va kontaktlarni kiriting</li>");
            content.AppendLine("<li>Bo'lim (OrgUnit) va lavozimni (Position) tanlang</li>");
            content.AppendLine("<li>Tarif darajasi yoki ish haqi turi (oylik/soatbay/akkord) ni belgilang</li>");
            content.AppendLine("<li>Agar kerak bo'lsa, ishga qabul sanasi va shartnoma turini kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-36: Xodim yaratish</strong></p>");

            content.AppendLine("<h4>3.2. Xodimni ko'rish, tahrirlash, o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> xodim kartasini ochganda barcha ish tarixi, org. birlik va lavozim o'zgarishlari ko'rinadi (HrEmployeeHistory bilan bog'liq).</li>");
            content.AppendLine("<li><strong>Edit:</strong> lavozim, bo'lim, tarif darajasi, ish shartlari o'zgarishi HR bo'limi tomonidan xodim kartasida yangilanadi.</li>");
            content.AppendLine("<li><strong>Dismiss/Deactivate:</strong> xodim ishdan bo'shatilganda status o'zgartiriladi va \"Ishdan bo'shash sanasi\" kiritiladi, lekin kartasi tarixda saqlanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>4. Tabel (HrTimesheets) – ish vaqti hisobini kiritish, tahrirlash, o'chirish</h3>");
            content.AppendLine("<p>Tabel (HrTimesheets) – xodimlar bo'yicha <strong>kunlik/soatlik davomad</strong> va ishlangan vaqtni hisoblash uchun ishlatiladi. Tabel ma'lumotlari ish haqi va tannarx hisobi uchun asos bo'ladi.</p>");
            content.AppendLine("<h4>4.1. Tabel yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Kadrlar → Tabel (HrTimesheets) bo'limiga o'ting</li>");
            content.AppendLine("<li>Davrni tanlang (oy va yil)</li>");
            content.AppendLine("<li>Bo'lim yoki sexni tanlang, kerak bo'lsa filtrlar qo'llang</li>");
            content.AppendLine("<li>\"Tabel yaratish\" yoki \"Generatsiya qilish\" tugmasini bosib, xodimlar ro'yxati uchun dastlabki jadvalni hosil qiling</li>");
            content.AppendLine("<li>Har bir xodim uchun ish kuni/soatlari, ta'til, kasallik va boshqa holatlarni kiritib chiqing</li>");
            content.AppendLine("<li>\"Saqlash\" yoki \"Tasdiqlash\" tugmasini bosing (tasdiqlangandan so'ng ba'zi tizimlarda tahrirlash cheklanadi)</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-37: Tabel</strong></p>");

            content.AppendLine("<h4>4.2. Tabelni tahrirlash (Edit) va o'chirish (Delete/Cancel):</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Edit:</strong> tasdiqlanmagan davrlar uchun xodim bo'yicha ish soatlari va kunlari qayta kiritilishi mumkin.</li>");
            content.AppendLine("<li><strong>Qisman o'chirish:</strong> alohida xodim bo'yicha qatorni tozalash yoki ma'lum kunlar uchun qiymatlarni 0 ga tenglashtirish.</li>");
            content.AppendLine("<li><strong>Tabel davrini bekor qilish:</strong> agar butun oy bo'yicha tabel noto'g'ri tuzilgan bo'lsa, davr bo'yicha bekor qilish (cancel) funksiyasi ishlatilishi mumkin.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>5. Ish haqi (HrPayroll, HrPieceRateTariffs, HrPieceRateTimesheets)</h3>");
            content.AppendLine("<p>Ish haqi moduli tabel, tarif va qo'shimcha parametrlar asosida <strong>oylik ish haqi</strong>ni hisoblaydi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>HrPieceRateTariffs:</strong> akkord/bo'lak ishlari bo'yicha tariflar (bitta mahsulot yoki ish birligi uchun to'lov).</li>");
            content.AppendLine("<li><strong>HrPieceRateTimesheets:</strong> akkord ishlari bo'yicha bajarilgan ish miqdori (qty × tariff).</li>");
            content.AppendLine("<li><strong>HrPayroll:</strong> umumiy ish haqi hisob-kitobi – oylik maosh, akkord, ustamalar, chegirmalar va h.k.</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Ish haqi hisoblashda jarayon quyidagicha:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Tabel (HrTimesheets) va akkord tabeli (HrPieceRateTimesheets) tasdiqlanadi;</li>");
            content.AppendLine("<li>Tariflar (HrSalaryGrades va HrPieceRateTariffs) asosida xodimlar uchun brutto ish haqi hisoblanadi;</li>");
            content.AppendLine("<li>Solig'lar va ushlanmalar qo'llaniladi;</li>");
            content.AppendLine("<li>Natija HrPayroll hisobotlari va Moliya moduli bilan bog'lanadi.</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-38: Ish haqi</strong></p>");

            return new UserManualChapterViewModel
            {
                Id = "hr",
                Title = "Kadrlar boshqaruvi (HR)",
                ModuleCode = "HrEmployees",
                ContentHtml = content.ToString(),
                Order = 8
            };
        }

        private static UserManualChapterViewModel CreateSettingsChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Sozlamalar</h2>");
            content.AppendLine("<p>Sozlamalar bo'limida tizimning asosiy ma'lumotlari va reference data boshqariladi.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Sozlamalar</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-39: Sozlamalar bo'limi</strong></p>");

            content.AppendLine("<h3>1. Korxona (Company)</h3>");
            content.AppendLine("<p>Korxona - kompaniya asosiy ma'lumotlari.</p>");
            content.AppendLine("<p><strong>Rasm-40: Korxona sozlamalari</strong></p>");

            content.AppendLine("<h3>2. O'lchov birliklari (Units)</h3>");
            content.AppendLine("<p>O'lchov birliklari - mahsulotlar uchun ishlatiladigan o'lchov birliklari (kg, tonna, dona va hokazo).</p>");
            content.AppendLine("<h4>Yangi o'lchov birligi yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Sozlamalar → O'lchov birliklari bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Birlik nomi va qisqartmasini kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h3>3. Valyutalar (Currencies)</h3>");
            content.AppendLine("<p>Valyutalar - tizimda ishlatiladigan valyutalar (UZS, EUR va hokazo).</p>");
            content.AppendLine("<p><strong>Rasm-41: Valyutalar ro'yxati</strong></p>");

            content.AppendLine("<h3>4. Maxsulotlar (Products)</h3>");
            content.AppendLine("<p>Maxsulotlar - kompaniya ishlab chiqaradigan yoki sotadigan barcha mahsulotlar.</p>");
            content.AppendLine("<p>Maxsulotlar Engineering → Maxsulotlar bo'limidan boshqariladi.</p>");
            content.AppendLine("<p><strong>Rasm-42: Maxsulotlar ro'yxati</strong></p>");

            return new UserManualChapterViewModel
            {
                Id = "settings",
                Title = "Sozlamalar",
                ModuleCode = "Settings",
                ContentHtml = content.ToString(),
                Order = 9
            };
        }

        private static UserManualChapterViewModel CreateEngineeringChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Engineering / Muhandislik</h2>");
            content.AppendLine("<p>Engineering moduli mahsulotlar, BOM va norma-sarflarni boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Engineering</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-43: Engineering bo'limi</strong></p>");

            content.AppendLine("<h3>1. Maxsulotlar (Products)</h3>");
            content.AppendLine("<p>Maxsulotlar - barcha mahsulotlar, yarim tayyor mahsulotlar va xomashyolar.</p>");
            content.AppendLine("<h4>Yangi maxsulot yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Engineering → Maxsulotlar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Maxsulot nomi, kodi va boshqa ma'lumotlarni kiriting</li>");
            content.AppendLine("<li>Maxsulot turini tanlang (Tayyor maxsulot, Yarim tayyor, Xomashyo)</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-44: Maxsulot yaratish</strong></p>");

            content.AppendLine("<h3>2. BOM (Bill of Materials)</h3>");
            content.AppendLine("<p>BOM - mahsulot tarkibi, ya'ni bir mahsulotni ishlab chiqarish uchun qanday materiallar kerakligi.</p>");
            content.AppendLine("<h4>BOM yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Engineering → BOM bo'limiga o'ting</li>");
            content.AppendLine("<li>Asosiy mahsulotni tanlang</li>");
            content.AppendLine("<li>BOM qatorlarini qo'shing (materiallar va miqdorlar)</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-45: BOM yaratish</strong></p>");

            return new UserManualChapterViewModel
            {
                Id = "engineering",
                Title = "Engineering / Muhandislik",
                ModuleCode = "BOM",
                ContentHtml = content.ToString(),
                Order = 10
            };
        }

        private static UserManualChapterViewModel CreateLogisticsChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Logistika</h2>");
            content.AppendLine("<p>Logistika moduli yuk tashish, forwarderlar, transport shartnomalari va yo'l varaqalarini boshqarish uchun mo'ljallangan. Bu modul sotuv, ombor va moliya bilan uzviy bog'langan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Logistika</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-46: Logistika bo'limi</strong></p>");

            content.AppendLine("<h3>1. Forwarderlar – tashuvchilar ma'lumotnomasi (Forwarder)</h3>");
            content.AppendLine("<p>Forwarderlar – yuk tashish xizmatini ko'rsatuvchi kompaniya yoki yakka tartibdagi tashuvchilar. Ular bilan shartnomalar tuziladi va yo'l varaqalarida ishlatiladi.</p>");
            content.AppendLine("<h4>1.1. Yangi forwarder yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Logistika → Forwarderlar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Kompaniya nomi, STIR, manzil, aloqa ma'lumotlarini kiriting</li>");
            content.AppendLine("<li>Valyuta, xizmat turi (ichki/tashqi), transport turlari (avto, temir yo'l va h.k.) ni tanlang</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-47: Forwarderlar ro'yxati</strong></p>");

            content.AppendLine("<h4>1.2. Forwarderni ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> forwarder kartasida barcha shartlar va aloqa ma'lumotlari ko'rsatiladi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> tariflar o'zgarsa yoki aloqa ma'lumotlari yangilansa, forwarder kartasi tahrirlanadi.</li>");
            content.AppendLine("<li><strong>Delete:</strong> odatda soft delete – endi foydalanilmaydigan forwarderlar filtrdan chiqariladi, lekin tarix uchun saqlanadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>2. Forwarder shartnomalari (FContract va FContractTariff)</h3>");
            content.AppendLine("<p>Forwarder shartnomalari – aniq forwarder bilan tuzilgan transport shartnomalari, unda tariflar, marshrutlar, xizmat turlari va boshqa shartlar belgilanadi.</p>");
            content.AppendLine("<h4>2.1. Shartnoma yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Logistika → Forwarder shartnomalari (FContract) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Forwarderni tanlang</li>");
            content.AppendLine("<li>Shartnoma raqami, sanasi, amal qilish muddati va valyutani kiriting</li>");
            content.AppendLine("<li>Agar mavjud bo'lsa, tariflar bo'limida (FContractTariff) marshrut, masofa, birlik narxi kabi ma'lumotlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-48: Forwarder shartnomalari</strong></p>");

            content.AppendLine("<h4>2.2. Ko'rish, tahrirlash, o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> shartnoma kartasida tariflar, forwarder va amal qilish muddati ko'rinadi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> amal qilish muddati uzaytirilishi yoki yangi tariflar qo'shilishi mumkin.</li>");
            content.AppendLine("<li><strong>Delete/Deactivate:</strong> muddati tugagan yoki ishlatilmaydigan shartnomalar deaktiv qilinadi; ular tarixiy ma'lumot sifatida qoladi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>3. Yo'l varaqalari (Waybill) – TTNK va transport hujjatlari</h3>");
            content.AppendLine("<p>Yo'l varaqalari (Waybill) – mahsulotni ombordan tashqi manzilga yoki mijozga yetkazishda rasmiylashtiriladigan transport hujjati. U TTNK (SalesTTNK) va sotuv hujjatlari bilan bog'liq.</p>");
            content.AppendLine("<h4>3.1. Yo'l varaqasini yaratish (Create):</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Logistika → Yo'l varaqalari (Waybill) bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Mijoz yoki qabul qiluvchini tanlang (ko'pincha SalesOrder/Invoice bilan bog'liq)</li>");
            content.AppendLine("<li>Forwarder va transport vositasini tanlang (mashina raqami, haydovchi)</li>");
            content.AppendLine("<li>Yuklanadigan mahsulotlar va miqdorlarni kiriting yoki invoysdan avtomatik torting</li>");
            content.AppendLine("<li>Chiqish ombori va yetkazib berish manzilini belgilang</li>");
            content.AppendLine("<li>\"Saqlash\" va agar kerak bo'lsa \"Tasdiqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-49: Yo'l varaqalari</strong></p>");

            content.AppendLine("<h4>3.2. Ombor bilan bog'lanishi va avtomatik chiqim:</h4>");
            content.AppendLine("<p>Yo'l varaqasi tasdiqlanganda odatda quyidagi jarayonlar ishlaydi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Tegishli omborda <strong>Expense</strong> (chiqim) hujjati yaratiladi – yuklangan mahsulotlar miqdori bo'yicha;</li>");
            content.AppendLine("<li>Agar logistika jarayoni boshqa oraliq omborga yetkazishni o'z ichiga olsa, qabul qiluvchi ombordagi kirim ham avtomatik shakllanishi mumkin;</li>");
            content.AppendLine("<li>Waybill holatlari (Draft, Approved, In Transit, Delivered) bo'yicha monitoring qilinadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>3.3. Yo'l varaqasini ko'rish, tahrirlash va o'chirish:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>View:</strong> marshrut, yuklangan mahsulotlar, forwarder va transport ma'lumotlari to'liq ko'rinadi.</li>");
            content.AppendLine("<li><strong>Edit:</strong> faqat tasdiqlanmagan (Draft) holatdagi yo'l varaqalarida ma'lumotlar tahrirlanadi.</li>");
            content.AppendLine("<li><strong>Cancel/Delete:</strong> noto'g'ri rasmiylashtirilgan yo'l varaqalari bekor qilinadi, bunda unga bog'liq ombor chiqim hujjatlari ham teskari yozuv bilan neytrallashadi.</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h3>4. Logistika va boshqa modullar o'zaro aloqasi</h3>");
            content.AppendLine("<p>Logistika funksiyalari quyidagi modullar bilan bevosita bog'langan:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>CRM/Sales:</strong> SalesOrder, SalesInvoice va SalesTTNK asosida Waybill yaratiladi.</li>");
            content.AppendLine("<li><strong>Warehouse:</strong> Waybill tasdiqlanganda omborda chiqim (Expense) yoziladi.</li>");
            content.AppendLine("<li><strong>Finance:</strong> transport xarajatlari FinTransactions va Tannarx hisoblarida e'tiborga olinadi (xarajat moddasi sifatida).</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>Natijada butun yetkazib berish zanjiri – buyurtmadan tortib mijozga yetkazish va transport tannarxigacha bo'lgan jarayon boshqariladi.</p>");

            return new UserManualChapterViewModel
            {
                Id = "logistics",
                Title = "Logistika",
                ModuleCode = "Forwarder",
                ContentHtml = content.ToString(),
                Order = 11
            };
        }

        private static UserManualChapterViewModel CreateTasksChapter()
        {
            var content = new StringBuilder();
            content.AppendLine("<h2>Vazifalar boshqaruvi (Tasks)</h2>");
            content.AppendLine("<p>Vazifalar moduli loyihalar va vazifalarni boshqarish, vazifalarni tayinlash va kuzatish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Tasks / Vazifalar</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-50: Vazifalar bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Vazifalar ro'yxati</h3>");
            content.AppendLine("<p>Vazifalar ro'yxatida barcha vazifalarni ko'rishingiz mumkin. Har bir vazifa quyidagi ma'lumotlarga ega:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>KeyCode</strong> - vazifa kodi (masalan: TASK-001)</li>");
            content.AppendLine("<li><strong>Title</strong> - vazifa nomi</li>");
            content.AppendLine("<li><strong>Description</strong> - vazifa tavsifi</li>");
            content.AppendLine("<li><strong>Project</strong> - loyiha</li>");
            content.AppendLine("<li><strong>Status</strong> - vazifa holati (Yangi, Jarayonda, Tugallangan va hokazo)</li>");
            content.AppendLine("<li><strong>Priority</strong> - vazifa ustuvorligi (Past, O'rta, Yuqori, Kritik)</li>");
            content.AppendLine("<li><strong>Assignee</strong> - vazifa tayinlangan foydalanuvchi</li>");
            content.AppendLine("<li><strong>Reporter</strong> - vazifani yaratgan foydalanuvchi</li>");
            content.AppendLine("<li><strong>DueDate</strong> - vazifa muddati</li>");
            content.AppendLine("<li><strong>CreatedDT</strong> - yaratilgan sana</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Rasm-51: Vazifalar ro'yxati</strong></p>");

            content.AppendLine("<h3>2. Yangi vazifa yaratish</h3>");
            content.AppendLine("<p>Yangi vazifa yaratish uchun quyidagi qadamlarni bajaring:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Vazifalar bo'limiga o'ting</li>");
            content.AppendLine("<li>Yuqoridagi \"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Formada quyidagi maydonlarni to'ldiring:");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Loyiha (Project)</strong> - vazifa qaysi loyihaga tegishli ekanligini tanlang</li>");
            content.AppendLine("<li><strong>Vazifa nomi (Title)</strong> - vazifaning qisqa nomi</li>");
            content.AppendLine("<li><strong>Tavsif (Description)</strong> - vazifaning batafsil tavsifi</li>");
            content.AppendLine("<li><strong>Holat (Status)</strong> - vazifa holati (odatda \"Yangi\" bilan boshlanadi)</li>");
            content.AppendLine("<li><strong>Ustuvorlik (Priority)</strong> - vazifa ustuvorligi</li>");
            content.AppendLine("<li><strong>Tayinlangan (Assignee)</strong> - vazifani kimga tayinlayotganingizni tanlang</li>");
            content.AppendLine("<li><strong>Muddati (DueDate)</strong> - vazifa tugash sanasi</li>");
            content.AppendLine("</ul></li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-52: Vazifa yaratish formasi</strong></p>");

            content.AppendLine("<h3>3. Mavjud vazifani tahrirlash</h3>");
            content.AppendLine("<p>Vazifani tahrirlash uchun:</p>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Vazifalar ro'yxatidan tahrirlash kerak bo'lgan vazifani toping</li>");
            content.AppendLine("<li>Vazifa ustiga bosing yoki \"Tahrirlash\" tugmasini bosing</li>");
            content.AppendLine("<li>Ma'lumotlarni o'zgartiring (holat, ustuvorlik, tayinlangan shaxs va hokazo)</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-53: Vazifa tahrirlash</strong></p>");

            content.AppendLine("<h3>4. Vazifalarni filtrlash va qidirish</h3>");
            content.AppendLine("<p>Vazifalar ro'yxatida quyidagi filtrlardan foydalanishingiz mumkin:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Tayinlangan shaxs</strong> - faqat sizga tayinlangan vazifalarni ko'rish</li>");
            content.AppendLine("<li><strong>Holat</strong> - vazifa holati bo'yicha filtr (Yangi, Jarayonda, Tugallangan va hokazo)</li>");
            content.AppendLine("<li><strong>Ustuvorlik</strong> - vazifa ustuvorligi bo'yicha filtr</li>");
            content.AppendLine("<li><strong>Muddati</strong> - vazifa muddati bo'yicha sana oralig'i</li>");
            content.AppendLine("<li><strong>Qidiruv</strong> - vazifa nomi, tavsifi yoki kodi bo'yicha qidirish</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Rasm-54: Vazifalar filtrlari</strong></p>");

            content.AppendLine("<h3>5. Vazifa holatlari</h3>");
            content.AppendLine("<p>Vazifalar quyidagi holatlarda bo'lishi mumkin:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Yangi (New)</strong> - yangi yaratilgan vazifa</li>");
            content.AppendLine("<li><strong>Jarayonda (In Progress)</strong> - ish olib borilayotgan vazifa</li>");
            content.AppendLine("<li><strong>Kutilmoqda (Pending)</strong> - kutish holatida bo'lgan vazifa</li>");
            content.AppendLine("<li><strong>Tugallangan (Completed)</strong> - bajarilgan vazifa</li>");
            content.AppendLine("<li><strong>Bekor qilingan (Cancelled)</strong> - bekor qilingan vazifa</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>❗ Bu yerda tizimning aniq holatlar ro'yxati bo'yicha qo'shimcha aniqlik kiritish kerak (TODO).</p>");

            content.AppendLine("<h3>6. Vazifa ustuvorliklari</h3>");
            content.AppendLine("<p>Har bir vazifaga ustuvorlik beriladi:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Past (Low)</strong> - past ustuvorlik</li>");
            content.AppendLine("<li><strong>O'rta (Medium)</strong> - o'rta ustuvorlik</li>");
            content.AppendLine("<li><strong>Yuqori (High)</strong> - yuqori ustuvorlik</li>");
            content.AppendLine("<li><strong>Kritik (Critical)</strong> - eng yuqori ustuvorlik</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p>❗ Bu yerda tizimning aniq ustuvorliklar ro'yxati bo'yicha qo'shimcha aniqlik kiritish kerak (TODO).</p>");

            content.AppendLine("<h3>7. Loyihalar (Projects)</h3>");
            content.AppendLine("<p>Vazifalar loyihalar bo'yicha guruhlanadi. Har bir loyiha bir nechta vazifalarni o'z ichiga olishi mumkin.</p>");
            content.AppendLine("<p>Loyihalar vazifa yaratish yoki tahrirlash paytida tanlanadi.</p>");
            content.AppendLine("<p><strong>Rasm-55: Loyihalar ro'yxati</strong></p>");

            return new UserManualChapterViewModel
            {
                Id = "tasks",
                Title = "Vazifalar boshqaruvi (Tasks)",
                ModuleCode = "Tasks",
                ContentHtml = content.ToString(),
                Order = 12
            };
        }
    }
}
