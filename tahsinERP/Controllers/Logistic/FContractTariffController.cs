using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>Ruxsat: [Permission] (GET — ko'rish, POST/Create/Edit/Delete — o'zgartirish; AJAX endpointlar ham, LOG-S02).</summary>
    // LOG-S03: POST entity binding faqat ruxsat etilgan maydonlar bilan ([Bind(Include)]) — over-posting yo'q
    [Permission]
    public class FContractTariffController : BaseController
    {
        public async Task<ActionResult> Index(int? contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.F_CONTRACT_TARIFFS
                    .Include(t => t.F_CONTRACTS)
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.F_CONTRACTS.IsDeleted == false);

                if (contractId.HasValue)
                {
                    query = query.Where(t => t.ContractID == contractId.Value);
                }

                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", contractId);

                var list = await query.OrderByDescending(t => t.CreatedDT).ToListAsync();
                var viewList = list.Select(t => new ViewModels.ContractTariffIndexItemViewModel
                {
                    ID = t.ID,
                    ContractNo = t.F_CONTRACTS?.ContractNo,
                    TransportType = t.F_TRANSPORT_TYPES?.TransportType,
                    FromLocation = string.IsNullOrEmpty(t.FromCity) ? t.FromRegion : t.FromCity,
                    ToLocation = string.IsNullOrEmpty(t.ToCity) ? t.ToRegion : t.ToCity,
                    FreightBasis = t.FreightBasis,
                    Rate = t.Rate,
                    Currency = t.CURRENCy?.Shortname,
                    IsActive = t.IsActive
                }).ToList();

                return View(viewList);
            }
        }

        public async Task<ActionResult> Create(int? contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", contractId);
                ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted == false || t.IsDeleted == null).ToListAsync(), "ID", "TransportType");
                ViewBag.Currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname");
                return View(new F_CONTRACT_TARIFFS { ContractID = contractId ?? 0, IsActive = true, CreatedDT = DateTime.Now });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "ContractID,FromRegion,FromCity,ToRegion,ToCity,TransportTypeID,FreightBasis,Rate,MinimumCharge,CurrencyID,IsActive,Description")] F_CONTRACT_TARIFFS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    model.CreatedDT = DateTime.Now;
                    db.F_CONTRACT_TARIFFS.Add(model);
                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", L.T("Log_SaveError"));
                        LogHelper.LogToDatabase(User.Identity.Name, "FContractTariffController", "Create", $"Xatolik: {ex.Message}");
                        ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", model.ContractID);
                        ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted == false || t.IsDeleted == null).ToListAsync(), "ID", "TransportType", model.TransportTypeID);
                        ViewBag.Currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname", model.CurrencyID);
                        return View(model);
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "FContractTariffController", "Create",
                        $"Shartnoma tarifini yaratdi: {model.ContractID}");

                    return RedirectToAction("Index", new { contractId = model.ContractID });
                }

                ModelState.AddModelError("", L.T("Log_FillRequired"));
                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", model.ContractID);
                ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted == false || t.IsDeleted == null).ToListAsync(), "ID", "TransportType", model.TransportTypeID);
                ViewBag.Currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname", model.CurrencyID);
                return View(model);
            }
        }

        public async Task<ActionResult> Edit(int? id)
        {
            if (!id.HasValue)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariff = await db.F_CONTRACT_TARIFFS.FindAsync(id.Value);
                if (tariff == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", tariff.ContractID);
                ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted == false || t.IsDeleted == null).ToListAsync(), "ID", "TransportType", tariff.TransportTypeID);
                ViewBag.Currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname", tariff.CurrencyID);
                return View(tariff);
            }
        }

        public async Task<ActionResult> Details(int? id)
        {
            if (!id.HasValue)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariff = await db.F_CONTRACT_TARIFFS
                    .Include(t => t.F_CONTRACTS)
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Include(t => t.CURRENCy)
                    .FirstOrDefaultAsync(t => t.ID == id.Value);

                if (tariff == null)
                {
                    return HttpNotFound();
                }

                return View(tariff);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,ContractID,FromRegion,FromCity,ToRegion,ToCity,TransportTypeID,FreightBasis,Rate,MinimumCharge,CurrencyID,IsActive,Description")] F_CONTRACT_TARIFFS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var tariffToUpdate = await db.F_CONTRACT_TARIFFS.FindAsync(model.ID);
                    if (tariffToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    tariffToUpdate.ContractID = model.ContractID;
                    tariffToUpdate.FromRegion = model.FromRegion;
                    tariffToUpdate.FromCity = model.FromCity;
                    tariffToUpdate.ToRegion = model.ToRegion;
                    tariffToUpdate.ToCity = model.ToCity;
                    tariffToUpdate.TransportTypeID = model.TransportTypeID;
                    tariffToUpdate.FreightBasis = model.FreightBasis;
                    tariffToUpdate.Rate = model.Rate;
                    tariffToUpdate.MinimumCharge = model.MinimumCharge;
                    tariffToUpdate.CurrencyID = model.CurrencyID;
                    tariffToUpdate.IsActive = model.IsActive;
                    tariffToUpdate.Description = model.Description;

                    try
                    {
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "FContractTariffController", "Edit",
                            $"Shartnoma tarifini tahrirladi: {model.ID}");

                        return RedirectToAction("Index", new { contractId = model.ContractID });
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", L.T("Log_SaveFailedRetry"));
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", L.T("Log_SaveError"));
                        LogHelper.LogToDatabase(User.Identity.Name, "FContractTariffController", "Edit", $"Xatolik: {ex.Message}");
                    }
                }
                else
                {
                    ModelState.AddModelError("", L.T("Log_FillRequired"));
                }

                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", model.ContractID);
                ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted == false || t.IsDeleted == null).ToListAsync(), "ID", "TransportType", model.TransportTypeID);
                ViewBag.Currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname", model.CurrencyID);
                return View(model);
            }
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (!id.HasValue)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariff = await db.F_CONTRACT_TARIFFS
                    .Include(t => t.F_CONTRACTS)
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Include(t => t.CURRENCy)
                    .FirstOrDefaultAsync(t => t.ID == id.Value);

                if (tariff == null)
                {
                    return HttpNotFound();
                }

                return View(tariff);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<ActionResult> DeleteConfirmed(int? id)
        {
            if (!id.HasValue)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariff = await db.F_CONTRACT_TARIFFS.FindAsync(id.Value);
                if (tariff == null)
                {
                    return HttpNotFound();
                }

                db.F_CONTRACT_TARIFFS.Remove(tariff);
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Log_DeleteError"));
                    LogHelper.LogToDatabase(User.Identity.Name, "FContractTariffController", "Delete", $"Xatolik: {ex.Message}");
                    return View(tariff);
                }

                LogHelper.LogToDatabase(User.Identity.Name, "FContractTariffController", "Delete",
                    $"Shartnoma tarifini o'chirdi: {tariff.ID}");

                return RedirectToAction("Index", new { contractId = tariff.ContractID });
            }
        }
    }
}
