using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Documents;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Rasmiy maktublar. Ruxsat — [Permission]: GET = ko'rish, POST/Create/Edit/Delete = o'zgartirish.
    /// </summary>
    [Permission]
    public class OfficialLettersController : BaseController
    {
        private static readonly object[] DirectionItems = { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } };
        private static readonly string[] ChannelItems = { "EMAIL", "LETTER", "COURIER", "FAX" };

        // Servislar faqat kerak bo'lganda yaratiladi (avval har so'rovda, Index'da ham — yangi HttpClient bilan)
        private IDocumentTemplateService _templateService;
        private ITextSuggestionService _textSuggestionService;
        private IDocumentTemplateService TemplateService => _templateService ?? (_templateService = new DocumentTemplateService());
        private ITextSuggestionService TextSuggestion => _textSuggestionService ?? (_textSuggestionService = new TextSuggestionService());

        private static SelectList Directions(string selected) => new SelectList(DirectionItems, "Code", "Name", selected);

        private static SelectList Channels(string selected) => new SelectList(ChannelItems, selected);

        // Olish so'rovi: OfficialLetters
        public async Task<ActionResult> Index(int? year, string directionCode, string topicSearch)
        {
            using (var db = new DBTHSNEntities())
            {
                var query = from doc in db.DOC_DOCUMENTS.AsNoTracking()
                            join letter in db.COM_OFFICIAL_LETTERS on doc.ID equals letter.DocumentID
                            where doc.DOC_DOCUMENT_TYPES.Code == DocumentService.OfficialLetterType && doc.IsDeleted != true && letter.IsDeleted != true
                            select new { doc, letter };

                if (year.HasValue)
                {
                    query = query.Where(x => x.letter.LetterDate.Year == year.Value);
                }

                if (!string.IsNullOrEmpty(directionCode))
                {
                    query = query.Where(x => x.letter.DirectionCode == directionCode);
                }

                if (!string.IsNullOrEmpty(topicSearch))
                {
                    query = query.Where(x => x.letter.Topic.Contains(topicSearch) ||
                                             x.letter.ExternalCompanyName.Contains(topicSearch));
                }

                // SQL'da proyeksiya — avval to'liq entity'lar olinib, status har hujjatga lazy-load qilinardi (DOC-P11)
                var viewModel = await query
                    .OrderByDescending(x => x.letter.LetterDate)
                    .ThenByDescending(x => x.doc.ID)
                    .Select(x => new OfficialLetterIndexVm
                    {
                        DocumentId = x.doc.ID,
                        LetterNo = x.letter.LetterNo,
                        LetterDate = x.letter.LetterDate,
                        DirectionCode = x.letter.DirectionCode,
                        ExternalCompanyName = x.letter.ExternalCompanyName,
                        ExternalContactName = x.letter.ExternalContactName,
                        Topic = x.letter.Topic,
                        StatusName = x.doc.DOC_DOCUMENT_STATUS_REF.Name,
                        CreatedDT = x.doc.CreatedDT
                    })
                    .ToListAsync();

                ViewBag.Year = year;
                ViewBag.DirectionCode = directionCode;
                ViewBag.TopicSearch = topicSearch;
                // O'chirilganlar yillari ko'rinmasin (DOC-B12)
                ViewBag.Years = new SelectList(await db.COM_OFFICIAL_LETTERS
                    .Where(l => l.IsDeleted != true && l.DOC_DOCUMENTS.IsDeleted != true)
                    .Select(l => l.LetterDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(), year);
                ViewBag.Directions = Directions(directionCode);

                return View(viewModel);
            }
        }

        // Olish so'rovi: OfficialLetters/yaratish
        public async Task<ActionResult> Create()
        {
            using (var db = new DBTHSNEntities())
            {
                var vm = new OfficialLetterCreateVm
                {
                    LetterDate = DateTime.Now,
                    DirectionCode = "OUT",
                    // Taxminiy raqam; saqlashda qulf ostida qayta tekshiriladi
                    LetterNo = await DocNumbering.PeekLetterNoAsync(db)
                };

                vm.Directions = Directions("OUT");
                vm.Channels = Channels("EMAIL");

                return View(vm);
            }
        }

        // POST so'rovi: OfficialLetters/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(OfficialLetterCreateVm vm)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    using (var tx = db.Database.BeginTransaction())
                    {
                        var ids = await DocumentService.DraftHeaderIdsAsync(db, DocumentService.OfficialLetterType);
                        if (ids == null)
                        {
                            TempData["ErrorMessage"] = "Hujjat turi yoki holati topilmadi. Iltimos, seed skriptlarini ishga tushiring.";
                            return RedirectToAction("Index");
                        }

                        // Chiquvchi xat raqami qulf ostida: band bo'lsa keyingisi beriladi (DOC-B06)
                        string letterNo = vm.DirectionCode == "OUT"
                            ? await DocNumbering.EnsureOutgoingLetterNoAsync(db, vm.LetterNo)
                            : vm.LetterNo;
                        int userId = CurrentUserId;

                        // Hujjat va xat bitta tranzaksiyada — xato bo'lsa yetim DOC_DOCUMENTS qolmaydi (DOC-B05)
                        var document = DocumentService.NewHeader(ids.Item1, ids.Item2, letterNo, vm.Topic, userId);
                        document.COM_OFFICIAL_LETTERS = new COM_OFFICIAL_LETTERS
                        {
                            DirectionCode = vm.DirectionCode,
                            LetterNo = letterNo,
                            LetterDate = vm.LetterDate,
                            ExternalCompanyName = vm.ExternalCompanyName,
                            ExternalContactName = vm.ExternalContactName,
                            ExternalContactPosition = vm.ExternalContactPosition,
                            OurRefNo = vm.OurRefNo,
                            TheirRefNo = vm.TheirRefNo,
                            ChannelCode = vm.ChannelCode,
                            Topic = vm.Topic
                        };

                        db.DOC_DOCUMENTS.Add(document);
                        await db.SaveChangesAsync();

                        // Matn, salomlashish va mahsulot/narx satrlarini DOC_FILES'ga JSON sifatida saqlash
                        var letterData = BuildLetterData(vm.BodyText, vm.GreetingLine, vm.Items);
                        await OfficialLetterDataService.SaveAsync(db, document.ID, letterData, userId);
                        await db.SaveChangesAsync();
                        tx.Commit();

                        LogHelper.LogToDatabase(CurrentUserName, "OfficialLetters", "Create", $"Xat yaratildi: {letterNo}");

                        TempData["Success"] = letterNo == vm.LetterNo?.Trim()
                            ? "Maktub muvaffaqiyatli yaratildi"
                            : $"Maktub yaratildi. {vm.LetterNo} raqami band bo'lgani uchun {letterNo} raqami berildi";
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("OfficialLetters Create: {0}", ex);
                    ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + InnermostMessage(ex));
                }
            }

            vm.Directions = Directions(vm.DirectionCode);
            vm.Channels = Channels(vm.ChannelCode);

            return View(vm);
        }

        // Olish so'rovi: OfficialLetters/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS.AsNoTracking()
                    .Where(d => d.ID == id && d.IsDeleted != true)
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync();

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null || (doc.COM_OFFICIAL_LETTERS.IsDeleted == true))
                {
                    return HttpNotFound();
                }

                var letter = doc.COM_OFFICIAL_LETTERS;

                // Saqlangan matn, salomlashish va mahsulotlarni DOC_FILES JSON'dan yuklash
                var letterData = await OfficialLetterDataService.LoadAsync(db, doc.ID);

                var vm = new OfficialLetterEditVm
                {
                    DocumentId = doc.ID,
                    DirectionCode = letter.DirectionCode,
                    LetterNo = letter.LetterNo,
                    LetterDate = letter.LetterDate,
                    ExternalCompanyName = letter.ExternalCompanyName,
                    ExternalContactName = letter.ExternalContactName,
                    ExternalContactPosition = letter.ExternalContactPosition,
                    OurRefNo = letter.OurRefNo,
                    TheirRefNo = letter.TheirRefNo,
                    ChannelCode = letter.ChannelCode,
                    Topic = letter.Topic,
                    GreetingLine = !string.IsNullOrEmpty(letterData.GreetingLine)
                        ? letterData.GreetingLine
                        : $"Hurmatli {letter.ExternalContactName ?? "hamkor"}!",
                    BodyText = letterData.BodyHtml,
                    Items = MapItems(letterData.Items)
                };

                vm.Directions = Directions(letter.DirectionCode);
                vm.Channels = Channels(letter.ChannelCode);

                return View(vm);
            }
        }

        // POST so'rovi: OfficialLetters/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(OfficialLetterEditVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.LetterNo))
            {
                ModelState.AddModelError("LetterNo", "Maktub raqami kiritilishi shart");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        var doc = await db.DOC_DOCUMENTS
                            .Include(d => d.COM_OFFICIAL_LETTERS)
                            .FirstOrDefaultAsync(d => d.ID == vm.DocumentId && d.IsDeleted != true);

                        if (doc == null || doc.COM_OFFICIAL_LETTERS == null || doc.COM_OFFICIAL_LETTERS.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        doc.Title = vm.Topic;
                        doc.Code = vm.LetterNo; // hujjat kodi ham maktub raqamiga moslashtiriladi
                        doc.UpdatedDT = DateTime.Now;

                        var letter = doc.COM_OFFICIAL_LETTERS;
                        letter.DirectionCode = vm.DirectionCode;
                        letter.LetterNo = vm.LetterNo;
                        letter.LetterDate = vm.LetterDate;
                        letter.ExternalCompanyName = vm.ExternalCompanyName;
                        letter.ExternalContactName = vm.ExternalContactName;
                        letter.ExternalContactPosition = vm.ExternalContactPosition;
                        letter.OurRefNo = vm.OurRefNo;
                        letter.TheirRefNo = vm.TheirRefNo;
                        letter.ChannelCode = vm.ChannelCode;
                        letter.Topic = vm.Topic;

                        // Matn, salomlashish va mahsulot/narx satrlarini yangilash
                        var letterData = BuildLetterData(vm.BodyText, vm.GreetingLine, vm.Items);
                        await OfficialLetterDataService.SaveAsync(db, doc.ID, letterData, CurrentUserId);

                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(CurrentUserName, "OfficialLetters", "Edit", $"Xat yangilandi: {vm.LetterNo}");

                        TempData["Success"] = "Maktub muvaffaqiyatli yangilandi";
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("OfficialLetters Edit {0}: {1}", vm.DocumentId, ex);
                    ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + InnermostMessage(ex));
                }
            }

            vm.Directions = Directions(vm.DirectionCode);
            vm.Channels = Channels(vm.ChannelCode);

            return View(vm);
        }

        // Olish so'rovi: OfficialLetters/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS.AsNoTracking()
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .Include(d => d.DOC_DOCUMENT_STATUS_REF)
                    .FirstOrDefaultAsync(d => d.ID == id && d.IsDeleted != true);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null || doc.COM_OFFICIAL_LETTERS.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                // Faqat metadata — avval Include(DOC_FILES) barcha versiyalar blob'ini o'qirdi (DOC-P01)
                ViewBag.DocFiles = await DocFileService.ListMainAsync(db, doc.ID);
                ViewBag.Templates = await DocTemplateStore.ListAsync(db, DocPlaceholders.LetterType, activeOnly: true);

                // Saqlangan matn, salomlashish va mahsulotlarni ko'rsatish uchun yuklash
                var letterData = await OfficialLetterDataService.LoadAsync(db, doc.ID);
                ViewBag.BodyHtml = letterData.BodyHtml;
                ViewBag.GreetingLine = letterData.GreetingLine;
                ViewBag.LetterItems = letterData.Items;

                return View(doc);
            }
        }

        // POST so'rovi: OfficialLetters/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(long id)
        {
            using (var db = new DBTHSNEntities())
            {
                var code = await db.COM_OFFICIAL_LETTERS
                    .Where(l => l.DocumentID == id && l.IsDeleted != true && l.DOC_DOCUMENTS.IsDeleted != true)
                    .Select(l => l.DOC_DOCUMENTS.Code)
                    .FirstOrDefaultAsync();

                if (code != null)
                {
                    // Set-based: hujjat, xat va fayllar blob'larini yuklamasdan (DOC-P06)
                    await DocumentService.SoftDeleteAsync(db, id);

                    LogHelper.LogToDatabase(CurrentUserName, "OfficialLetters", "Delete", $"Xat o'chirildi: {code}");

                    TempData["Success"] = "Maktub muvaffaqiyatli o'chirildi";
                }
            }

            return RedirectToAction("Index");
        }

        // POST so'rovi: OfficialLetters/GenerateDoc/5 — templateId: Details'da tanlangan shablon
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateDoc(long id, int? templateId)
        {
            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync(d => d.ID == id && d.IsDeleted != true);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null || doc.COM_OFFICIAL_LETTERS.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                var letter = doc.COM_OFFICIAL_LETTERS;

                // Tanlangan (yoki yagona/eng yangi faol) shablon — shablonlar bazada (DOC_TEMPLATES)
                var resolvedTemplateId = await DocTemplateStore.ResolveActiveAsync(db, DocPlaceholders.LetterType, templateId);
                if (resolvedTemplateId == null)
                {
                    TempData["ErrorMessage"] = templateId.HasValue
                        ? "Tanlangan shablon topilmadi yoki arxivlangan."
                        : "Faol shablon yo'q. \"Shablonlar\" sahifasida Word shablon yuklang.";
                    return RedirectToAction("Details", new { id });
                }

                // Matn (formatlash bilan), salomlashish va mahsulotlar
                var letterData = await OfficialLetterDataService.LoadAsync(db, doc.ID);
                var data = DocTemplateDataBuilder.ForLetter(doc, letter, letterData, await CompanySettings.DirectorNameAsync(db));

                GeneratedDocument generated;
                try
                {
                    generated = TemplateService.Generate(resolvedTemplateId.Value, data);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("OfficialLetters GenerateDoc {0}: {1}", id, ex);
                    TempData["ErrorMessage"] = "Hujjat yaratib bo'lmadi: " +
                        (ex is InvalidOperationException ? ex.Message : "shablon faylini tekshiring (Word'da ochilishi kerak).");
                    return RedirectToAction("Details", new { id });
                }

                // Yangi asosiy versiya (VersionNo MAX va IsMain UPDATE SQL'da — blob'lar o'qilmaydi, DOC-P05)
                DOC_FILES file;
                using (var tx = db.Database.BeginTransaction())
                {
                    file = await DocFileService.AddMainVersionAsync(db, doc.ID, generated.Content,
                        $"{letter.LetterNo}_{letter.LetterDate:yyyyMMdd}" + generated.FileExtension,
                        generated.FileExtension, generated.MimeType, CurrentUserId);
                    doc.TemplateID = resolvedTemplateId.Value;
                    await db.SaveChangesAsync();
                    tx.Commit();
                }

                LogHelper.LogToDatabase(CurrentUserName, "OfficialLetters", "GenerateDoc", $"Xat uchun hujjat yaratildi: {letter.LetterNo}");

                TempData["Success"] = $"Hujjat yaratildi (versiya {file.VersionNo}) va yuklab olinmoqda.";
                if (generated.UnknownPlaceholders.Count > 0)
                {
                    TempData["Warning"] = "Shablonda tanilmagan maydonlar o'zgarishsiz qoldi: " +
                        string.Join(", ", generated.UnknownPlaceholders.Select(p => "{{" + p + "}}")) +
                        ". To'g'ri nomlar \"Shablonlar\" sahifasida.";
                }
                // Details sahifasi faylni avtomatik yuklab beradi
                TempData["DownloadFileId"] = file.ID;
                return RedirectToAction("Details", new { id });
            }
        }

        // POST so'rovi: OfficialLetters/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(long id, string statusCode)
        {
            using (var db = new DBTHSNEntities())
            {
                // Hujjat turi, o'chirilmaganligi va ruxsat etilgan o'tish tekshiriladi (DOC-B08)
                var result = await DocumentService.ChangeStatusAsync(db, id, DocumentService.OfficialLetterType, statusCode, CurrentUserId);
                if (result == null)
                {
                    return HttpNotFound();
                }

                if (!result.Success)
                {
                    TempData["ErrorMessage"] = result.Error;
                    return RedirectToAction("Details", new { id });
                }

                LogHelper.LogToDatabase(CurrentUserName, "OfficialLetters", "ChangeStatus",
                    $"Xat holati '{result.StatusName}' ga o'zgartirildi: {result.DocumentCode}");

                TempData["Success"] = $"Holat '{result.StatusName}' ga o'zgartirildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // Olish so'rovi: OfficialLetters/DownloadFile/5
        public async Task<ActionResult> DownloadFile(long fileId)
        {
            using (var db = new DBTHSNEntities())
            {
                return DocFileResult.For(await DocFileAccess.FindAsync(db, fileId, DocFileAccess.Owner.OfficialLetter));
            }
        }

        // AJAX so'rovi: Suggest text (ruxsat — [Permission], POST = o'zgartirish; avval tekshiruv yo'q edi, DOC-S04)
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> SuggestText(string directionCode, string externalCompanyName, string externalContactName, string topic, string letterDate, string ourRefNo, string theirRefNo)
        {
            try
            {
                DateTime parsedDate = DateTime.Now;
                if (!string.IsNullOrEmpty(letterDate))
                {
                    if (DateTime.TryParse(letterDate, out DateTime tempDate))
                    {
                        parsedDate = tempDate;
                    }
                }

                var meta = new OfficialLetterMeta
                {
                    DirectionCode = directionCode ?? "OUT",
                    ExternalCompanyName = externalCompanyName ?? string.Empty,
                    ExternalContactName = externalContactName ?? string.Empty,
                    Topic = topic ?? string.Empty,
                    LetterDate = parsedDate,
                    OurRefNo = ourRefNo ?? string.Empty,
                    TheirRefNo = theirRefNo ?? string.Empty
                };

                // Async — avval .Result bilan kutilardi (AI yoqilganda deadlock, DOC-B09)
                var suggestedText = await TextSuggestion.SuggestLetterBodyAsync(meta);
                return Json(new { success = true, text = suggestedText });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("OfficialLetters SuggestText: {0}", ex);
                return Json(new { success = false, error = "Matn taklif qilib bo'lmadi." });
            }
        }

        // VM mahsulot satrlarini saqlash uchun data modeliga aylantiradi (bo'sh satrlarni tashlab)
        private static OfficialLetterData BuildLetterData(string bodyHtml, string greetingLine, List<OfficialLetterItemVm> items)
        {
            var data = new OfficialLetterData
            {
                BodyHtml = bodyHtml,
                GreetingLine = greetingLine,
                Items = new List<OfficialLetterItemData>()
            };

            if (items != null)
            {
                foreach (var it in items)
                {
                    if (it == null)
                        continue;

                    // Butunlay bo'sh satrlarni saqlamaymiz
                    bool isEmpty = string.IsNullOrWhiteSpace(it.ItemName)
                                   && string.IsNullOrWhiteSpace(it.ItemSize)
                                   && !it.PriceExw.HasValue && !it.PriceVatExw.HasValue
                                   && !it.PriceCip.HasValue && !it.PriceVatCip.HasValue;
                    if (isEmpty)
                        continue;

                    data.Items.Add(new OfficialLetterItemData
                    {
                        ItemName = it.ItemName,
                        ItemSize = it.ItemSize,
                        PriceExw = it.PriceExw,
                        PriceVatExw = it.PriceVatExw,
                        PriceCip = it.PriceCip,
                        PriceVatCip = it.PriceVatCip
                    });
                }
            }

            return data;
        }

        // Saqlangan data mahsulotlarini tahrirlash VM ro'yxatiga aylantiradi
        private static List<OfficialLetterItemVm> MapItems(List<OfficialLetterItemData> items)
        {
            var result = new List<OfficialLetterItemVm>();
            if (items != null)
            {
                foreach (var it in items)
                {
                    result.Add(new OfficialLetterItemVm
                    {
                        ItemName = it.ItemName,
                        ItemSize = it.ItemSize,
                        PriceExw = it.PriceExw,
                        PriceVatExw = it.PriceVatExw,
                        PriceCip = it.PriceCip,
                        PriceVatCip = it.PriceVatCip
                    });
                }
            }
            return result;
        }

        // Olish so'rovi: OfficialLetters/RegisterIncoming
        [Permission(Change = true)]
        public ActionResult RegisterIncoming()
        {
            var vm = new IncomingLetterRegisterVm
            {
                LetterDate = DateTime.Now
            };

            return View(vm);
        }

        // POST so'rovi: OfficialLetters/RegisterIncoming
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterIncoming(IncomingLetterRegisterVm vm)
        {
            // Fayl hujjat yozilishidan OLDIN tekshiriladi — avval DOC_DOCUMENTS saqlanib, keyin validatsiya
            // xatosida yetim hujjat qolardi (DOC-B04)
            string fileExtension = null;
            bool hasFile = vm.AttachmentFile != null && vm.AttachmentFile.ContentLength > 0;
            if (hasFile)
            {
                fileExtension = System.IO.Path.GetExtension(vm.AttachmentFile.FileName).ToLower();
                if (fileExtension != ".pdf")
                {
                    ModelState.AddModelError("AttachmentFile", "Faqat PDF fayllarni yuklash mumkin");
                }
                else if (vm.AttachmentFile.ContentLength > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("AttachmentFile", "Fayl hajmi 10 MB dan oshmasligi kerak");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            using (var db = new DBTHSNEntities())
            {
                var ids = await DocumentService.DraftHeaderIdsAsync(db, DocumentService.OfficialLetterType);
                if (ids == null)
                {
                    TempData["ErrorMessage"] = "Hujjat turi yoki holati topilmadi";
                    return View(vm);
                }

                int userId = CurrentUserId;

                // Kiruvchi xat raqami tashqi (jo'natuvchiniki) — chiquvchi ketma-ketlikka qo'shilmaydi
                string title = $"Kiruvchi xat: {vm.CompanyOrPersonName}";
                var doc = DocumentService.NewHeader(ids.Item1, ids.Item2, $"IN-{vm.LetterNo}", title, userId);
                doc.COM_OFFICIAL_LETTERS = new COM_OFFICIAL_LETTERS
                {
                    DirectionCode = "IN",
                    LetterNo = vm.LetterNo,
                    LetterDate = vm.LetterDate ?? DateTime.Now,
                    ExternalCompanyName = vm.CompanyOrPersonName,
                    Topic = title
                };

                if (hasFile)
                {
                    byte[] fileContent;
                    using (var binaryReader = new System.IO.BinaryReader(vm.AttachmentFile.InputStream))
                    {
                        fileContent = binaryReader.ReadBytes(vm.AttachmentFile.ContentLength);
                    }

                    doc.DOC_FILES.Add(new DOC_FILES
                    {
                        VersionNo = 1,
                        FileName = vm.AttachmentFile.FileName,
                        FileExtension = fileExtension,
                        MimeType = vm.AttachmentFile.ContentType ?? "application/pdf",
                        FileSizeBytes = vm.AttachmentFile.ContentLength,
                        FileContent = fileContent,
                        IsMain = true,
                        CreatedByUserID = userId,
                        CreatedDT = DateTime.Now
                    });
                }

                // Hujjat, xat va fayl — bitta SaveChanges (bitta tranzaksiya)
                db.DOC_DOCUMENTS.Add(doc);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(CurrentUserName, "OfficialLetters", "RegisterIncoming",
                    $"Kiruvchi xat ro'yxatga olindi: {vm.LetterNo} - {vm.CompanyOrPersonName}");

                TempData["Success"] = "Kiruvchi xat muvaffaqiyatli ro'yxatga olindi";
                return RedirectToAction("Index");
            }
        }
    }
}
