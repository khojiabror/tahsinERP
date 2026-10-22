using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Akt-sverki — bitta shartnoma yoki kontragentning barcha shartnomalari (contractId = -1) bo'yicha.
    /// </summary>
    public class ReconciliationController : Controller
    {
        public async Task<ActionResult> Supplier(int? id, int? contractId, DateTime? asOfDate)
        {
            try
            {
                if (User?.Identity?.Name == null || !RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier", "Details"))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }
                if (id == null)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

                asOfDate = asOfDate ?? DateTime.Today;

                using (var db = new DBTHSNEntities())
                {
                    var supplier = await db.SUPPLIERS.FindAsync(id.Value);
                    if (supplier == null || supplier.IsDeleted == true)
                        return HttpNotFound();

                    var companyName = (db.COMPANIES != null && db.COMPANIES.Any())
                        ? db.COMPANIES.FirstOrDefault()?.Name
                        : null;

                    var contractList = await db.P_CONTRACTS
                        .Where(c => c.SupplierID == id.Value && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate)
                        .Select(c => new ReconciliationContractOption { Id = c.ID, ContractNo = c.ContractNo })
                        .ToListAsync();

                    if (contractId == ReconciliationContractOption.AllContractsId)
                    {
                        var all = ReconciliationService.BuildSupplierReconciliation(db, id.Value, null, asOfDate.Value, companyName, supplier.Name ?? "", null);
                        all.ContractsForParty = contractList;
                        return View("AktSverki", all);
                    }
                    if (!contractId.HasValue || contractId.Value <= 0)
                    {
                        var empty = EmptyVm("Supplier", id.Value, supplier.Name ?? "", companyName, asOfDate.Value, contractList);
                        return View("AktSverki", empty);
                    }

                    var pcon = await db.P_CONTRACTS.FirstOrDefaultAsync(c => c.ID == contractId.Value && c.SupplierID == id.Value && c.IsDeleted != true);
                    if (pcon == null)
                        return HttpNotFound();

                    var vm = ReconciliationService.BuildSupplierReconciliation(db, id.Value, contractId.Value, asOfDate.Value, companyName, supplier.Name ?? "", pcon.ContractNo ?? "");
                    vm.ContractsForParty = contractList;
                    return View("AktSverki", vm);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User?.Identity?.Name ?? "System", "Reconciliation", "Supplier", $"Xatolik (Supplier {id}): {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public async Task<ActionResult> Customer(int? id, int? contractId, DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Customer", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            asOfDate = asOfDate ?? DateTime.Today;

            using (var db = new DBTHSNEntities())
            {
                var customer = db.CUSTOMERS.Find(id.Value);
                if (customer == null || customer.IsDeleted == true)
                    return HttpNotFound();

                var companyName = db.COMPANIES.Any() ? db.COMPANIES.FirstOrDefault()?.Name : null;

                var contractList = await db.S_CONTRACTS
                    .Where(c => c.CustomerID == id.Value && c.IsDeleted == false)
                    .OrderByDescending(c => c.IssuedDate)
                    .Select(c => new ReconciliationContractOption { Id = c.ID, ContractNo = c.ContractNo })
                    .ToListAsync();

                if (contractId == ReconciliationContractOption.AllContractsId)
                {
                    var all = ReconciliationService.BuildCustomerReconciliation(db, id.Value, null, asOfDate.Value, companyName, customer.Name ?? "", null);
                    all.ContractsForParty = contractList;
                    return View("AktSverki", all);
                }
                if (!contractId.HasValue || contractId.Value <= 0)
                {
                    var empty = EmptyVm("Customer", id.Value, customer.Name ?? "", companyName, asOfDate.Value, contractList);
                    return View("AktSverki", empty);
                }

                var scon = await db.S_CONTRACTS.FirstOrDefaultAsync(c => c.ID == contractId.Value && c.CustomerID == id.Value && c.IsDeleted == false);
                if (scon == null)
                    return HttpNotFound();

                var vm = ReconciliationService.BuildCustomerReconciliation(db, id.Value, contractId.Value, asOfDate.Value, companyName, customer.Name ?? "", scon.ContractNo ?? "");
                vm.ContractsForParty = contractList;
                return View("AktSverki", vm);
            }
        }

        public async Task<ActionResult> Forwarder(int? id, int? contractId, DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Forwarder", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            asOfDate = asOfDate ?? DateTime.Today;

            using (var db = new DBTHSNEntities())
            {
                var forwarder = await db.FORWARDERS.FindAsync(id.Value);
                if (forwarder == null || forwarder.IsDeleted == true)
                    return HttpNotFound();

                var companyName = db.COMPANIES.Any() ? db.COMPANIES.FirstOrDefault()?.Name : null;

                var contractList = await db.F_CONTRACTS
                    .Where(c => c.ForwarderID == id.Value && c.IsDeleted == false)
                    .OrderByDescending(c => c.IssueDate)
                    .Select(c => new ReconciliationContractOption { Id = c.ID, ContractNo = c.ContractNo })
                    .ToListAsync();

                if (contractId == ReconciliationContractOption.AllContractsId)
                {
                    var all = ReconciliationService.BuildForwarderReconciliation(db, id.Value, null, asOfDate.Value, companyName, forwarder.ForwarderName ?? "", null);
                    all.ContractsForParty = contractList;
                    return View("AktSverki", all);
                }
                if (!contractId.HasValue || contractId.Value <= 0)
                {
                    var empty = EmptyVm("Forwarder", id.Value, forwarder.ForwarderName ?? "", companyName, asOfDate.Value, contractList);
                    return View("AktSverki", empty);
                }

                var fcon = await db.F_CONTRACTS.FirstOrDefaultAsync(c => c.ID == contractId.Value && c.ForwarderID == id.Value && c.IsDeleted == false);
                if (fcon == null)
                    return HttpNotFound();

                var vm = ReconciliationService.BuildForwarderReconciliation(db, id.Value, contractId.Value, asOfDate.Value, companyName, forwarder.ForwarderName ?? "", fcon.ContractNo ?? "");
                vm.ContractsForParty = contractList;
                return View("AktSverki", vm);
            }
        }

        private static ReconciliationViewModel EmptyVm(string reconciliationType, int counterpartyId, string party2Name, string companyName, DateTime asOfDate, List<ReconciliationContractOption> contractList)
        {
            return new ReconciliationViewModel
            {
                ReconciliationType = reconciliationType,
                CounterpartyId = counterpartyId,
                ContractId = 0,
                ContractNo = null,
                Party1Name = companyName ?? L.T("Crm_RecOurCompany"),
                Party2Name = party2Name,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                AgreementInfo = L.T("Crm_RecAgreementSelectHint"),
                Rows = new List<ReconciliationRowViewModel>(),
                ContractsForParty = contractList ?? new List<ReconciliationContractOption>()
            };
        }

        [HttpGet]
        public async Task<ActionResult> EditAdjustment(string type, int? id, int? contractId)
        {
            if (User?.Identity?.Name == null || id == null || !contractId.HasValue || contractId.Value <= 0)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            var module = ReconciliationAdjustmentService.ModuleNameFromType(type ?? "");
            if (module == null)
                return HttpNotFound();

            if (!RoleHelper.IsChangePermitted(User.Identity.Name, module))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var cpType = ReconciliationAdjustmentService.TypeFromString(type);
            if (cpType == 0)
                return HttpNotFound();

            var year = DateTime.Now.Year;

            using (var db = new DBTHSNEntities())
            {
                var name = await GetCounterpartyNameAsync(db, cpType, id.Value);
                if (name == null)
                    return HttpNotFound();

                var contractNo = await ValidateAndGetContractNoAsync(db, cpType, id.Value, contractId.Value);
                if (contractNo == null)
                    return HttpNotFound();

                var existing = ReconciliationAdjustmentService.LoadForYear(db, cpType, id.Value, contractId.Value, year);
                var vm = new ReconciliationAdjustmentEditViewModel
                {
                    ReconciliationType = type,
                    CounterpartyId = id.Value,
                    ContractId = contractId.Value,
                    ContractNo = contractNo,
                    CounterpartyName = name,
                    EntryYear = year,
                    CutoffDate = existing?.CutoffDate ?? DateTime.Today,
                    Party1Debit = existing?.Party1Debit ?? 0,
                    Party1Credit = existing?.Party1Credit ?? 0,
                    Note = existing?.Note,
                    AdjustmentAlreadyExistsForYear = existing != null
                };
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditAdjustment(ReconciliationAdjustmentEditViewModel model)
        {
            if (User?.Identity?.Name == null || model == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            var module = ReconciliationAdjustmentService.ModuleNameFromType(model.ReconciliationType ?? "");
            if (module == null)
                return HttpNotFound();

            if (!RoleHelper.IsChangePermitted(User.Identity.Name, module))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var cpType = ReconciliationAdjustmentService.TypeFromString(model.ReconciliationType);
            if (cpType == 0)
                return HttpNotFound();

            var year = DateTime.Now.Year;

            int userId;
            try
            {
                userId = UserHelper.GetUserId(User.Identity.Name);
            }
            catch
            {
                userId = 0;
            }

            if (userId <= 0)
                ModelState.AddModelError("", L.T("Crm_RecUserNotFound"));

            if (model.ContractId <= 0)
                ModelState.AddModelError("", L.T("Crm_RecContractRequired"));

            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        var name = await GetCounterpartyNameAsync(db, cpType, model.CounterpartyId);
                        if (name == null)
                            return HttpNotFound();

                        if (await ValidateAndGetContractNoAsync(db, cpType, model.CounterpartyId, model.ContractId) == null)
                            return HttpNotFound();

                        ReconciliationAdjustmentService.Save(db, cpType, model.CounterpartyId, model.ContractId, year, model.CutoffDate, model.Party1Debit, model.Party1Credit, model.Note, userId);
                    }

                    return RedirectToAction(model.ReconciliationType, "Reconciliation", new { id = model.CounterpartyId, contractId = model.ContractId });
                }
                catch (SqlException ex)
                {
                    ModelState.AddModelError("", L.T("Crm_RecDbSchemaError", ex.Message));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Crm_RecSaveError", ex.Message));
                }
            }

            using (var db = new DBTHSNEntities())
            {
                model.CounterpartyName = (await GetCounterpartyNameAsync(db, cpType, model.CounterpartyId)) ?? model.CounterpartyName;
                model.ContractNo = (await ValidateAndGetContractNoAsync(db, cpType, model.CounterpartyId, model.ContractId)) ?? model.ContractNo;
                var existing = ReconciliationAdjustmentService.LoadForYear(db, cpType, model.CounterpartyId, model.ContractId, year);
                model.AdjustmentAlreadyExistsForYear = existing != null;
            }

            return View(model);
        }

        private static async Task<string> GetCounterpartyNameAsync(DBTHSNEntities db, byte counterpartyType, int counterpartyId)
        {
            if (counterpartyType == ReconciliationAdjustmentService.CounterpartySupplier)
            {
                var s = await db.SUPPLIERS.FindAsync(counterpartyId);
                if (s == null || s.IsDeleted == true)
                    return null;
                return s.Name ?? "";
            }
            if (counterpartyType == ReconciliationAdjustmentService.CounterpartyCustomer)
            {
                var c = await db.CUSTOMERS.FindAsync(counterpartyId);
                if (c == null || c.IsDeleted == true)
                    return null;
                return c.Name ?? "";
            }
            if (counterpartyType == ReconciliationAdjustmentService.CounterpartyForwarder)
            {
                var f = await db.FORWARDERS.FindAsync(counterpartyId);
                if (f == null || f.IsDeleted == true)
                    return null;
                return f.ForwarderName ?? "";
            }
            return null;
        }

        private static async Task<string> ValidateAndGetContractNoAsync(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId)
        {
            if (counterpartyType == ReconciliationAdjustmentService.CounterpartySupplier)
            {
                var c = await db.P_CONTRACTS.FirstOrDefaultAsync(x => x.ID == contractId && x.SupplierID == counterpartyId && x.IsDeleted != true);
                return c?.ContractNo;
            }
            if (counterpartyType == ReconciliationAdjustmentService.CounterpartyCustomer)
            {
                var c = await db.S_CONTRACTS.FirstOrDefaultAsync(x => x.ID == contractId && x.CustomerID == counterpartyId && x.IsDeleted == false);
                return c?.ContractNo;
            }
            if (counterpartyType == ReconciliationAdjustmentService.CounterpartyForwarder)
            {
                var c = await db.F_CONTRACTS.FirstOrDefaultAsync(x => x.ID == contractId && x.ForwarderID == counterpartyId && x.IsDeleted == false);
                return c?.ContractNo;
            }
            return null;
        }
    }
}
