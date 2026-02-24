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
using tahsinERP.ViewModels;
using System.Web.Security;

namespace tahsinERP.Controllers
{
    public class SContractController : Controller
    {
        // Olish so'rovi: SContract | Index
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string contractNo, int? customerID, DateTime? fromDate, DateTime? toDate, string sortOrder, int? page, int? pageSize)
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

                // Pagination
                int currentPage = page ?? 1;
                int itemsPerPage = pageSize ?? 25;
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var contracts = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // ViewBag uchun filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "Name", customerID);
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;
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
                    // Calculate total amount dan products (including VAT agar provided)
                    decimal totalAmount = 0;
                    int totalIndex = 0;
                    while (form[$"ProductList[{totalIndex}].ProductID"] != null)
                    {
                        var productIDStr = form[$"ProductList[{totalIndex}].ProductID"];
                        if (!string.IsNullOrEmpty(productIDStr) && productIDStr != "0")
                        {
                            var piecePrice = double.Parse(form[$"ProductList[{totalIndex}].PiecePrice"] ?? "0");
                            var amount = int.Parse(form[$"ProductList[{totalIndex}].Amount"] ?? "0");
                            var vatPercent = double.Parse(form[$"ProductList[{totalIndex}].VatPercent"] ?? "12");
                            if (piecePrice > 0 && amount > 0)
                            {
                                var baseTotal = piecePrice * amount;
                                var totalWithVat = baseTotal + (baseTotal * vatPercent / 100);
                                totalAmount += (decimal)totalWithVat;
                            }
                        }
                        totalIndex++;
                    }

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
                        Amount = (int)totalAmount,
                        CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                        IsDeleted = false
                    };

                    db.S_CONTRACTS.Add(newContract);
                    db.SaveChanges();

                    // Qo'shish products
                    foreach (var product in model.ProductList)
                    {
                        var contractProduct = new S_CONTRACT_PRODUCTS
                        {
                            ContractID = newContract.ID,
                            ProductID = product.ProductID,
                            PiecePrice = product.PiecePrice,
                            Amount = product.Amount,
                            UnitID = product.UnitID
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
                                 .Include(c => c.S_ORDERS)
                                 .FirstOrDefault(p => p.ID == id);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var productList = db.S_CONTRACT_PRODUCTS
                                    .Include(pl => pl.PRODUCT)
                                    .Include(pl => pl.UNIT)
                                    .Where(pl => pl.ContractID == contract.ID)
                                    .ToList();

                ViewBag.ProductList = productList;

                var linkedOrders = contract.S_ORDERS.Where(o => o.IsDeleted == false).ToList();
                ViewBag.LinkedOrders = linkedOrders;
                ViewBag.OrdersCount = linkedOrders.Count;

                var invoicesCount = db.SALES_INVOICES.Where(i => i.ContractID == id).Count();
                ViewBag.InvoicesCount = invoicesCount;

                // Use contract.Amount instead of sum dan products
                var contractedTotal = contract.Amount ?? 0;
                var orderedTotal = linkedOrders.Sum(o => o.Amount ?? 0);
                var invoicedTotal = db.SALES_INVOICES
                    .Where(i => i.ContractID == id)
                    .Sum(i => (decimal?)i.TotalAmount) ?? 0;

                ViewBag.ContractedTotal = contractedTotal;
                ViewBag.OrderedTotal = orderedTotal;
                ViewBag.InvoicedTotal = invoicedTotal;

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
                    Amount = (int)contract.Amount,
                    ProductList = contract.S_CONTRACT_PRODUCTS.Select(p => new SContractProductViewModel
                    {
                        ID = p.ID,
                        SContractID = p.ContractID,
                        ProductID = p.ProductID,
                        ProductPNo = p.PRODUCT.PNo,
                        PiecePrice = p.PiecePrice,
                        UnitID = p.UnitID,
                        Amount = (int)p.Amount,
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
                    foreach (var product in model.ProductList)
                    {
                        if (product.ID > 0)
                        {
                            // Yangilash existing
                            var existing = db.S_CONTRACT_PRODUCTS.Find(product.ID);
                            if (existing != null)
                            {
                                existing.ProductID = product.ProductID;
                                existing.PiecePrice = product.PiecePrice;
                                existing.UnitID = product.UnitID;
                                existing.Amount = product.Amount;
                                db.Entry(existing).State = EntityState.Modified;
                            }
                        }
                        else
                        {
                            // Qo'shish new
                            var newProduct = new S_CONTRACT_PRODUCTS
                            {
                                ContractID = contractToUpdate.ID,
                                ProductID = product.ProductID,
                                PiecePrice = product.PiecePrice,
                                UnitID = product.UnitID,
                                Amount = product.Amount
                            };
                            db.S_CONTRACT_PRODUCTS.Add(newProduct);
                        }
                    }

                    // Calculate total amount (including VAT agar provided)
                    decimal totalAmount = 0;
                    int totalIndex = 0;
                    while (form[$"ProductList[{totalIndex}].ProductID"] != null)
                    {
                        var productIDStr = form[$"ProductList[{totalIndex}].ProductID"];
                        if (!string.IsNullOrEmpty(productIDStr) && productIDStr != "0")
                        {
                            var piecePrice = double.Parse(form[$"ProductList[{totalIndex}].PiecePrice"] ?? "0");
                            var amount = int.Parse(form[$"ProductList[{totalIndex}].Amount"] ?? "0");
                            var vatPercent = double.Parse(form[$"ProductList[{totalIndex}].VatPercent"] ?? "12");
                            if (piecePrice > 0 && amount > 0)
                            {
                                var baseTotal = piecePrice * amount;
                                var totalWithVat = baseTotal + (baseTotal * vatPercent / 100);
                                totalAmount += (decimal)totalWithVat;
                            }
                        }
                        totalIndex++;
                    }

                    // Yangilash contract amount
                    contractToUpdate.Amount = (int)totalAmount;
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
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                    .Include(c => c.CUSTOMER)
                    .Include(c => c.CURRENCy)
                    .FirstOrDefault(c => c.ID == id);

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
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS.Find(id);
                if (contract != null)
                {
                    contract.IsDeleted = true;
                    db.Entry(contract).State = EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{contract.ContractNo} - SContractni o'chirdi");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: DeleteProduct
        public ActionResult DeleteProduct(int id)
        {
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
                        totalAmount += (decimal)(product.PiecePrice * product.Amount);
                    }

                    var contract = db.S_CONTRACTS.Find(contractID);
                    if (contract != null)
                    {
                        contract.Amount = (int)totalAmount;
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
