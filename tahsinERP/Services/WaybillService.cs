using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Yo'l varaqasi biznes qoidalari (LOG-M01: avval WaybillController ichida edi):
    /// tarifni tekshirish va qo'llash, raqam takrorini tekshirish, view model ↔ entity, forma ro'yxatlari.
    /// </summary>
    public static class WaybillService
    {
        public const string WaybillNoPrefix = "TB_";

        /// <summary>
        /// Tanlangan tarifni tekshiradi va (reapply bo'lsa) stavka/asos/transport turini undan oladi.
        /// Xatolar <paramref name="modelState"/>'ga yoziladi.
        /// </summary>
        public static F_CONTRACT_TARIFFS ApplyTariff(DBTHSNEntities db, WaybillCreateViewModel viewModel,
            ModelStateDictionary modelState, bool requireTariff, bool reapply)
        {
            if (!viewModel.TariffID.HasValue)
            {
                if (requireTariff)
                    modelState.AddModelError("TariffID", L.T("Log_TariffRequired"));
                return null;
            }

            if (!reapply)
            {
                // Tarif o'zgarmagan: saqlangan stavka qoladi; MinimumCharge uchun tarif o'qiladi (faol bo'lmasa ham)
                return db.F_CONTRACT_TARIFFS.AsNoTracking().FirstOrDefault(t => t.ID == viewModel.TariffID.Value);
            }

            var selectedTariff = db.F_CONTRACT_TARIFFS.AsNoTracking()
                .FirstOrDefault(t => t.ID == viewModel.TariffID.Value && t.IsActive);
            if (selectedTariff == null)
            {
                modelState.AddModelError("TariffID", L.T("Log_TariffNotFoundOrInactive"));
                return null;
            }
            if (viewModel.ContractID.HasValue && selectedTariff.ContractID != viewModel.ContractID.Value)
            {
                modelState.AddModelError("TariffID", L.T("Log_TariffNotInContract"));
                return null;
            }

            viewModel.TransportTypeID = selectedTariff.TransportTypeID;
            viewModel.FreightBasis = selectedTariff.FreightBasis;
            viewModel.Rate = selectedTariff.Rate;
            if (!viewModel.CurrencyID.HasValue)
            {
                viewModel.CurrencyID = selectedTariff.CurrencyID;
            }
            return selectedTariff;
        }

        /// <summary>Navlun summasi qo'lda kiritilmagan bo'lsa — tarif bo'yicha (MinimumCharge bilan, LOG-B06).</summary>
        public static void FillAmount(WaybillCreateViewModel viewModel, F_CONTRACT_TARIFFS tariff)
        {
            if (!viewModel.Amount.HasValue)
            {
                viewModel.Amount = FreightCalculator.Calculate(viewModel.FreightBasis, viewModel.Rate,
                    viewModel.GrWeight, viewModel.DistanceKm, tariff?.MinimumCharge);
            }
        }

        /// <summary>LOG-B07: qo'lda kiritilgan raqam takrorlanmasin (Excel import'dagi tekshiruv Create/Edit'da ham).</summary>
        public static void ValidateWaybillNo(DBTHSNEntities db, WaybillCreateViewModel viewModel, ModelStateDictionary modelState)
        {
            if (string.IsNullOrWhiteSpace(viewModel.WaybillNo))
            {
                if (viewModel.ID.HasValue)
                    modelState.AddModelError("WaybillNo", L.T("Log_WaybillNoRequired"));
                return;
            }

            viewModel.WaybillNo = viewModel.WaybillNo.Trim();
            if (WaybillNoExists(db, viewModel.WaybillNo, viewModel.ID))
            {
                modelState.AddModelError("WaybillNo", DuplicateNoMessage);
            }
        }

        public static string DuplicateNoMessage => L.T("Log_WaybillNoDuplicate");

        public static bool WaybillNoExists(DBTHSNEntities db, string waybillNo, int? exceptId)
        {
            return db.F_WAYBILLS.Any(w => w.WaybillNo == waybillNo && w.IsDeleted != true &&
                                          (!exceptId.HasValue || w.ID != exceptId.Value));
        }

        public static int? GetContractForwarder(DBTHSNEntities db, int? contractId)
        {
            return contractId.HasValue
                ? db.F_CONTRACTS.Where(c => c.ID == contractId.Value).Select(c => (int?)c.ForwarderID).FirstOrDefault()
                : null;
        }

        /// <summary>
        /// LOG-B05: yo'l varaqasida saqlangan tarif (F_WAYBILLS.TariffID, Logistics_2026_10_B05_WaybillTariffID.sql).
        /// Ustun bo'sh bo'lgan eski yozuvlar uchun — shartnoma + transport turi bo'yicha eng so'nggi faol tarif.
        /// </summary>
        public static int? ResolveTariffId(DBTHSNEntities db, F_WAYBILLS waybill)
        {
            if (waybill.TariffID.HasValue)
                return waybill.TariffID;

            return db.F_CONTRACT_TARIFFS
                .Where(t => t.ContractID == waybill.ContractID && t.TransportTypeID == waybill.TransportTypeID && t.IsActive)
                .OrderByDescending(t => t.CreatedDT)
                .Select(t => (int?)t.ID)
                .FirstOrDefault();
        }

        /// <summary>Entity → view model (Edit sahifasi).</summary>
        public static WaybillCreateViewModel ToViewModel(F_WAYBILLS waybill, int? tariffId)
        {
            return new WaybillCreateViewModel
            {
                ID = waybill.ID,
                WaybillNo = waybill.WaybillNo,
                IssueDate = waybill.IssueDate,
                Direction = waybill.Direction,
                ForwarderID = waybill.ForwarderID,
                ContractID = waybill.ContractID,
                TransportTypeID = waybill.TransportTypeID,
                TariffID = tariffId,
                OriginalTariffID = tariffId,
                FromLocation = waybill.FromLocation,
                ToLocation = waybill.ToLocation,
                DistanceKm = waybill.DistanceKm,
                GrWeight = waybill.GrWeight,
                NetWeight = waybill.NetWeight,
                CBM = waybill.CBM,
                PalletsCount = waybill.PalletsCount,
                FreightBasis = waybill.FreightBasis,
                Rate = waybill.Rate,
                Amount = waybill.Amount,
                CurrencyID = waybill.CurrencyID,
                PayerType = waybill.PayerType,
                PayerID = waybill.PayerID,
                InvoiceID = waybill.InvoiceID,
                SalesInvoiceID = waybill.SalesInvoiceID,
                SalesOrderID = waybill.SalesOrderID,
                PurchaseOrderID = waybill.PurchaseOrderID,
                TTNKID = waybill.TTNKID,
                TruckNo = waybill.TruckNo,
                TrailerNo = waybill.TrailerNo,
                TruckModel = waybill.TruckModel,
                TrailerModel = waybill.TrailerModel,
                DriverName = waybill.DriverName,
                DriverPINFL = waybill.DriverPINFL,
                PlannedDeliveryDate = waybill.PlannedDeliveryDate,
                ActualDeliveryDate = waybill.ActualDeliveryDate,
                Description = waybill.Description,
                ManualCorrectionReason = waybill.ManualCorrectionReason
            };
        }

        /// <summary>View model → entity (avval Create va Edit'da 37 qatorli nusxa edi).</summary>
        public static void ApplyToEntity(WaybillCreateViewModel viewModel, F_WAYBILLS waybill, int? contractForwarder)
        {
            waybill.WaybillNo = viewModel.WaybillNo;
            waybill.IssueDate = viewModel.IssueDate;
            waybill.Direction = viewModel.Direction;
            waybill.ForwarderID = viewModel.ForwarderID ?? contractForwarder;
            waybill.ContractID = viewModel.ContractID;
            waybill.TransportTypeID = viewModel.TransportTypeID;
            waybill.TariffID = viewModel.TariffID;
            waybill.FromLocation = viewModel.FromLocation;
            waybill.ToLocation = viewModel.ToLocation;
            waybill.DistanceKm = viewModel.DistanceKm;
            waybill.GrWeight = viewModel.GrWeight;
            waybill.NetWeight = viewModel.NetWeight;
            waybill.CBM = viewModel.CBM;
            waybill.PalletsCount = viewModel.PalletsCount;
            waybill.FreightBasis = viewModel.FreightBasis;
            waybill.Rate = viewModel.Rate;
            waybill.Amount = viewModel.Amount;
            waybill.CurrencyID = viewModel.CurrencyID;
            waybill.PayerType = viewModel.PayerType;
            waybill.PayerID = viewModel.PayerID;
            waybill.InvoiceID = viewModel.InvoiceID;
            waybill.SalesInvoiceID = viewModel.SalesInvoiceID;
            waybill.SalesOrderID = viewModel.SalesOrderID;
            waybill.PurchaseOrderID = viewModel.PurchaseOrderID;
            waybill.TTNKID = viewModel.TTNKID;
            waybill.TruckNo = viewModel.TruckNo;
            waybill.TrailerNo = viewModel.TrailerNo;
            waybill.TruckModel = viewModel.TruckModel;
            waybill.TrailerModel = viewModel.TrailerModel;
            waybill.DriverName = viewModel.DriverName;
            waybill.DriverPINFL = viewModel.DriverPINFL;
            waybill.PlannedDeliveryDate = viewModel.PlannedDeliveryDate;
            waybill.ActualDeliveryDate = viewModel.ActualDeliveryDate;
            waybill.Description = viewModel.Description;
            waybill.ManualCorrectionReason = viewModel.ManualCorrectionReason;
        }

        /// <summary>
        /// Forma dropdown'lari. LOG-P01: invoys/buyurtma/TTNK ro'yxatlari to'liq yuklanmaydi — faqat
        /// tanlangan qiymat (agar bo'lsa) oldindan chiqariladi, qolgani WaybillLookup/SearchDocuments (select2 AJAX) orqali.
        /// Yuk tashuvchi, valyuta, shartnoma va transport turlari — ReferenceDataCache / LogisticsLookups.
        /// </summary>
        public static WaybillCreateViewModel BuildViewModel(DBTHSNEntities db, WaybillCreateViewModel model = null)
        {
            var viewModel = model ?? new WaybillCreateViewModel();

            viewModel.Forwarders = ToItems(ReferenceDataCache.Get(ReferenceDataCache.Forwarders), viewModel.ForwarderID);
            viewModel.Currencies = ToItems(ReferenceDataCache.Get(ReferenceDataCache.Currencies), viewModel.CurrencyID);
            viewModel.Contracts = ToItems(LogisticsLookups.Contracts(db, viewModel.ContractID), viewModel.ContractID);
            viewModel.TransportTypes = ToItems(LogisticsLookups.TransportTypes(db, viewModel.TransportTypeID), viewModel.TransportTypeID);

            viewModel.PurchaseInvoices = SelectedOnly(viewModel.InvoiceID,
                id => db.P_INVOICES.Where(i => i.ID == id).Select(i => i.InvoiceNo).FirstOrDefault());
            viewModel.SalesInvoices = SelectedOnly(viewModel.SalesInvoiceID,
                id => db.SALES_INVOICES.Where(i => i.ID == id).Select(i => i.InvoiceNo).FirstOrDefault());
            viewModel.SalesOrders = SelectedOnly(viewModel.SalesOrderID,
                id => db.S_ORDERS.Where(o => o.ID == id).Select(o => o.OrderNo).FirstOrDefault());
            viewModel.PurchaseOrders = SelectedOnly(viewModel.PurchaseOrderID,
                id => db.P_ORDERS.Where(o => o.ID == id).Select(o => o.OrderNo).FirstOrDefault());
            viewModel.TTNKs = SelectedOnly(viewModel.TTNKID,
                id => db.SALES_TTNK_HEADERS.Where(t => t.ID == id).Select(t => t.TTNKNumber).FirstOrDefault());

            viewModel.Tariffs = new List<SelectListItem>();
            if (viewModel.ContractID.HasValue)
            {
                var tariffs = db.F_CONTRACT_TARIFFS.AsNoTracking()
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Where(t => t.ContractID == viewModel.ContractID.Value &&
                                (t.IsActive || (viewModel.TariffID.HasValue && t.ID == viewModel.TariffID.Value)))
                    .OrderByDescending(t => t.CreatedDT)
                    .ToList();

                viewModel.Tariffs = tariffs.Select(t => new SelectListItem
                {
                    Value = t.ID.ToString(),
                    Text = FormatTariffDisplay(t),
                    Selected = viewModel.TariffID.HasValue && t.ID == viewModel.TariffID.Value
                }).ToList();
            }

            return viewModel;
        }

        public static string FormatTariffDisplay(F_CONTRACT_TARIFFS tariff)
        {
            var transport = tariff.F_TRANSPORT_TYPES?.TransportType ?? "";
            var capacity = tariff.F_TRANSPORT_TYPES?.CapableOfLifting;
            var capacityText = capacity.HasValue ? $"{capacity.Value:0.##}t" : "";
            var transportToken = transport.Replace(" ", "_");
            return $"{tariff.ID}_{transportToken}_{capacityText}";
        }

        public static string CargoTypeText(long? salesInvoiceId, int? salesOrderId, long? ttnkId, int? invoiceId, int? purchaseOrderId)
        {
            if (salesInvoiceId.HasValue || salesOrderId.HasValue || ttnkId.HasValue)
                return L.T("Log_Cargo_Finished");
            if (invoiceId.HasValue || purchaseOrderId.HasValue)
                return L.T("Log_Cargo_Material");
            return L.T("Log_Cargo_Other");
        }

        public static string LinkedDocText(long? salesInvoiceId, int? salesOrderId, long? ttnkId, int? invoiceId, int? purchaseOrderId)
        {
            if (salesInvoiceId.HasValue) return L.T("Log_Doc_SalesInvoice", salesInvoiceId);
            if (invoiceId.HasValue) return L.T("Log_Doc_PurchaseInvoice", invoiceId);
            if (ttnkId.HasValue) return L.T("Log_Doc_Ttnk", ttnkId);
            if (salesOrderId.HasValue) return L.T("Log_Doc_SalesOrder", salesOrderId);
            if (purchaseOrderId.HasValue) return L.T("Log_Doc_PurchaseOrder", purchaseOrderId);
            return null;
        }

        private static List<SelectListItem> ToItems(IEnumerable<RefItem> items, int? selected)
        {
            return items.Select(x => new SelectListItem
            {
                Value = x.ID.ToString(),
                Text = x.Name,
                Selected = selected.HasValue && x.ID == selected.Value
            }).ToList();
        }

        private static List<SelectListItem> SelectedOnly<TId>(TId? id, Func<TId, string> text) where TId : struct
        {
            var list = new List<SelectListItem>();
            if (id.HasValue)
            {
                list.Add(new SelectListItem { Value = id.Value.ToString(), Text = text(id.Value) ?? ("#" + id.Value), Selected = true });
            }
            return list;
        }
    }
}
