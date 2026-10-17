using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;
using tahsinERP.Services.Documents;
using tahsinERP.Controllers;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class OfficialLettersController : Controller
    {
        private readonly IDocumentTemplateService _templateService;
        private readonly ITextSuggestionService _textSuggestionService;

        public OfficialLettersController()
        {
            _templateService = new DocumentTemplateService();
            _textSuggestionService = new TextSuggestionService();
        }

        // Olish so'rovi: OfficialLetters
        public async Task<ActionResult> Index(int? year, string directionCode, string topicSearch)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OfficialLetters", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = from doc in db.DOC_DOCUMENTS
                            join letter in db.COM_OFFICIAL_LETTERS on doc.ID equals letter.DocumentID
                            join docType in db.DOC_DOCUMENT_TYPES on doc.DocTypeID equals docType.ID
                            where docType.Code == "OFFICIAL_LETTER" && doc.IsDeleted != true && letter.IsDeleted != true
                            select new { doc, letter, docType };

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

                var results = await query
                    .OrderByDescending(x => x.letter.LetterDate)
                    .ThenByDescending(x => x.doc.ID)
                    .ToListAsync();

                var viewModel = results.Select(x => new OfficialLetterIndexVm
                {
                    DocumentId = x.doc.ID,
                    LetterNo = x.letter.LetterNo,
                    LetterDate = x.letter.LetterDate,
                    DirectionCode = x.letter.DirectionCode,
                    ExternalCompanyName = x.letter.ExternalCompanyName,
                    ExternalContactName = x.letter.ExternalContactName,
                    Topic = x.letter.Topic,
                    StatusName = x.doc.DOC_DOCUMENT_STATUS_REF != null ? x.doc.DOC_DOCUMENT_STATUS_REF.Name : null,
                    CreatedDT = x.doc.CreatedDT
                }).ToList();

                ViewBag.Year = year;
                ViewBag.DirectionCode = directionCode;
                ViewBag.TopicSearch = topicSearch;
                ViewBag.Years = new SelectList(await db.COM_OFFICIAL_LETTERS
                    .Select(l => l.LetterDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(), year);
                ViewBag.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", directionCode);

                return View(viewModel);
            }
        }

        // Olish so'rovi: OfficialLetters/yaratish
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var vm = new OfficialLetterCreateVm
            {
                LetterDate = DateTime.Now,
                DirectionCode = "OUT",
                LetterNo = GenerateNextLetterNo()
            };

            vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", "OUT");
            vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, "EMAIL");

            return View(vm);
        }

        // POST so'rovi: OfficialLetters/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(OfficialLetterCreateVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        var docType = await db.DOC_DOCUMENT_TYPES.FirstOrDefaultAsync(dt => dt.Code == "OFFICIAL_LETTER");
                        var statusDraft = await db.DOC_DOCUMENT_STATUS_REF.FirstOrDefaultAsync(s => s.Code == "DRAFT");

                        if (docType == null || statusDraft == null)
                        {
                            TempData["ErrorMessage"] = "Hujjat turi yoki holati topilmadi. Iltimos, seed skriptlarini ishga tushiring.";
                            return RedirectToAction("Index");
                        }

                        var document = new DOC_DOCUMENTS
                        {
                            DocTypeID = docType.ID,
                            StatusID = statusDraft.ID,
                            Code = vm.LetterNo,
                            Title = vm.Topic,
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now,
                            IntegrationUID = Guid.NewGuid()
                        };

                        db.DOC_DOCUMENTS.Add(document);
                        await db.SaveChangesAsync();

                        var letter = new COM_OFFICIAL_LETTERS
                        {
                            DocumentID = document.ID,
                            DirectionCode = vm.DirectionCode,
                            LetterNo = vm.LetterNo,
                            LetterDate = vm.LetterDate,
                            ExternalCompanyName = vm.ExternalCompanyName,
                            ExternalContactName = vm.ExternalContactName,
                            ExternalContactPosition = vm.ExternalContactPosition,
                            OurRefNo = vm.OurRefNo,
                            TheirRefNo = vm.TheirRefNo,
                            ChannelCode = vm.ChannelCode,
                            Topic = vm.Topic
                        };

                        db.COM_OFFICIAL_LETTERS.Add(letter);
                        await db.SaveChangesAsync();

                        // Matn, salomlashish va mahsulot/narx satrlarini DOC_FILES'ga JSON sifatida saqlash
                        var letterData = BuildLetterData(vm.BodyText, vm.GreetingLine, vm.Items);
                        await OfficialLetterDataService.SaveAsync(db, document.ID, letterData, UserHelper.GetUserId(User.Identity.Name));
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "Create", $"Xat yaratildi: {vm.LetterNo}");

                        TempData["Success"] = "Maktub muvaffaqiyatli yaratildi";
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + (ex.InnerException?.Message ?? ex.Message));
                }
            }

            vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", vm.DirectionCode);
            vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, vm.ChannelCode);

            return View(vm);
        }

        // Olish so'rovi: OfficialLetters/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Where(d => d.ID == id && (d.IsDeleted == false || d.IsDeleted == null))
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

                vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", letter.DirectionCode);
                vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, letter.ChannelCode);

                return View(vm);
            }
        }

        // POST so'rovi: OfficialLetters/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(OfficialLetterEditVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
                            .FirstOrDefaultAsync(d => d.ID == vm.DocumentId);

                        if (doc == null || doc.COM_OFFICIAL_LETTERS == null)
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
                        await OfficialLetterDataService.SaveAsync(db, doc.ID, letterData, UserHelper.GetUserId(User.Identity.Name));

                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "Edit", $"Xat yangilandi: {vm.LetterNo}");

                        TempData["Success"] = "Maktub muvaffaqiyatli yangilandi";
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + (ex.InnerException?.Message ?? ex.Message));
                }
            }

            vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", vm.DirectionCode);
            vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, vm.ChannelCode);

            return View(vm);
        }

        // Olish so'rovi: OfficialLetters/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OfficialLetters", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .Include(d => d.DOC_DOCUMENT_STATUS_REF)
                    .FirstOrDefaultAsync(d => d.ID == id && d.IsDeleted != true);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null || doc.COM_OFFICIAL_LETTERS.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                // Faqat metadata — avval Include(DOC_FILES) barcha versiyalar blob'ini o'qirdi (DOC-P01)
                ViewBag.DocFiles = await DocFileService.ListMainAsync(db, doc.ID);

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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

                    LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "Delete", $"Xat o'chirildi: {code}");

                    TempData["Success"] = "Maktub muvaffaqiyatli o'chirildi";
                }
            }

            return RedirectToAction("Index");
        }

        // POST so'rovi: OfficialLetters/GenerateDoc/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateDoc(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "GenerateDoc"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null)
                {
                    return HttpNotFound();
                }

                var letter = doc.COM_OFFICIAL_LETTERS;
                // Faqat ID — shablon blob'i DocumentTemplateService'da (keshdan) o'qiladi (DOC-P07)
                var templateId = await db.DOC_TEMPLATES
                    .Where(t => t.DocTypeID == doc.DocTypeID && t.IsActive)
                    .Select(t => (int?)t.ID)
                    .FirstOrDefaultAsync();

                if (templateId == null)
                {
                    TempData["ErrorMessage"] = "Shablon topilmadi";
                    return RedirectToAction("Details", new { id });
                }

                // Tayyorlash placeholders
                var recipientBlock = letter.ExternalCompanyName ?? "";
                if (!string.IsNullOrEmpty(letter.ExternalContactPosition))
                {
                    recipientBlock += "\n" + letter.ExternalContactPosition;
                }
                if (!string.IsNullOrEmpty(letter.ExternalContactName))
                {
                    recipientBlock += "\n" + letter.ExternalContactName;
                }

                // Saqlangan matn, salomlashish va mahsulotlarni yuklash
                var letterData = await OfficialLetterDataService.LoadAsync(db, doc.ID);
                var bodyText = !string.IsNullOrWhiteSpace(letterData.BodyHtml)
                    ? OfficialLetterDataService.HtmlToPlainText(letterData.BodyHtml)
                    : (doc.Title ?? "");
                var greetingLine = !string.IsNullOrWhiteSpace(letterData.GreetingLine)
                    ? letterData.GreetingLine
                    : $"Hurmatli {letter.ExternalContactName ?? "hamkor"}!";

                var placeholders = new Dictionary<string, string>
                {
                    { "LETTER_NO", letter.LetterNo },
                    { "LETTER_DATE", letter.LetterDate.ToString("dd.MM.yyyy") },
                    { "RECIPIENT_BLOCK", recipientBlock },
                    { "GREETING_LINE", greetingLine },
                    { "LETTER_BODY", bodyText },
                    { "ITEMS_BLOCK", BuildItemsTextBlock(letterData.Items) },
                    { "DIRECTOR_NAME", "X.M. Maxmudov" }, // TODO: Get dan settings
                    { "OUR_REF_NO", letter.OurRefNo ?? "" },
                    { "THEIR_REF_NO", letter.TheirRefNo ?? "" }
                };

                // Har bir mahsulot uchun alohida o'rin egallovchilar (shablon mosligi uchun)
                AddItemPlaceholders(placeholders, letterData.Items);

                // Yaratish hujjat
                string fileName, fileExtension, mimeType;
                byte[] fileContent = _templateService.GenerateDocFromTemplate(
                    templateId.Value, placeholders, out fileName, out fileExtension, out mimeType);

                // Yangi asosiy versiya (VersionNo MAX va IsMain UPDATE SQL'da — blob'lar o'qilmaydi, DOC-P05)
                using (var tx = db.Database.BeginTransaction())
                {
                    await DocFileService.AddMainVersionAsync(db, doc.ID, fileContent,
                        $"{letter.LetterNo}_{letter.LetterDate:yyyyMMdd}{fileExtension}",
                        fileExtension, mimeType, UserHelper.GetUserId(User.Identity.Name));
                    await db.SaveChangesAsync();
                    tx.Commit();
                }

                LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "GenerateDoc", $"Xat uchun hujjat yaratildi: {letter.LetterNo}");

                TempData["Success"] = "Hujjat muvaffaqiyatli yaratildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // POST so'rovi: OfficialLetters/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(long id, string statusCode)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "ChangeStatus"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null)
                {
                    return HttpNotFound();
                }

                var status = await db.DOC_DOCUMENT_STATUS_REF
                    .FirstOrDefaultAsync(s => s.Code == statusCode && s.IsActive);

                if (status == null)
                {
                    TempData["ErrorMessage"] = "Noto'g'ri holat kodi";
                    return RedirectToAction("Details", new { id });
                }

                doc.StatusID = status.ID;
                doc.UpdatedDT = DateTime.Now;

                // agar approving, set ApprovedByUserID
                if (statusCode == "APPROVED")
                {
                    var currentUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);
                    if (currentUser != null)
                    {
                        doc.ApprovedByUserID = currentUser.ID;
                    }
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "ChangeStatus", 
                    $"Xat holati '{status.Name}' ga o'zgartirildi: {doc.COM_OFFICIAL_LETTERS?.LetterNo}");

                TempData["Success"] = $"Holat '{status.Name}' ga o'zgartirildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // Olish so'rovi: OfficialLetters/DownloadFile/5
        public async Task<ActionResult> DownloadFile(long fileId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OfficialLetters", "DownloadFile"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var file = await DocFileAccess.FindAsync(db, fileId, DocFileAccess.Owner.OfficialLetter);
                if (file == null || file.FileContent == null)
                {
                    return HttpNotFound();
                }

                return File(file.FileContent, file.MimeType ?? "application/octet-stream", file.FileName);
            }
        }

        // AJAX so'rovi: Suggest text
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SuggestText(string directionCode, string externalCompanyName, string externalContactName, string topic, string letterDate, string ourRefNo, string theirRefNo)
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

                var suggestedText = _textSuggestionService.SuggestLetterBody(meta);
                return Json(new { success = true, text = suggestedText }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
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

        // Mahsulotlarni hujjatga joylash uchun matn bloki sifatida shakllantiradi
        private static string BuildItemsTextBlock(List<OfficialLetterItemData> items)
        {
            if (items == null || items.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            int i = 1;
            foreach (var it in items)
            {
                sb.Append(i + ". " + (it.ItemName ?? ""));
                if (!string.IsNullOrWhiteSpace(it.ItemSize))
                    sb.Append(" (" + it.ItemSize + ")");
                sb.AppendLine();

                if (it.PriceExw.HasValue)
                    sb.AppendLine("   Narx EXW: " + it.PriceExw.Value.ToString("#,0.##"));
                if (it.PriceVatExw.HasValue)
                    sb.AppendLine("   Narx QQS bilan EXW: " + it.PriceVatExw.Value.ToString("#,0.##"));
                if (it.PriceCip.HasValue)
                    sb.AppendLine("   Narx CIP: " + it.PriceCip.Value.ToString("#,0.##"));
                if (it.PriceVatCip.HasValue)
                    sb.AppendLine("   Narx QQS bilan CIP: " + it.PriceVatCip.Value.ToString("#,0.##"));

                i++;
            }

            return sb.ToString().TrimEnd();
        }

        // Har bir mahsulot uchun indeksli o'rin egallovchilarni qo'shadi (ITEM_NAME_1, ITEM_PRICE_EXW_1, ...)
        private static void AddItemPlaceholders(Dictionary<string, string> placeholders, List<OfficialLetterItemData> items)
        {
            if (items == null)
                return;

            for (int idx = 0; idx < items.Count; idx++)
            {
                var it = items[idx];
                int n = idx + 1;
                placeholders["ITEM_NAME_" + n] = it.ItemName ?? "";
                placeholders["ITEM_DIM_" + n] = it.ItemSize ?? "";
                placeholders["ITEM_PRICE_EXW_" + n] = it.PriceExw.HasValue ? it.PriceExw.Value.ToString("#,0.##") : "";
                placeholders["ITEM_PRICE_VAT_EXW_" + n] = it.PriceVatExw.HasValue ? it.PriceVatExw.Value.ToString("#,0.##") : "";
                placeholders["ITEM_PRICE_CIP_" + n] = it.PriceCip.HasValue ? it.PriceCip.Value.ToString("#,0.##") : "";
                placeholders["ITEM_PRICE_VAT_CIP_" + n] = it.PriceVatCip.HasValue ? it.PriceVatCip.Value.ToString("#,0.##") : "";

                // Eski shablon mosligi: ITEM_PRICE_1 / ITEM_PRICE_VAT_1 = EXW narxlari
                placeholders["ITEM_PRICE_" + n] = placeholders["ITEM_PRICE_EXW_" + n];
                placeholders["ITEM_PRICE_VAT_" + n] = placeholders["ITEM_PRICE_VAT_EXW_" + n];
            }
        }

        private string GenerateNextLetterNo()
        {
            using (var db = new DBTHSNEntities())
            {
                // Olish barcha letter numbers va find the maksimal numeric value
                var allLetters = db.COM_OFFICIAL_LETTERS
                    .Where(l => l.IsDeleted != true)
                    .Select(l => l.LetterNo)
                    .ToList();

                int maxLetterNo = 0;
                
                foreach (var letterNo in allLetters)
                {
                    // Urinish ga tahlil as pure number (e.g., "0005", "0122")
                    if (int.TryParse(letterNo, out int num))
                    {
                        if (num > maxLetterNo)
                            maxLetterNo = num;
                    }
                    // Urinish ga tahlil as year + number (e.g., "20250005")
                    else if (letterNo.Length >= 4 && int.TryParse(letterNo.Substring(4), out int yearNum))
                    {
                        if (yearNum > maxLetterNo)
                            maxLetterNo = yearNum;
                    }
                }

                // Qaytarish next number in format "0123", "0124", etc.
                return (maxLetterNo + 1).ToString("D4");
            }
        }

        // Olish so'rovi: OfficialLetters/RegisterIncoming
        public ActionResult RegisterIncoming()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "RegisterIncoming"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "RegisterIncoming"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    // Olish hujjat type uchun OFFICIAL_LETTER
                    var docType = await db.DOC_DOCUMENT_TYPES
                        .FirstOrDefaultAsync(dt => dt.Code == "OFFICIAL_LETTER");

                    if (docType == null)
                    {
                        TempData["ErrorMessage"] = "Hujjat turi topilmadi";
                        return View(vm);
                    }

                    // Olish qoralama holat
                    var statusDraft = await db.DOC_DOCUMENT_STATUS_REF
                        .FirstOrDefaultAsync(s => s.Code == "DRAFT" && s.IsActive);

                    if (statusDraft == null)
                    {
                        TempData["ErrorMessage"] = "Holat topilmadi";
                        return View(vm);
                    }

                    // Olish current user
                    var currentUser = await db.USERS
                        .FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);

                    // Yaratish hujjat
                    var doc = new DOC_DOCUMENTS
                    {
                        DocTypeID = docType.ID,
                        StatusID = statusDraft.ID,
                        Code = $"IN-{vm.LetterNo}",
                        Title = $"Kiruvchi xat: {vm.CompanyOrPersonName}",
                        CreatedDT = DateTime.Now,
                        IntegrationUID = Guid.NewGuid(),
                        CreatedByUserID = currentUser?.ID
                    };

                    db.DOC_DOCUMENTS.Add(doc);
                    await db.SaveChangesAsync();

                    // Yaratish official letter record
                    var letter = new COM_OFFICIAL_LETTERS
                    {
                        DocumentID = doc.ID,
                        DirectionCode = "IN",
                        LetterNo = vm.LetterNo,
                        LetterDate = vm.LetterDate ?? DateTime.Now,
                        ExternalCompanyName = vm.CompanyOrPersonName,
                        ExternalContactName = null,
                        Topic = $"Kiruvchi xat: {vm.CompanyOrPersonName}"
                    };

                    db.COM_OFFICIAL_LETTERS.Add(letter);

                    // Handle fayl upload
                    if (vm.AttachmentFile != null && vm.AttachmentFile.ContentLength > 0)
                    {
                        // Tekshirish fayl type (PDF faqat)
                        string fileExtension = System.IO.Path.GetExtension(vm.AttachmentFile.FileName).ToLower();
                        if (fileExtension != ".pdf")
                        {
                            ModelState.AddModelError("AttachmentFile", "Faqat PDF fayllarni yuklash mumkin");
                            return View(vm);
                        }

                        // Tekshirish fayl size (max 10MB)
                        if (vm.AttachmentFile.ContentLength > 10 * 1024 * 1024)
                        {
                            ModelState.AddModelError("AttachmentFile", "Fayl hajmi 10 MB dan oshmasligi kerak");
                            return View(vm);
                        }

                        // O'qish fayl content
                        byte[] fileContent;
                        using (var binaryReader = new System.IO.BinaryReader(vm.AttachmentFile.InputStream))
                        {
                            fileContent = binaryReader.ReadBytes(vm.AttachmentFile.ContentLength);
                        }

                        // Saqlash fayl ga DOC_FILES
                        var docFile = new DOC_FILES
                        {
                            DocumentID = doc.ID,
                            VersionNo = 1,
                            FileName = vm.AttachmentFile.FileName,
                            FileExtension = fileExtension,
                            MimeType = vm.AttachmentFile.ContentType ?? "application/pdf",
                            FileSizeBytes = vm.AttachmentFile.ContentLength,
                            FileContent = fileContent,
                            IsMain = true,
                            CreatedByUserID = currentUser?.ID,
                            CreatedDT = DateTime.Now
                        };

                        db.DOC_FILES.Add(docFile);
                    }

                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "RegisterIncoming", 
                        $"Kiruvchi xat ro'yxatga olindi: {vm.LetterNo} - {vm.CompanyOrPersonName}");

                    TempData["Success"] = "Kiruvchi xat muvaffaqiyatli ro'yxatga olindi";
                    return RedirectToAction("Index");
                }
            }

            return View(vm);
        }
    }
}

