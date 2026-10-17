using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Sotuv shartnomalari. Server tomonidagi ruxsat — [Permission] (CRM-S01: avval faqat Delete'da bor edi).
    /// </summary>
    [Permission]
    public class SContractController : BaseController
    {
        private static readonly Regex IndexPattern = new Regex(@"^ProductList\[(\d+)\]\.ProductID$", RegexOptions.Compiled);

        // Olish so'rovi: SContract | Index
        public async Task<ActionResult> Index(string searchTerm, string contractNo, int? customerID, DateTime? fromDate, DateTime? toDate, string sortOrder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.S_CONTRACTS.AsNoTracking()
                    .Include(sc => sc.CUSTOMER)
                    .Include(sc => sc.CURRENCy)
                    .Where(sc => sc.IsDeleted == false);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c =>
                        c.ContractNo.Contains(searchTerm) ||
                        (c.Incoterms != null && c.Incoterms.Contains(searchTerm)) ||
                        (c.PaymentTerms != null && c.PaymentTerms.Contains(searchTerm)));
                }

                if (!string.IsNullOrEmpty(contractNo))
                {
                    query = query.Where(c => c.ContractNo.Contains(contractNo));
                }

                if (customerID.HasValue)
                {
                    query = query.Where(c => c.CustomerID == customerID.Value);
                }

                if (fromDate.HasValue)
                {
                    var from = fromDate.Value.Date;
                    query = query.Where(c => c.IssuedDate >= from);
                }

                if (toDate.HasValue)
                {
                    var toExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(c => c.IssuedDate < toExclusive);
                }

                switch (sortOrder)
                {
                    case "contractNo_desc":
                        query = query.OrderByDescending(c => c.ContractNo);
                        break;
                    case "contractNo":
                        query = query.OrderBy(c => c.ContractNo);
                        break;
                    case "date":
                        query = query.OrderBy(c => c.IssuedDate);
                        break;
                    default:
                        query = query.OrderByDescending(c => c.IssuedDate);
                        break;
                }

                var contracts = await query.ToListAsync();

                ViewBag.Customers = SalesLookups.Customers(customerID);
                ViewBag.SearchTerm = searchTerm;
                ViewBag.ContractNo = contractNo;
                ViewBag.CustomerID = customerID;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.SortOrder = sortOrder;

                return View(contracts);
            }
        }

        // Olish so'rovi: yaratish
        [HttpGet]
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = new SContractViewModel
                {
                    ProductList = new List<SContractProductViewModel>(),
                    IssuedDate = DateTime.Now
                };

                PopulateContractLookups(db, model, "CustomerID");
                return View(model);
            }
        }

        // POST so'rovi: yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SContractViewModel model, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                model.ProductList = ParseProductList(form);

                if (!model.ProductList.Any())
                {
                    ModelState.AddModelError("", "Kamida bitta maxsulot qo'shish kerak");
                }

                if (!ModelState.IsValid)
                {
                    PopulateContractLookups(db, model, "CustomerID");
                    return View(model);
                }

                try
                {
                    var newContract = new S_CONTRACTS
                    {
                        ContractNo = model.ContractNo,
                        CustomerID = model.CustomerID,
                        IssuedDate = model.IssuedDate,
                        DueDate = model.DueDate,
                        Incoterms = model.Incoterms,
                        PaymentTerms = model.PaymentTerms,
                        CurrencyID = model.CurrencyID,
                        CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                        IsDeleted = false
                    };

                    foreach (var product in model.ProductList)
                    {
                        var line = new S_CONTRACT_PRODUCTS();
                        ApplyLine(line, product);
                        newContract.S_CONTRACT_PRODUCTS.Add(line);
                    }

                    // Summa saqlangan qatorlardan (avval formadagi indekslar bo'yicha qayta o'qilardi)
                    newContract.Amount = SContractVatHelper.CalculateContractTotalWithVat(newContract.S_CONTRACT_PRODUCTS);

                    db.S_CONTRACTS.Add(newContract);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(CurrentUserName, "SContractController", $"{newContract.ContractNo} - SContractni yaratdi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Xatolik: " + InnermostMessage(ex));
                    PopulateContractLookups(db, model, "CustomerID");
                    return View(model);
                }
            }
        }

        // Olish so'rovi: Details
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                                 .Include(c => c.CUSTOMER)
                                 .Include(c => c.CURRENCy)
                                 .FirstOrDefault(p => p.ID == id && p.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var productList = db.S_CONTRACT_PRODUCTS
                                    .Include(pl => pl.PRODUCT)
                                    .Include(pl => pl.UNIT)
                                    .Where(pl => pl.ContractID == contract.ID
                                        && (pl.PRODUCT == null || pl.PRODUCT.IsDeleted != true))
                                    .ToList();

                ViewBag.ProductList = productList;

                // CURRENCy1 view'da ishlatiladi — kontekst yopilgandan keyin lazy-load yiqilmasligi uchun (CRM-B14)
                var linkedOrders = db.S_ORDERS
                    .Include(o => o.CURRENCy1)
                    .Where(o => o.ContractID == contract.ID && (o.IsDeleted == false || o.IsDeleted == null))
                    .ToList();
                ViewBag.LinkedOrders = linkedOrders;
                ViewBag.OrdersCount = linkedOrders.Count;

                var activeInvoicesQuery = db.SALES_INVOICES.Active().Where(i => i.ContractID == id);

                ViewBag.InvoicesCount = activeInvoicesQuery.Count();

                var contractedTotal = SContractVatHelper.CalculateContractTotalWithVat(productList);
                if (contractedTotal <= 0 && contract.Amount.HasValue)
                {
                    contractedTotal = contract.Amount.Value;
                }
                var orderIds = linkedOrders.Select(o => o.ID).ToList();

                ViewBag.ContractedTotal = contractedTotal;
                ViewBag.ContractedBase = productList.Sum(p => SContractVatHelper.GetLineBaseAmount(p));
                ViewBag.ContractedVat = productList.Sum(p => p.vatamount ?? 0);
                ViewBag.ContractedVatRate = SContractVatHelper.CalculateContractVatRate(productList);
                ViewBag.ContractedQty = productList.Sum(p => p.Amount);
                ViewBag.OrderedTotal = linkedOrders.Sum(o => o.Amount ?? 0);
                ViewBag.OrderedQty = orderIds.Count == 0
                    ? 0d
                    : db.S_ORDER_PRODUCTS
                        .Where(op => orderIds.Contains(op.OrderID))
                        .Sum(op => (double?)op.Amount) ?? 0d;
                ViewBag.InvoicedTotal = activeInvoicesQuery
                    .Sum(i => (decimal?)(i.TotalWithVAT ?? i.TotalAmount)) ?? 0;
                ViewBag.InvoicedQty = db.SALES_INVOICE_LINES.Active()
                    .Where(l => l.SALES_INVOICES.ContractID == id)
                    .Sum(l => (decimal?)l.Qty) ?? 0m;

                return View(contract);
            }
        }

        // Olish so'rovi: Edit
        [HttpGet]
        public ActionResult Edit(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                    .Include(x => x.S_CONTRACT_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefault(x => x.ID == id && x.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var model = new SContractViewModel
                {
                    ID = contract.ID,
                    ContractNo = contract.ContractNo,
                    CustomerID = contract.CustomerID,
                    IssuedDate = contract.IssuedDate,
                    DueDate = contract.DueDate,
                    Incoterms = contract.Incoterms,
                    PaymentTerms = contract.PaymentTerms,
                    CurrencyID = contract.CurrencyID,
                    Amount = (decimal)SContractVatHelper.CalculateContractTotalWithVat(contract.S_CONTRACT_PRODUCTS),
                    ProductList = contract.S_CONTRACT_PRODUCTS.Select(p => new SContractProductViewModel
                    {
                        ID = p.ID,
                        SContractID = p.ContractID,
                        ProductID = p.ProductID,
                        ProductPNo = p.PRODUCT.PNo,
                        PiecePrice = p.PiecePrice,
                        UnitID = p.UnitID,
                        Amount = p.Amount,
                        VatPercent = SContractVatHelper.GetVatPercentFromLine(
                            SContractVatHelper.GetLineBaseAmount(p), p.vatamount),
                        PRODUCT = new ProductViewModel
                        {
                            ID = p.PRODUCT.ID,
                            PNo = p.PRODUCT.PNo,
                            Name = p.PRODUCT.Name
                        }
                    }).ToList()
                };

                PopulateContractLookups(db, model, "Customers");
                return View(model);
            }
        }

        // POST so'rovi: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SContractViewModel model, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                model.ProductList = ParseProductList(form);

                if (!model.ProductList.Any())
                {
                    ModelState.AddModelError("", "Kamida bitta mahsulot kerak");
                }

                if (!ModelState.IsValid)
                {
                    return EditError(db, model);
                }

                try
                {
                    var contractToUpdate = db.S_CONTRACTS
                        .Include(c => c.S_CONTRACT_PRODUCTS)
                        .FirstOrDefault(c => c.ID == model.ID && c.IsDeleted == false);

                    if (contractToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    // Formada yo'q qatorlar o'chiriladi — lekin buyurtma/invoysda ishlatilgan mahsulot bo'lsa rad etiladi
                    var postedIds = model.ProductList.Where(p => p.ID > 0).Select(p => p.ID).ToList();
                    var productsToRemove = contractToUpdate.S_CONTRACT_PRODUCTS
                        .Where(p => !postedIds.Contains(p.ID))
                        .ToList();

                    var keptProductIds = new HashSet<int>(model.ProductList.Select(p => p.ProductID));
                    foreach (var removed in productsToRemove.Where(p => !keptProductIds.Contains(p.ProductID)))
                    {
                        var usage = ProductUsage(db, contractToUpdate.ID, removed.ProductID);
                        if (usage != null)
                        {
                            ModelState.AddModelError("", usage);
                            return EditError(db, model);
                        }
                    }

                    contractToUpdate.ContractNo = model.ContractNo;
                    contractToUpdate.CustomerID = model.CustomerID;
                    contractToUpdate.IssuedDate = model.IssuedDate;
                    contractToUpdate.DueDate = model.DueDate;
                    contractToUpdate.Incoterms = model.Incoterms;
                    contractToUpdate.PaymentTerms = model.PaymentTerms;
                    contractToUpdate.CurrencyID = model.CurrencyID;

                    db.S_CONTRACT_PRODUCTS.RemoveRange(productsToRemove);

                    var existingById = contractToUpdate.S_CONTRACT_PRODUCTS.ToDictionary(p => p.ID);
                    foreach (var product in model.ProductList)
                    {
                        S_CONTRACT_PRODUCTS line;
                        if (product.ID <= 0 || !existingById.TryGetValue(product.ID, out line))
                        {
                            line = new S_CONTRACT_PRODUCTS { ContractID = contractToUpdate.ID };
                            db.S_CONTRACT_PRODUCTS.Add(line);
                        }
                        ApplyLine(line, product);
                    }

                    // Summa saqlanadigan qatorlardan hisoblanadi
                    var finalLines = contractToUpdate.S_CONTRACT_PRODUCTS.Where(p => !productsToRemove.Contains(p))
                        .Concat(db.ChangeTracker.Entries<S_CONTRACT_PRODUCTS>()
                            .Where(e => e.State == EntityState.Added)
                            .Select(e => e.Entity))
                        .Distinct()
                        .ToList();
                    contractToUpdate.Amount = SContractVatHelper.CalculateContractTotalWithVat(finalLines);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(CurrentUserName, "SContractController", $"{contractToUpdate.ContractNo} - SContractni tahrirladi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Xatolik: " + InnermostMessage(ex));
                    return EditError(db, model);
                }
            }
        }

        // Olish so'rovi: o'chirish
        [HttpGet]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                    .Include(c => c.CUSTOMER)
                    .Include(c => c.CURRENCy)
                    .FirstOrDefault(c => c.ID == id && c.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ProductList = db.S_CONTRACT_PRODUCTS
                    .Include(pl => pl.PRODUCT)
                    .Include(pl => pl.UNIT)
                    .Where(pl => pl.ContractID == contract.ID)
                    .ToList();

                return View(contract);
            }
        }

        // POST so'rovi: o'chirish
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = await db.S_CONTRACTS.FirstOrDefaultAsync(c => c.ID == id && c.IsDeleted == false);
                if (contract != null)
                {
                    contract.IsDeleted = true;
                    await SoftDeleteReferenceService.AfterSContractSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(CurrentUserName, "SContractController", $"{contract.ContractNo} - SContractni o'chirdi");
                }

                return RedirectToAction("Index");
            }
        }

        // POST: DeleteProduct (CRM-S02: POST + CSRF; buyurtma/invoysda ishlatilgan mahsulot o'chirilmaydi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProduct(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractProduct = db.S_CONTRACT_PRODUCTS.FirstOrDefault(cp => cp.ID == id);
                if (contractProduct == null)
                {
                    return RedirectToAction("Index");
                }

                int contractID = contractProduct.ContractID;
                bool duplicateExists = db.S_CONTRACT_PRODUCTS.Any(cp => cp.ContractID == contractID && cp.ProductID == contractProduct.ProductID && cp.ID != id);
                var usage = duplicateExists ? null : ProductUsage(db, contractID, contractProduct.ProductID);
                if (usage != null)
                {
                    TempData["ErrorMessage"] = usage;
                    return RedirectToAction("Edit", new { id = contractID });
                }

                db.S_CONTRACT_PRODUCTS.Remove(contractProduct);
                db.SaveChanges();

                var remainingProducts = db.S_CONTRACT_PRODUCTS.Where(cp => cp.ContractID == contractID).ToList();
                var contract = db.S_CONTRACTS.Find(contractID);
                if (contract != null)
                {
                    contract.Amount = SContractVatHelper.CalculateContractTotalWithVat(remainingProducts);
                    db.SaveChanges();
                }

                LogHelper.LogToDatabase(CurrentUserName, "SContractController", "SContractProductni o'chirdi va shartnoma summasini yangiladi");

                return RedirectToAction("Edit", new { id = contractID });
            }
        }

        // Buyurtmalarni ko'rish/bog'lash (view o'zgartirish huquqini talab qiladi)
        [Permission(Change = true)]
        public ActionResult ManageOrders(int? contractID)
        {
            if (contractID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                    .Include(c => c.CUSTOMER)
                    .FirstOrDefault(c => c.ID == contractID && c.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Contract = contract;
                ViewBag.LinkedOrders = db.S_ORDERS
                    .Include(o => o.CURRENCy1)
                    .Where(o => o.ContractID == contract.ID && o.IsDeleted == false)
                    .ToList();
                // Faqat shartnomasiz (yoki shartnomasi o'chirilgan) buyurtmalar — boshqa faol shartnomadagini
                // "o'g'irlab" bo'lmaydi (CRM-B12)
                ViewBag.UnlinkedOrders = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.CURRENCy1)
                    .Where(o => o.CustomerID == contract.CustomerID &&
                                o.IsDeleted == false &&
                                o.ContractID != contract.ID &&
                                (o.ContractID == 0 || o.S_CONTRACTS == null || o.S_CONTRACTS.IsDeleted == true))
                    .ToList();

                return View(contract);
            }
        }

        // POST so'rovi: Link Order ga Contract
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public ActionResult LinkOrder(int contractID, int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS.Include(o => o.S_CONTRACTS).FirstOrDefault(o => o.ID == orderID);
                var contract = db.S_CONTRACTS.FirstOrDefault(c => c.ID == contractID && c.IsDeleted == false);
                bool orderIsFree = order != null && (order.ContractID == 0 || order.S_CONTRACTS == null || order.S_CONTRACTS.IsDeleted == true);
                if (order != null && contract != null && order.CustomerID == contract.CustomerID && orderIsFree)
                {
                    order.ContractID = contractID;
                    db.SaveChanges();
                    LogHelper.LogToDatabase(CurrentUserName, "SContractController", $"Buyurtma {order.OrderNo} ni shartnomaga bog'ladi");
                }
                else
                {
                    TempData["ErrorMessage"] = "Faqat shu mijozning shartnomasiz buyurtmasini bog'lash mumkin.";
                }
                return RedirectToAction("ManageOrders", new { contractID = contractID });
            }
        }

        /// <summary>
        /// S_ORDERS.ContractID majburiy FK — buyurtmani shartnomadan "ajratish" (ContractID = 0) FK xatosiga
        /// olib kelardi (CRM-B12). Endi rad etiladi: buyurtmani o'chiring yoki tahrirda boshqa shartnoma tanlang.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public ActionResult UnlinkOrder(int contractID, int orderID)
        {
            TempData["ErrorMessage"] = "Buyurtmani shartnomasiz qoldirib bo'lmaydi (shartnoma majburiy). Buyurtmani tahrirlab boshqa shartnoma tanlang yoki o'chiring.";
            return RedirectToAction("ManageOrders", new { contractID = contractID });
        }

        // ───────────────────────────── yordamchi metodlar ─────────────────────────────

        /// <summary>
        /// Formadagi "ProductList[i].*" qatorlari. Indekslarda bo'shliq bo'lsa ham hammasi o'qiladi
        /// (CRM-B08: avval model binder bo'shliqdan keyingi qatorlarni tashlab yuborar, Edit esa ularni
        /// bazadan O'CHIRARDI). Sonlar madaniyatdan qat'i nazar o'qiladi (CRM-B19: int.Parse/double.Parse yiqilardi),
        /// QQS % shu qatorning o'zidan olinadi (avval qisqargan ro'yxat indeksi bo'yicha — boshqa qatorniki).
        /// </summary>
        private List<SContractProductViewModel> ParseProductList(FormCollection form)
        {
            // Bog'langan ro'yxatdagi madaniyat xatolari ("12.5" uz-UZ'da) — qo'lda o'qilgani uchun e'tiborsiz
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("ProductList")).ToList())
                ModelState.Remove(key);

            var result = new List<SContractProductViewModel>();
            var indexes = form.AllKeys
                .Where(k => k != null)
                .Select(k => IndexPattern.Match(k))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .OrderBy(i => i);

            foreach (var i in indexes)
            {
                int productId, unitId, lineId;
                if (!int.TryParse(form[$"ProductList[{i}].ProductID"], out productId) || productId <= 0)
                    continue;
                int.TryParse(form[$"ProductList[{i}].UnitID"], out unitId);
                int.TryParse(form[$"ProductList[{i}].ID"], out lineId);
                var piecePrice = CrmNumber.Double(form[$"ProductList[{i}].PiecePrice"]);
                var amount = CrmNumber.Double(form[$"ProductList[{i}].Amount"]);
                var vatPercent = Math.Max(0, Math.Min(100, CrmNumber.Double(form[$"ProductList[{i}].VatPercent"])));

                if (amount <= 0 || piecePrice <= 0)
                {
                    ModelState.AddModelError("", "Har bir mahsulot uchun miqdor va narx 0 dan katta bo'lishi kerak.");
                    continue;
                }

                result.Add(new SContractProductViewModel
                {
                    ID = lineId,
                    ProductID = productId,
                    PiecePrice = piecePrice,
                    Amount = amount,
                    UnitID = unitId,
                    VatPercent = vatPercent
                });
            }
            return result;
        }

        private static void ApplyLine(S_CONTRACT_PRODUCTS line, SContractProductViewModel product)
        {
            var lineBase = SContractVatHelper.GetLineBaseAmount(product.PiecePrice, product.Amount);
            var vatAmount = SContractVatHelper.CalculateVatAmount(lineBase, product.VatPercent);
            line.ProductID = product.ProductID;
            line.PiecePrice = product.PiecePrice;
            line.Amount = product.Amount;
            line.UnitID = product.UnitID;
            line.vatamount = vatAmount;
            // totalamount GetLineTotalWithVat'da ustun turadi — eskirib qolmasligi uchun yangilanadi
            line.totalamount = lineBase + vatAmount;
        }

        /// <summary>Mahsulot shu shartnoma bo'yicha buyurtma yoki faol invoysda ishlatilgan bo'lsa — xabar, aks holda null.</summary>
        private static string ProductUsage(DBTHSNEntities db, int contractId, int productId)
        {
            bool ordered = db.S_ORDER_PRODUCTS.Any(op => op.ProductID == productId
                && op.S_ORDERS.ContractID == contractId
                && (op.S_ORDERS.IsDeleted == false || op.S_ORDERS.IsDeleted == null));
            bool invoiced = db.SALES_INVOICE_LINES.Active().Any(l => l.ProductID == productId && l.SALES_INVOICES.ContractID == contractId);
            if (!ordered && !invoiced)
                return null;

            var pno = db.PRODUCTS.Where(p => p.ID == productId).Select(p => p.PNo).FirstOrDefault() ?? productId.ToString();
            return $"{pno} mahsuloti shu shartnoma bo'yicha {(ordered ? "buyurtmada" : "invoysda")} ishlatilgan — uni shartnomadan o'chirib bo'lmaydi.";
        }

        private ActionResult EditError(DBTHSNEntities db, SContractViewModel model)
        {
            // Mavjud qatorlarning mahsulot kodi (view ko'rsatadi)
            var ids = model.ProductList.Select(p => p.ProductID).Distinct().ToList();
            var pnos = db.PRODUCTS.AsNoTracking().Where(p => ids.Contains(p.ID)).Select(p => new { p.ID, p.PNo, p.Name }).ToList().ToDictionary(p => p.ID);
            foreach (var p in model.ProductList)
            {
                if (pnos.TryGetValue(p.ProductID, out var info))
                {
                    p.ProductPNo = info.PNo;
                    p.PRODUCT = new ProductViewModel { ID = info.ID, PNo = info.PNo, Name = info.Name };
                }
            }
            PopulateContractLookups(db, model, "Customers");
            return View(model);
        }

        /// <summary>Shartnoma formasi ro'yxatlari (8 nusxa o'rniga — D13). Mijoz/birlik/valyuta keshdan.</summary>
        private void PopulateContractLookups(DBTHSNEntities db, SContractViewModel model, string customersKey)
        {
            ViewData[customersKey] = SalesLookups.Customers(model.CustomerID > 0 ? (int?)model.CustomerID : null);
            ViewBag.Products = new SelectList(db.PRODUCTS.AsNoTracking()
                .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                .OrderBy(p => p.PNo)
                .Select(p => new { p.ID, p.PNo })
                .ToList(), "ID", "PNo");
            ViewBag.Units = new SelectList(ReferenceDataCache.Get(ReferenceDataCache.Units), "ID", "Extra");
            ViewBag.currencies = new SelectList(ReferenceDataCache.Get(ReferenceDataCache.Currencies), "ID", "Name", model.CurrencyID > 0 ? (object)model.CurrencyID : null);
        }
    }
}
