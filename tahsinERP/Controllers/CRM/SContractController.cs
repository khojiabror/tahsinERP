using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using System.Web.Security;

namespace tahsinERP.Controllers
{
    public class SContractController : Controller
    {
        // GET: SContract | Index
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

                // ViewBag for filters
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
        // __________



        // Create
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SContractViewModel SContractViewModel = new SContractViewModel();
                var customers = db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList();
                ViewBag.CustomerID = new SelectList(customers, "ID", "Name");
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(SContractViewModel);
            }
        }

        [HttpPost]
        public ActionResult Create(SContractViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (model == null)
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name");
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    ModelState.AddModelError("", "Shartnoma bo'sh bo'lishi mumkin emas");
                    return View(new SContractViewModel());
                }

                if (model.ProductList == null || !model.ProductList.Any())
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name");
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    ModelState.AddModelError("", "Kamida bitta maxsulot qo'shish kerak");
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name");
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    return View(model);
                }

                try
                {
                    // Add New S_CONTRACT
                    S_CONTRACTS newContract = new S_CONTRACTS
                    {
                        ContractNo = model.ContractNo,
                        IssuedDate = DateTime.Now,
                        CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                        CustomerID = model.CustomerID,
                        CurrencyID = model.CurrencyID,
                        Amount = (int)model.Amount,
                        Incoterms = model.Incoterms,
                        PaymentTerms = model.PaymentTerms,
                        DueDate = model.DueDate,
                        IsDeleted = false,
                    };

                    db.S_CONTRACTS.Add(newContract);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{newContract.ContractNo} - SContractni yaratdi");

                    // Yangi Contract ning ID sini olish
                    int newContractID = newContract.ID;

                    // Yangi Product lar ni saqlash va jami summani hisoblash
                    decimal totalAmount = 0;
                    foreach (var item in model.ProductList)
                    {
                        if (item == null || item.ProductID == 0)
                        {
                            continue;
                        }

                        S_CONTRACT_PRODUCTS newProduct = new S_CONTRACT_PRODUCTS
                        {
                            ContractID = newContractID,
                            ProductID = item.ProductID,
                            PiecePrice = (int)item.PiecePrice,
                            UnitID = item.UnitID,
                            Amount = item.Amount
                        };

                        db.S_CONTRACT_PRODUCTS.Add(newProduct);

                        // Har bir product uchun price * amount ni qo'shamiz
                        totalAmount += (decimal)(item.PiecePrice * item.Amount);
                    }

                    // Barcha productlar yaratilgandan so'ng, jami summani S_CONTRACTS ga yozamiz
                    newContract.Amount = (int)totalAmount;

                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ViewBag.CustomerID = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name");
                    ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(), "ID", "PNo");
                    ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"Ichki xatolik: {ex.InnerException.Message}");
                    }
                    return View(model);
                }
            }
        }
        // __________



        // Details
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

                // Get linked orders
                var linkedOrders = contract.S_ORDERS.Where(o => o.IsDeleted == false).ToList();
                ViewBag.LinkedOrders = linkedOrders;
                ViewBag.OrdersCount = linkedOrders.Count;

                // Get invoices count
                var invoicesCount = db.SALES_INVOICES.Where(i => i.ContractID == id).Count();
                ViewBag.InvoicesCount = invoicesCount;

                // Calculate totals: contracted vs ordered vs invoiced
                var contractedTotal = productList.Sum(p => p.Amount);
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
        // __________



        // Main Edit
        [HttpGet]
        public ActionResult Edit(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS.Include(x => x.S_CONTRACT_PRODUCTS).Where(x => x.ID == id && x.IsDeleted == false).FirstOrDefault();
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
                        PiecePrice = (int)p.PiecePrice,
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
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SContractViewModel model)
        {
            if (model == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Model bo'sh bo'lishi mumkin emas");
            }

            if (model.ProductList == null || !model.ProductList.Any())
            {
                ModelState.AddModelError("Products", "Kamida bitta mahsulot kerak.");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var contractToUpdate = db.S_CONTRACTS.Find(model.ID);
                    if (contractToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    contractToUpdate.ContractNo = model.ContractNo;
                    contractToUpdate.CustomerID = model.CustomerID;
                    contractToUpdate.IssuedDate = model.IssuedDate;
                    contractToUpdate.DueDate = model.DueDate;
                    contractToUpdate.Incoterms = model.Incoterms;
                    contractToUpdate.PaymentTerms = model.PaymentTerms;
                    contractToUpdate.CurrencyID = model.CurrencyID;
                    contractToUpdate.Amount = (int)model.Amount;


                    db.Entry(contractToUpdate).State = EntityState.Modified;
                    try
                    {
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{contractToUpdate.ContractNo} - SContractni tahrirladi");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", "Kontrakt o'zgarishlarini saqlab bo'lmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                    }


                    // Update ProductList va jami summani hisoblash
                    decimal totalAmount = 0;
                    foreach (var product in model.ProductList)
                    {
                        var existingProduct = db.S_CONTRACT_PRODUCTS.Find(product.ID);
                        if (existingProduct != null)
                        {
                            existingProduct.PiecePrice = product.PiecePrice;
                            existingProduct.UnitID = product.UnitID;
                            existingProduct.Amount = product.Amount;

                            db.Entry(existingProduct).State = EntityState.Modified;

                            // Har bir product uchun price * amount ni qo'shamiz
                            totalAmount += (decimal)(product.PiecePrice * product.Amount);

                            LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{existingProduct.PRODUCT.PNo} - SContractProductni tahrirladi");
                        }
                    }

                    // Barcha productlar yangilangandan so'ng, jami summani S_CONTRACTS ga yozamiz
                    contractToUpdate.Amount = (int)totalAmount;
                    db.Entry(contractToUpdate).State = EntityState.Modified;

                    try
                    {
                        db.SaveChanges();
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", "Maxsulot o'zgarishlarini saqlab bo'lmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratsiyasiga muroaat qiling.");
                    }
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    var userEmail = User.Identity.Name;
                    LogHelper.LogToDatabase(userEmail, "SContractController", "Edit[Post]");
                    return RedirectToAction("Index");
                }
            }

            ViewBag.ProductList = model.ProductList;

            return View(model);
        }
        // __________



        // Delete Main
        [HttpGet]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS.Find(id);
                if (contract == null)
                {
                    return HttpNotFound();
                }
                else
                {
                    ViewBag.ProductList = db.S_CONTRACT_PRODUCTS
                                            .Include(pl => pl.PRODUCT)
                                            .Include(pl => pl.UNIT)
                                            .Where(pl => pl.ContractID == contract.ID)
                                            .ToList();
                }

                db.Entry(contract).Reference(c => c.CUSTOMER).Load();
                db.Entry(contract).Reference(c => c.CURRENCy).Load();
                return View(contract);
            }
        }

        [HttpPost]
        public ActionResult Delete(int? id, FormCollection gfs)
        {
            if (!ModelState.IsValid || id == null)
            {
                return View();
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                S_CONTRACTS contractToDelete = db.S_CONTRACTS.Find(id);
                if (contractToDelete != null)
                {
                    contractToDelete.IsDeleted = true;

                    try
                    {
                        db.Entry(contractToDelete).State = EntityState.Modified;
                        var contractProducts = db.S_CONTRACT_PRODUCTS.Where(sp => sp.ContractID == contractToDelete.ID).ToList();

                        foreach (var contractProduct in contractProducts)
                        {
                            db.S_CONTRACT_PRODUCTS.Remove(contractProduct);

                            LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{contractProduct.PRODUCT.PNo} - SContractni o'chirdi");
                        }

                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Bunday shartnoma topilmadi.");
                }
            }

            return View();
        }
        public ActionResult DeleteProduct(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                S_CONTRACT_PRODUCTS contractProductToDelete = db.S_CONTRACT_PRODUCTS.Find(id);

                if (ModelState.IsValid)
                {
                    if (contractProductToDelete != null)
                    {
                        try
                        {
                            db.S_CONTRACT_PRODUCTS.Remove(contractProductToDelete);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"{contractProductToDelete.PRODUCT.PNo} - SContractProductni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday shartnoma topilmadi.");
                    }
                }

                return View(contractProductToDelete);
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

                // Get all orders for this customer that are not linked to this contract
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

        // POST: Link Order to Contract
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

        // POST: Unlink Order from Contract
        [HttpPost]
        public ActionResult UnlinkOrder(int contractID, int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS.Find(orderID);
                if (order != null && order.ContractID == contractID)
                {
                    // ContractID is not nullable, so we set it to 0 to indicate no contract
                    // Note: This assumes 0 is not a valid contract ID
                    order.ContractID = 0;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SContractController", $"Buyurtma {order.OrderNo} ni shartnomadan ajratdi");
                }

                return RedirectToAction("ManageOrders", new { contractID = contractID });
            }
        }
    }
}