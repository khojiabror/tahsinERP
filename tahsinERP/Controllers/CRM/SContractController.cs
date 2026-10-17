using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;
using System.Web.Security;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class SContractController : Controller
    {
        // Olish so'rovi: SContract | Index
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string contractNo, int? customerID, DateTime? fromDate, DateTime? toDate, string sortOrder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.S_CONTRACTS
                    .Include(sc => sc.CUSTOMER)
                    .Include(sc => sc.CURRENCy)
                    .Where(sc => sc.IsDeleted == false);

                // Filters
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
                    query = query.Where(c => c.IssuedDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(c => c.IssuedDate <= toDate.Value);
                }

                // Sorting
                switch (sortOrder)
                {
                    case "contractNo_desc":
                        query = query.OrderByDescending(c => c.ContractNo);
                        break;
                    case "contractNo":
                        query = query.OrderBy(c => c.ContractNo);
                        break;
                    case "date_desc":
                        query = query.OrderByDescending(c => c.IssuedDate);
                        break;
                    case "date":
                        query = query.OrderBy(c => c.IssuedDate);
                        break;
                    default:
                        query = query.OrderByDescending(c => c.IssuedDate);
                        break;
                }

                var contracts = await query.ToListAsync();

                // ViewBag uchun filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "Name", customerID);
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

                ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

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
                // Tahlil qilish products dan form repeater
                if (model.ProductList == null || !model.ProductList.Any())
                {
                    model.ProductList = new List<SContractProductViewModel>();
                    int index = 0;
                    while (form[$"ProductList[{index}].ProductID"] != null)
                    {
                        var productIDStr = form[$"ProductList[{index}].ProductID"];
                        if (!string.IsNullOrEmpty(productIDStr) && productIDStr != "0")
                        {
                            var productID = int.Parse(productIDStr);
                            var piecePrice = double.Parse(form[$"ProductList[{index}].PiecePrice"] ?? "0");
                            var amount = int.Parse(form[$"ProductList[{index}].Amount"] ?? "0");
                            var unitID = int.Parse(form[$"ProductList[{index}].UnitID"] ?? "0");

                            if (productID > 0 && amount > 0 && piecePrice > 0)
                            {
                                model.ProductList.Add(new SContractProductViewModel
                                {
                                    ProductID = productID,
                                    PiecePrice = piecePrice,
                                    Amount = amount,
                                    UnitID = unitID
                                });
                            }
                        }
                        index++;
                    }
                }

                // Validation
                if (model.ProductList == null || !model.ProductList.Any())
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
                    ModelState.AddModelError("", "Kamida bitta maxsulot qo'shish kerak");
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
                    return View(model);
                }

                try
                {
                    var totalAmount = SContractVatHelper.CalculateContractTotalWithVatFromForm(form);

                    // Yaratish contract
                    var newContract = new S_CONTRACTS
                    {
                        ContractNo = model.ContractNo,
                        CustomerID = model.CustomerID,
                        IssuedDate = model.IssuedDate,
                        DueDate = model.DueDate,
                        Incoterms = model.Incoterms,
                        PaymentTerms = model.PaymentTerms,
                        CurrencyID = model.CurrencyID,
                        Amount = totalAmount,
                        CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                        IsDeleted = false
                    };

                    db.S_CONTRACTS.Add(newContract);
                    db.SaveChanges();

                    // Qo'shish products
                    for (int i = 0; i < model.ProductList.Count; i++)
                    {
                        var product = model.ProductList[i];
                        var vatPercent = SContractVatHelper.ParseVatPercentFromForm(form, i);
                        var lineBase = SContractVatHelper.GetLineBaseAmount(product.PiecePrice, product.Amount);
                        var vatAmount = SContractVatHelper.CalculateVatAmount(lineBase, vatPercent);

                        var contractProduct = new S_CONTRACT_PRODUCTS
                        {
                            ContractID = newContract.ID,
                            ProductID = product.ProductID,
                            PiecePrice = product.PiecePrice,
                            Amount = product.Amount,
                            UnitID = product.UnitID,
                            vatamount = vatAmount
                        };
                        db.S_CONTRACT_PRODUCTS.Add(contractProduct);
                    }

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{newContract.ContractNo} - SContractni yaratdi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
                    var errorMessage = $"Xatolik: {ex.Message}";
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                        innerEx = innerEx.InnerException;
                    }
                    ModelState.AddModelError("", errorMessage);
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

                var linkedOrders = db.S_ORDERS
                    .Where(o => o.ContractID == contract.ID && (o.IsDeleted == false || o.IsDeleted == null))
                    .ToList();
                ViewBag.LinkedOrders = linkedOrders;
                ViewBag.OrdersCount = linkedOrders.Count;

                var activeInvoicesQuery = db.SALES_INVOICES
                    .Where(i => i.ContractID == id
                        && (i.StatusCode == null || (i.StatusCode != "CANCELED" && i.StatusCode != "CANCELLED")));

                ViewBag.InvoicesCount = activeInvoicesQuery.Count();

                // Use total (QQS bilan) from contract products stored in DB
                var contractedTotal = SContractVatHelper.CalculateContractTotalWithVat(productList);
                if (contractedTotal <= 0 && contract.Amount.HasValue)
                {
                    contractedTotal = contract.Amount.Value;
                }
                var contractedBase = productList.Sum(p => SContractVatHelper.GetLineBaseAmount(p));
                var contractedVat = productList.Sum(p => p.vatamount ?? 0);
                var contractedVatRate = SContractVatHelper.CalculateContractVatRate(productList);
                var contractedQty = productList.Sum(p => p.Amount);
                var orderedTotal = linkedOrders.Sum(o => o.Amount ?? 0);
                var orderIds = linkedOrders.Select(o => o.ID).ToList();
                var orderedQty = orderIds.Count == 0
                    ? 0d
                    : db.S_ORDER_PRODUCTS
                        .Where(op => orderIds.Contains(op.OrderID))
                        .Sum(op => (double?)op.Amount) ?? 0d;
                var invoicedTotal = activeInvoicesQuery
                    .Sum(i => (decimal?)(i.TotalWithVAT ?? i.TotalAmount)) ?? 0;
                var invoicedQty = db.SALES_INVOICE_LINES
                    .Where(l => l.SALES_INVOICES.ContractID == id
                        && (l.SALES_INVOICES.StatusCode == null
                            || (l.SALES_INVOICES.StatusCode != "CANCELED" && l.SALES_INVOICES.StatusCode != "CANCELLED")))
                    .Sum(l => (decimal?)l.Qty) ?? 0m;

                ViewBag.ContractedTotal = contractedTotal;
                ViewBag.ContractedBase = contractedBase;
                ViewBag.ContractedVat = contractedVat;
                ViewBag.ContractedVatRate = contractedVatRate;
                ViewBag.ContractedQty = contractedQty;
                ViewBag.OrderedTotal = orderedTotal;
                ViewBag.OrderedQty = orderedQty;
                ViewBag.InvoicedTotal = invoicedTotal;
                ViewBag.InvoicedQty = invoicedQty;

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
                    CustomerID = (int)contract.CustomerID,
                    IssuedDate = contract.IssuedDate,
                    DueDate = contract.DueDate,
                    Incoterms = contract.Incoterms,
                    PaymentTerms = contract.PaymentTerms,
                    CurrencyID = (int)contract.CurrencyID,
                    Amount = (decimal)SContractVatHelper.CalculateContractTotalWithVat(contract.S_CONTRACT_PRODUCTS),
                    ProductList = contract.S_CONTRACT_PRODUCTS.Select(p => new SContractProductViewModel
                    {
                        ID = p.ID,
                        SContractID = p.ContractID,
                        ProductID = p.ProductID,
                        ProductPNo = p.PRODUCT.PNo,
                        PiecePrice = p.PiecePrice,
                        UnitID = p.UnitID,
                        Amount = (int)p.Amount,
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

                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", contract.CustomerID);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", contract.CurrencyID);

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
                // Tahlil qilish products dan form
                if (model.ProductList == null || !model.ProductList.Any())
                {
                    model.ProductList = new List<SContractProductViewModel>();
                    int index = 0;
                    while (form[$"ProductList[{index}].ProductID"] != null)
                    {
                        var productIDStr = form[$"ProductList[{index}].ProductID"];
                        var idStr = form[$"ProductList[{index}].ID"];
                        
                        if (!string.IsNullOrEmpty(productIDStr) && productIDStr != "0")
                        {
                            var productID = int.Parse(productIDStr);
                            var piecePrice = double.Parse(form[$"ProductList[{index}].PiecePrice"] ?? "0");
                            var amount = int.Parse(form[$"ProductList[{index}].Amount"] ?? "0");
                            var unitID = int.Parse(form[$"ProductList[{index}].UnitID"] ?? "0");
                            var id = !string.IsNullOrEmpty(idStr) ? int.Parse(idStr) : 0;

                            if (productID > 0 && amount > 0 && piecePrice > 0)
                            {
                                model.ProductList.Add(new SContractProductViewModel
                                {
                                    ID = id,
                                    ProductID = productID,
                                    PiecePrice = piecePrice,
                                    Amount = amount,
                                    UnitID = unitID
                                });
                            }
                        }
                        index++;
                    }
                }

                // Validation
                if (model.ProductList == null || !model.ProductList.Any())
                {
                    ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
                    ModelState.AddModelError("", "Kamida bitta mahsulot kerak");
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
                    return View(model);
                }

                try
                {
                    var contractToUpdate = db.S_CONTRACTS
                        .Include(c => c.S_CONTRACT_PRODUCTS)
                        .FirstOrDefault(c => c.ID == model.ID);

                    if (contractToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    // Yangilash contract basic info
                    contractToUpdate.ContractNo = model.ContractNo;
                    contractToUpdate.CustomerID = model.CustomerID;
                    contractToUpdate.IssuedDate = model.IssuedDate;
                    contractToUpdate.DueDate = model.DueDate;
                    contractToUpdate.Incoterms = model.Incoterms;
                    contractToUpdate.PaymentTerms = model.PaymentTerms;
                    contractToUpdate.CurrencyID = model.CurrencyID;

                    // O'chirish products not in model
                    var modelProductIDs = model.ProductList.Where(p => p.ID > 0).Select(p => p.ID).ToList();
                    var productsToRemove = contractToUpdate.S_CONTRACT_PRODUCTS
                        .Where(p => !modelProductIDs.Contains(p.ID))
                        .ToList();

                    foreach (var productToRemove in productsToRemove)
                    {
                        db.S_CONTRACT_PRODUCTS.Remove(productToRemove);
                    }

                    // Yangilash/add products
                    for (int i = 0; i < model.ProductList.Count; i++)
                    {
                        var product = model.ProductList[i];
                        var vatPercent = SContractVatHelper.ParseVatPercentFromForm(form, i);
                        var lineBase = SContractVatHelper.GetLineBaseAmount(product.PiecePrice, product.Amount);
                        var vatAmount = SContractVatHelper.CalculateVatAmount(lineBase, vatPercent);

                        if (product.ID > 0)
                        {
                            var existing = db.S_CONTRACT_PRODUCTS.Find(product.ID);
                            if (existing != null)
                            {
                                existing.ProductID = product.ProductID;
                                existing.PiecePrice = product.PiecePrice;
                                existing.UnitID = product.UnitID;
                                existing.Amount = product.Amount;
                                existing.vatamount = vatAmount;
                                db.Entry(existing).State = EntityState.Modified;
                            }
                        }
                        else
                        {
                            var newProduct = new S_CONTRACT_PRODUCTS
                            {
                                ContractID = contractToUpdate.ID,
                                ProductID = product.ProductID,
                                PiecePrice = product.PiecePrice,
                                UnitID = product.UnitID,
                                Amount = product.Amount,
                                vatamount = vatAmount
                            };
                            db.S_CONTRACT_PRODUCTS.Add(newProduct);
                        }
                    }

                    var totalAmount = SContractVatHelper.CalculateContractTotalWithVatFromForm(form);

                    // Yangilash contract amount
                    contractToUpdate.Amount = totalAmount;
                    db.Entry(contractToUpdate).State = EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{contractToUpdate.ContractNo} - SContractni tahrirladi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
                    var errorMessage = $"Xatolik: {ex.Message}";
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                        innerEx = innerEx.InnerException;
                    }
                    ModelState.AddModelError("", errorMessage);
                    return View(model);
                }
            }
        }

        // Olish so'rovi: o'chirish
        [HttpGet]
        public ActionResult Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "SContract", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "SContract", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = await db.S_CONTRACTS.FirstOrDefaultAsync(c => c.ID == id && c.IsDeleted == false);
                if (contract != null)
                {
                    contract.IsDeleted = true;
                    db.Entry(contract).State = EntityState.Modified;
                    await SoftDeleteReferenceService.AfterSContractSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{contract.ContractNo} - SContractni o'chirdi");
                }

                return RedirectToAction("Index");
            }
        }

        // POST: DeleteProduct (avval GET edi — CSRF orqali o'chirish mumkin edi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProduct(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "SContract", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractProduct = db.S_CONTRACT_PRODUCTS
                    .Include(cp => cp.S_CONTRACTS)
                    .Include(cp => cp.PRODUCT)
                    .FirstOrDefault(cp => cp.ID == id);

                if (contractProduct != null)
                {
                    int contractID = contractProduct.ContractID;
                    db.S_CONTRACT_PRODUCTS.Remove(contractProduct);
                    db.SaveChanges();

                    // Recalculate contract amount
                    var remainingProducts = db.S_CONTRACT_PRODUCTS
                        .Where(cp => cp.ContractID == contractID)
                        .ToList();

                    decimal totalAmount = 0;
                    foreach (var product in remainingProducts)
                    {
                        totalAmount += (decimal)SContractVatHelper.GetLineTotalWithVat(product);
                    }

                    var contract = db.S_CONTRACTS.Find(contractID);
                    if (contract != null)
                    {
                        contract.Amount = (double)Math.Round(totalAmount, MidpointRounding.AwayFromZero);
                        db.SaveChanges();
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"SContractProductni o'chirdi va shartnoma summasini yangiladi");

                    return RedirectToAction("Edit", new { id = contractID });
                }

                return RedirectToAction("Index");
            }
        }

        // Link/Unlink Orders
        public ActionResult ManageOrders(int? contractID)
        {
            if (contractID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                    .Include(c => c.S_ORDERS)
                    .FirstOrDefault(c => c.ID == contractID && c.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var unlinkedOrders = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Where(o => o.CustomerID == contract.CustomerID && 
                                o.IsDeleted == false && 
                                (o.ContractID == 0 || o.ContractID != contractID))
                    .ToList();

                ViewBag.Contract = contract;
                ViewBag.LinkedOrders = contract.S_ORDERS.Where(o => o.IsDeleted == false).ToList();
                ViewBag.UnlinkedOrders = unlinkedOrders;

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
                var order = db.S_ORDERS.Find(orderID);
                if (order != null && order.CustomerID == db.S_CONTRACTS.Find(contractID)?.CustomerID)
                {
                    order.ContractID = contractID;
                    db.SaveChanges();
                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"Buyurtma {order.OrderNo} ni shartnomaga bog'ladi");
                }
                return RedirectToAction("ManageOrders", new { contractID = contractID });
            }
        }

        // POST so'rovi: Unlink Order dan Contract
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public ActionResult UnlinkOrder(int contractID, int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS.Find(orderID);
                if (order != null && order.ContractID == contractID)
                {
                    order.ContractID = 0;
                    db.SaveChanges();
                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"Buyurtma {order.OrderNo} ni shartnomadan ajratdi");
                }
                return RedirectToAction("ManageOrders", new { contractID = contractID });
            }
        }
    }
}
