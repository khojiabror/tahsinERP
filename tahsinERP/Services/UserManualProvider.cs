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
        public static UserManualPageViewModel GetManual()
        {
            var chapters = new List<UserManualChapterViewModel>();

            // 1. Kirish (Introduction)
            chapters.Add(CreateIntroductionChapter());

            // 2. Foydalanuvchilar, rollar va ruxsatlar (VERY DETAILED)
            chapters.Add(CreatePermissionsChapter());

            // 3. Sales / Sotuvlar
            chapters.Add(CreateSalesChapter());

            // 4. Purchase / Xarid
            chapters.Add(CreatePurchaseChapter());

            // 5. Warehouse / Omborxona
            chapters.Add(CreateWarehouseChapter());

            // 6. Production / Ishlab chiqarish
            chapters.Add(CreateProductionChapter());

            // 7. Finance / Moliya
            chapters.Add(CreateFinanceChapter());

            // 8. HR / Kadrlar
            chapters.Add(CreateHRChapter());

            // 9. Settings / Sozlamalar
            chapters.Add(CreateSettingsChapter());

            // 10. Engineering / Muhandislik
            chapters.Add(CreateEngineeringChapter());

            // 11. Logistics / Logistika
            chapters.Add(CreateLogisticsChapter());

            // 12. Tasks / Vazifalar
            chapters.Add(CreateTasksChapter());

            return new UserManualPageViewModel
            {
                Chapters = chapters.OrderBy(c => c.Order).ToList(),
                InitialChapterId = chapters.First().Id
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

            content.AppendLine("<h3>1. Xaridorlar (Customers)</h3>");
            content.AppendLine("<p>Xaridorlar - bu sizning mahsulotlaringizni sotib oladigan mijozlar.</p>");
            content.AppendLine("<h4>Yangi xaridor yaratish:</h4>");
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

            content.AppendLine("<h3>2. Sotuv buyurtmalari (SOrder)</h3>");
            content.AppendLine("<p>Sotuv buyurtmalari - mijozlar tomonidan berilgan buyurtmalar.</p>");
            content.AppendLine("<h4>Asosiy maydonlar:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>OrderNo</strong> - buyurtma raqami</li>");
            content.AppendLine("<li><strong>Customer</strong> - xaridor</li>");
            content.AppendLine("<li><strong>Contract</strong> - shartnoma (ixtiyoriy)</li>");
            content.AppendLine("<li><strong>IssuedDate</strong> - buyurtma sanasi</li>");
            content.AppendLine("<li><strong>Products</strong> - buyurtma mahsulotlari</li>");
            content.AppendLine("<li><strong>Amount</strong> - jami summa</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>Yangi buyurtma yaratish:</h4>");
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

            content.AppendLine("<h4>Mavjud buyurtmani tahrirlash:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Buyurtmalar ro'yxatidan tahrirlash kerak bo'lgan buyurtmani toping</li>");
            content.AppendLine("<li>Buyurtma ustiga bosing yoki \"Tahrirlash\" tugmasini bosing</li>");
            content.AppendLine("<li>Ma'lumotlarni o'zgartiring</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h4>Qidirish va filtr:</h4>");
            content.AppendLine("<p>Buyurtmalar ro'yxatida quyidagi filtrlardan foydalanishingiz mumkin:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li>Buyurtma raqami bo'yicha qidirish</li>");
            content.AppendLine("<li>Xaridor bo'yicha filtr</li>");
            content.AppendLine("<li>Sana oralig'i bo'yicha filtr</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Rasm-10: Buyurtmalar ro'yxati va filtrlari</strong></p>");

            content.AppendLine("<h3>3. Sotuv shartnomalari (SContract)</h3>");
            content.AppendLine("<p>Shartnomalar - xaridorlar bilan tuzilgan rasmiy shartnomalar.</p>");
            content.AppendLine("<p>Shartnoma yaratish va boshqarish jarayoni buyurtmalarga o'xshash.</p>");
            content.AppendLine("<p><strong>Rasm-11: Shartnomalar ro'yxati</strong></p>");

            content.AppendLine("<h3>4. Sotuv invoyslari (SalesInvoices)</h3>");
            content.AppendLine("<p>Sotuv invoyslari - mijozlarga berilgan hisob-fakturalar.</p>");
            content.AppendLine("<h4>Yangi invoys yaratish:</h4>");
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

            content.AppendLine("<h3>5. Счёт-фактура (SalesSF)</h3>");
            content.AppendLine("<p>Счёт-фактура - soliq to'lovchilar uchun rasmiy hujjat.</p>");
            content.AppendLine("<p>Счёт-фактура invoysdan yaratiladi va soliq organlariga yuboriladi.</p>");
            content.AppendLine("<h4>Счёт-фактура yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Sotuvlar → Счёт-фактура bo'limiga o'ting</li>");
            content.AppendLine("<li>Invoysni tanlang</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Ma'lumotlarni to'ldiring va \"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-13: Счёт-фактура yaratish</strong></p>");

            content.AppendLine("<h3>6. ТТНК (SalesTTNK)</h3>");
            content.AppendLine("<p>ТТНК (Transportno-Tovarno-Nakladnaya Karta) - yuk tashish hujjati.</p>");
            content.AppendLine("<p>ТТНК invoysdan yaratiladi va yuk tashish uchun ishlatiladi.</p>");
            content.AppendLine("<p><strong>Rasm-14: ТТНК yaratish</strong></p>");

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

            content.AppendLine("<h3>1. Ta'minotchilar (Suppliers)</h3>");
            content.AppendLine("<p>Ta'minotchilar - bu sizga mahsulotlar yetkazib beradigan kompaniyalar yoki shaxslar.</p>");
            content.AppendLine("<h4>Yangi ta'minotchi yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ta'minot → Ta'minotchilar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Formada kerakli maydonlarni to'ldiring</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");

            content.AppendLine("<h3>2. Xarid buyurtmalari (POrder)</h3>");
            content.AppendLine("<p>Xarid buyurtmalari - ta'minotchilarga berilgan buyurtmalar.</p>");
            content.AppendLine("<h4>Asosiy maydonlar:</h4>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>OrderNo</strong> - buyurtma raqami</li>");
            content.AppendLine("<li><strong>Supplier</strong> - ta'minotchi</li>");
            content.AppendLine("<li><strong>Contract</strong> - shartnoma</li>");
            content.AppendLine("<li><strong>Date</strong> - buyurtma sanasi</li>");
            content.AppendLine("<li><strong>Products</strong> - buyurtma mahsulotlari</li>");
            content.AppendLine("</ul>");

            content.AppendLine("<h4>Yangi xarid buyurtmasi yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Xarid → Buyurtmalar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Ta'minotchini tanlang</li>");
            content.AppendLine("<li>Mahsulotlarni qo'shing</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-16: Xarid buyurtmasi yaratish</strong></p>");

            content.AppendLine("<h3>3. Xarid shartnomalari (PContract)</h3>");
            content.AppendLine("<p>Xarid shartnomalari - ta'minotchilar bilan tuzilgan shartnomalar.</p>");
            content.AppendLine("<p><strong>Rasm-17: Xarid shartnomalari ro'yxati</strong></p>");

            content.AppendLine("<h3>4. Xarid invoyslari (PInvoice)</h3>");
            content.AppendLine("<p>Xarid invoyslari - ta'minotchilardan kelgan hisob-fakturalar.</p>");
            content.AppendLine("<p><strong>Rasm-18: Xarid invoyslari</strong></p>");

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
            content.AppendLine("<h4>Yangi kirim yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Omborxona → Ombor kirimlari bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Omborni tanlang</li>");
            content.AppendLine("<li>Mahsulotlarni va miqdorlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-20: Ombor kirimi yaratish</strong></p>");

            content.AppendLine("<h3>3. Ombor chiqimlari (Expenses)</h3>");
            content.AppendLine("<p>Ombor chiqimlari - ombordan mahsulotlar chiqarilganda yaratiladigan hujjatlar.</p>");
            content.AppendLine("<h4>Yangi chiqim yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Omborxona → Ombor chiqimlari bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Omborni tanlang</li>");
            content.AppendLine("<li>Mahsulotlarni va chiqariladigan miqdorlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-21: Ombor chiqimi yaratish</strong></p>");

            content.AppendLine("<h3>4. Ombor qoldiqlari (Inventory)</h3>");
            content.AppendLine("<p>Ombor qoldiqlari - har bir omborda qancha mahsulot qolganini ko'rsatadi.</p>");
            content.AppendLine("<p>Qoldiqlar avtomatik ravishda kirim-chiqim operatsiyalari asosida hisoblanadi.</p>");
            content.AppendLine("<p><strong>Rasm-22: Ombor qoldiqlari ro'yxati</strong></p>");

            content.AppendLine("<h3>5. Seriyalar va partiyalar</h3>");
            content.AppendLine("<p>❗ Bu yerda tizimning aniq xulq-atvori bo'yicha qo'shimcha aniqlik kiritish kerak (TODO).</p>");
            content.AppendLine("<p>Agar tizimda seriya va partiya boshqaruvi mavjud bo'lsa, bu yerda batafsil ko'rsatmalar bo'ladi.</p>");

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

            content.AppendLine("<h3>1. Mahsulot rejasi (ProductPlan)</h3>");
            content.AppendLine("<p>Mahsulot rejasi - qaysi mahsulotlarni qancha miqdorda ishlab chiqarish kerakligini ko'rsatadi.</p>");
            content.AppendLine("<h4>Yangi reja yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Ishlab chiqarish → Rejalashtirish bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Mahsulotni tanlang</li>");
            content.AppendLine("<li>Rejalashtirilgan miqdorni kiriting</li>");
            content.AppendLine("<li>Sana va boshqa parametrlarni kiriting</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-27: Mahsulot rejasi yaratish</strong></p>");

            content.AppendLine("<h3>2. BOM (Bill of Materials)</h3>");
            content.AppendLine("<p>BOM - mahsulot tarkibi, ya'ni bir mahsulotni ishlab chiqarish uchun qanday materiallar va qismlar kerakligi.</p>");
            content.AppendLine("<p><strong>Rasm-28: BOM ro'yxati</strong></p>");

            content.AppendLine("<h3>3. Norma-sarf</h3>");
            content.AppendLine("<p>Norma-sarf - mahsulot ishlab chiqarish uchun materiallar sarfining normasi.</p>");
            content.AppendLine("<p><strong>Rasm-23: Norma-sarf</strong></p>");

            content.AppendLine("<h3>4. Ishlab chiqarish (ProductProduction)</h3>");
            content.AppendLine("<p>Ishlab chiqarish - haqiqiy ishlab chiqarilgan mahsulotlar ro'yxati.</p>");
            content.AppendLine("<h4>Ishlab chiqarish yozuvini yaratish:</h4>");
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

            content.AppendLine("<h3>5. Sifat nazorati (QualityInspection)</h3>");
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
            content.AppendLine("<p>Moliya moduli moliyaviy operatsiyalar, hisoblar va hisobotlarni boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Moliya</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-29: Moliya bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Hisoblar (FinAccounts)</h3>");
            content.AppendLine("<p>Hisoblar - moliyaviy hisoblar ro'yxati.</p>");
            content.AppendLine("<p><strong>Rasm-30: Hisoblar ro'yxati</strong></p>");

            content.AppendLine("<h3>2. Tranzaksiyalar (FinTransactions)</h3>");
            content.AppendLine("<p>Tranzaksiyalar - barcha moliyaviy operatsiyalar.</p>");
            content.AppendLine("<h4>Yangi tranzaksiya yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Moliya → Tranzaksiyalar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Hisobni tanlang</li>");
            content.AppendLine("<li>Summani kiriting</li>");
            content.AppendLine("<li>Tranzaksiya turini tanlang</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-31: Tranzaksiya yaratish</strong></p>");

            content.AppendLine("<h3>3. Hisobotlar</h3>");
            content.AppendLine("<p>Moliya modulida quyidagi hisobotlar mavjud:</p>");
            content.AppendLine("<ul>");
            content.AppendLine("<li><strong>Kassa oqimi (Cashflow)</strong> - pul oqimlari hisoboti</li>");
            content.AppendLine("<li><strong>Foyda va zarar (P&L)</strong> - foyda va zarar hisoboti</li>");
            content.AppendLine("<li><strong>Balans (Balance Sheet)</strong> - balans hisoboti</li>");
            content.AppendLine("</ul>");
            content.AppendLine("<p><strong>Rasm-32: Moliya hisobotlari</strong></p>");

            content.AppendLine("<h3>4. Aksiyadorlar (Shareholders)</h3>");
            content.AppendLine("<p>Aksiyadorlar - kompaniya aksiyadorlari ro'yxati.</p>");
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
            content.AppendLine("<p>HR moduli tashkiliy struktura, xodimlar, tabel va ish haqini boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Kadrlar (HR)</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-34: HR bo'limi menyusi</strong></p>");

            content.AppendLine("<h3>1. Tashkiliy struktura (HrOrgUnits)</h3>");
            content.AppendLine("<p>Tashkiliy struktura - kompaniyaning bo'limlari va tuzilishi.</p>");
            content.AppendLine("<p><strong>Rasm-35: Tashkiliy struktura</strong></p>");

            content.AppendLine("<h3>2. Xodimlar (HrEmployees)</h3>");
            content.AppendLine("<p>Xodimlar - kompaniya xodimlari ro'yxati.</p>");
            content.AppendLine("<h4>Yangi xodim yaratish:</h4>");
            content.AppendLine("<ol>");
            content.AppendLine("<li>Kadrlar → Xodimlar bo'limiga o'ting</li>");
            content.AppendLine("<li>\"Yaratish\" tugmasini bosing</li>");
            content.AppendLine("<li>Xodimning shaxsiy ma'lumotlarini kiriting</li>");
            content.AppendLine("<li>Bo'lim va lavozimni tanlang</li>");
            content.AppendLine("<li>\"Saqlash\" tugmasini bosing</li>");
            content.AppendLine("</ol>");
            content.AppendLine("<p><strong>Rasm-36: Xodim yaratish</strong></p>");

            content.AppendLine("<h3>3. Tabel (HrTimesheets)</h3>");
            content.AppendLine("<p>Tabel - xodimlarning ish vaqtini hisobga olish.</p>");
            content.AppendLine("<p><strong>Rasm-37: Tabel</strong></p>");

            content.AppendLine("<h3>4. Ish haqi (HrPayroll)</h3>");
            content.AppendLine("<p>Ish haqi - xodimlarning ish haqi hisob-kitoblari.</p>");
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
            content.AppendLine("<p>Logistika moduli yuk tashish, forwarderlar va yo'l varaqalarini boshqarish uchun mo'ljallangan.</p>");

            content.AppendLine("<h3>Qayerdan topish mumkin?</h3>");
            content.AppendLine("<p>Asosiy menyudan <strong>Logistika</strong> bo'limiga o'ting.</p>");
            content.AppendLine("<p><strong>Rasm-46: Logistika bo'limi</strong></p>");

            content.AppendLine("<h3>1. Forwarderlar</h3>");
            content.AppendLine("<p>Forwarderlar - yuk tashish xizmatlarini ko'rsatadigan kompaniyalar.</p>");
            content.AppendLine("<p><strong>Rasm-47: Forwarderlar ro'yxati</strong></p>");

            content.AppendLine("<h3>2. Forwarder shartnomalari (FContract)</h3>");
            content.AppendLine("<p>Forwarder shartnomalari - forwarderlar bilan tuzilgan shartnomalar.</p>");
            content.AppendLine("<p><strong>Rasm-48: Forwarder shartnomalari</strong></p>");

            content.AppendLine("<h3>3. Yo'l varaqalari (Waybill)</h3>");
            content.AppendLine("<p>Yo'l varaqalari - yuk tashish hujjatlari.</p>");
            content.AppendLine("<p><strong>Rasm-49: Yo'l varaqalari</strong></p>");

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
