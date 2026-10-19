using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services
{
    /// <summary>Excel import natijasi.</summary>
    public sealed class WaybillImportResult
    {
        public string FatalError { get; set; }
        public int SuccessCount { get; set; }
        public int SkipCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    /// <summary>
    /// Yo'l varaqalarini Excel'dan import qilish va shablon (LOG-M01: avval WaybillController ichida edi).
    /// LOG-B06: har qator shartnoma tarifiga solishtiriladi — stavka/asos/valyuta berilmasa tarifdan olinadi,
    /// tarifdan farq qilsa yoki tarif topilmasa ManualCorrectionReason majburiy; summa MinimumCharge bilan hisoblanadi.
    /// </summary>
    public static class WaybillImportService
    {
        public static readonly string[] Headers =
        {
            "WaybillNo","IssueDate","Direction","ForwarderID","ForwarderName","ContractID","ContractNo",
            "TransportTypeID","TransportType","FromLocation","ToLocation","DistanceKm","GrWeight","NetWeight",
            "CBM","PalletsCount","FreightBasis","Rate","Amount","CurrencyID","Currency","PayerType","PayerID",
            "InvoiceID","SalesInvoiceID","SalesOrderID","PurchaseOrderID","TTNKID","TruckNo","TrailerNo",
            "TruckModel","TrailerModel","DriverName","DriverPINFL","PlannedDeliveryDate","ActualDeliveryDate",
            "Description","ManualCorrectionReason"
        };

        private static readonly HashSet<string> DateHeaders = new HashSet<string>
        {
            "IssueDate", "PlannedDeliveryDate", "ActualDeliveryDate"
        };

        private sealed class TariffRow
        {
            public int ID;
            public int ContractID;
            public int? TransportTypeID;
            public string FromRegion, FromCity, ToRegion, ToCity;
            public byte FreightBasis;
            public decimal Rate;
            public decimal? MinimumCharge;
            public int? CurrencyID;
            public DateTime CreatedDT;
        }

        /// <summary>
        /// LOG-B12(d): namuna qatori haqiqiy faol tarif (shartnoma, transport turi, asos, stavka) bilan —
        /// shablonni o'zgartirmasdan import qilsa ham o'tadi. LOG-B12(c): sana formati ustun nomi bo'yicha.
        /// </summary>
        public static byte[] BuildTemplate(DBTHSNEntities db)
        {
            var sample = new Dictionary<string, object>
            {
                { "WaybillNo", "WB-0001" },
                { "IssueDate", DateTime.Today },
                { "Direction", "Kiruvchi" },
                { "FromLocation", "Supplier A" },
                { "ToLocation", "Warehouse B" },
                { "DistanceKm", 25 },
                { "GrWeight", 12.5 }
            };

            var tariff = db.F_CONTRACT_TARIFFS.AsNoTracking()
                .Where(t => t.IsActive && !t.F_CONTRACTS.IsDeleted)
                .OrderByDescending(t => t.CreatedDT)
                .Select(t => new
                {
                    t.F_CONTRACTS.ContractNo,
                    TransportType = t.F_TRANSPORT_TYPES.TransportType,
                    t.FreightBasis,
                    t.Rate,
                    Currency = t.CURRENCy.Shortname
                })
                .FirstOrDefault();

            if (tariff != null)
            {
                sample["ContractNo"] = tariff.ContractNo;
                sample["TransportType"] = tariff.TransportType;
                sample["FreightBasis"] = LogisticsEnums.FreightBasisText(tariff.FreightBasis);
                sample["Rate"] = tariff.Rate;
                sample["Currency"] = tariff.Currency;
            }
            else
            {
                sample["ContractNo"] = db.F_CONTRACTS.Where(c => !c.IsDeleted).OrderByDescending(c => c.ID).Select(c => c.ContractNo).FirstOrDefault();
                sample["TransportType"] = db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted != true).OrderBy(t => t.ID).Select(t => t.TransportType).FirstOrDefault();
                sample["ManualCorrectionReason"] = "Tarif yo'q — qo'lda";
                sample["FreightBasis"] = LogisticsEnums.FreightBasisText(1);
                sample["Rate"] = 150000;
                sample["Currency"] = "UZS";
            }

            var columns = Headers
                .Select(h => ExcelExport.Col<Dictionary<string, object>>(h,
                    row => row.ContainsKey(h) ? row[h] : null,
                    DateHeaders.Contains(h) ? "yyyy-mm-dd" : null))
                .ToArray();

            return ExcelExport.Create("Waybills", new[] { sample }, columns);
        }

        public static async Task<WaybillImportResult> ImportAsync(DBTHSNEntities db, Stream stream)
        {
            var result = new WaybillImportResult();

            var forwarders = await db.FORWARDERS.AsNoTracking().Where(f => f.IsDeleted != true).Select(f => new { f.ID, f.ForwarderName }).ToListAsync();
            var contracts = await db.F_CONTRACTS.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.ID, c.ContractNo, c.ForwarderID }).ToListAsync();
            var transportTypes = await db.F_TRANSPORT_TYPES.AsNoTracking().Where(t => t.IsDeleted != true).Select(t => new { t.ID, t.TransportType }).ToListAsync();
            var tariffs = (await db.F_CONTRACT_TARIFFS.AsNoTracking().Where(t => t.IsActive)
                    .Select(t => new TariffRow
                    {
                        ID = t.ID, ContractID = t.ContractID, TransportTypeID = t.TransportTypeID,
                        FromRegion = t.FromRegion, FromCity = t.FromCity, ToRegion = t.ToRegion, ToCity = t.ToCity,
                        FreightBasis = t.FreightBasis, Rate = t.Rate, MinimumCharge = t.MinimumCharge,
                        CurrencyID = t.CurrencyID, CreatedDT = t.CreatedDT
                    }).ToListAsync())
                .ToLookup(t => t.ContractID);
            var currencies = ReferenceDataCache.Get(ReferenceDataCache.Currencies);
            var existingWaybillNos = new HashSet<string>(
                db.F_WAYBILLS.Where(w => w.IsDeleted != true).Select(w => w.WaybillNo),
                StringComparer.OrdinalIgnoreCase);
            var fileWaybillNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null || worksheet.LastRowNumber() == 0)
                {
                    result.FatalError = "Excel fayl bo'sh.";
                    return result;
                }

                var cells = new ExcelCellReader(worksheet);
                if (!cells.Has("WaybillNo") || !cells.Has("IssueDate"))
                {
                    result.FatalError = "Excel faylda majburiy ustunlar yo'q (WaybillNo, IssueDate).";
                    return result;
                }

                var errors = result.Errors;
                var lastRow = worksheet.LastRowNumber();
                for (int row = 2; row <= lastRow; row++)
                {
                    var waybillNo = cells.String(row, "WaybillNo");
                    if (string.IsNullOrWhiteSpace(waybillNo))
                    {
                        result.SkipCount++;
                        continue;
                    }

                    if (fileWaybillNos.Contains(waybillNo))
                    {
                        errors.Add($"Qator {row}: Bu waybill raqami Excel faylida takrorlangan ({waybillNo}).");
                        continue;
                    }
                    fileWaybillNos.Add(waybillNo);

                    if (existingWaybillNos.Contains(waybillNo))
                    {
                        errors.Add($"Qator {row}: Bu waybill raqami avval kiritilgan ({waybillNo}).");
                        continue;
                    }

                    var issueDate = cells.Date(row, "IssueDate");
                    if (!issueDate.HasValue)
                    {
                        errors.Add($"Qator {row}: sana noto'g'ri formatda.");
                        continue;
                    }

                    var contractId = cells.Int(row, "ContractID");
                    var contractNo = cells.String(row, "ContractNo");
                    var contract = contractId.HasValue
                        ? contracts.FirstOrDefault(c => c.ID == contractId.Value)
                        : (!string.IsNullOrWhiteSpace(contractNo) ? contracts.FirstOrDefault(c => c.ContractNo == contractNo) : null);

                    if (contract == null)
                    {
                        errors.Add($"Qator {row}: Shartnoma topilmadi (ContractID/ContractNo).");
                        continue;
                    }

                    var forwarderId = cells.Int(row, "ForwarderID");
                    var forwarderName = cells.String(row, "ForwarderName");
                    var forwarder = forwarderId.HasValue
                        ? forwarders.FirstOrDefault(f => f.ID == forwarderId.Value)
                        : (!string.IsNullOrWhiteSpace(forwarderName) ? forwarders.FirstOrDefault(f => f.ForwarderName == forwarderName) : null);

                    var transportTypeId = cells.Int(row, "TransportTypeID");
                    var transportTypeName = cells.String(row, "TransportType");
                    var transportType = transportTypeId.HasValue
                        ? transportTypes.FirstOrDefault(t => t.ID == transportTypeId.Value)
                        : (!string.IsNullOrWhiteSpace(transportTypeName) ? transportTypes.FirstOrDefault(t => t.TransportType == transportTypeName) : null);

                    if (transportType == null)
                    {
                        errors.Add($"Qator {row}: Transport turi topilmadi (TransportTypeID/TransportType).");
                        continue;
                    }

                    var currencyId = cells.Int(row, "CurrencyID");
                    var currencyCode = cells.String(row, "Currency");
                    var currency = currencyId.HasValue
                        ? currencies.FirstOrDefault(c => c.ID == currencyId.Value)
                        : (!string.IsNullOrWhiteSpace(currencyCode) ? currencies.FirstOrDefault(c => string.Equals(c.Name, currencyCode, StringComparison.OrdinalIgnoreCase)) : null);

                    var waybill = new F_WAYBILLS
                    {
                        WaybillNo = waybillNo,
                        IssueDate = issueDate.Value,
                        Direction = cells.Byte(row, "Direction", LogisticsEnums.ParseDirection),
                        ForwarderID = forwarder?.ID ?? contract.ForwarderID,
                        ContractID = contract.ID,
                        TransportTypeID = transportType.ID,
                        FromLocation = cells.String(row, "FromLocation"),
                        ToLocation = cells.String(row, "ToLocation"),
                        DistanceKm = cells.Decimal(row, "DistanceKm"),
                        GrWeight = cells.Decimal(row, "GrWeight"),
                        NetWeight = cells.Decimal(row, "NetWeight"),
                        CBM = cells.Decimal(row, "CBM"),
                        PalletsCount = cells.Int(row, "PalletsCount"),
                        FreightBasis = cells.Byte(row, "FreightBasis", LogisticsEnums.ParseFreightBasis),
                        Rate = cells.Decimal(row, "Rate"),
                        Amount = cells.Decimal(row, "Amount"),
                        CurrencyID = currency?.ID,
                        PayerType = cells.Byte(row, "PayerType", LogisticsEnums.ParsePayerType),
                        PayerID = cells.Int(row, "PayerID"),
                        InvoiceID = cells.Int(row, "InvoiceID"),
                        SalesInvoiceID = cells.Long(row, "SalesInvoiceID"),
                        SalesOrderID = cells.Int(row, "SalesOrderID"),
                        PurchaseOrderID = cells.Int(row, "PurchaseOrderID"),
                        TTNKID = cells.Long(row, "TTNKID"),
                        TruckNo = cells.String(row, "TruckNo"),
                        TrailerNo = cells.String(row, "TrailerNo"),
                        TruckModel = cells.String(row, "TruckModel"),
                        TrailerModel = cells.String(row, "TrailerModel"),
                        DriverName = cells.String(row, "DriverName"),
                        DriverPINFL = cells.String(row, "DriverPINFL"),
                        PlannedDeliveryDate = cells.Date(row, "PlannedDeliveryDate"),
                        ActualDeliveryDate = cells.Date(row, "ActualDeliveryDate"),
                        Description = cells.String(row, "Description"),
                        ManualCorrectionReason = cells.String(row, "ManualCorrectionReason"),
                        IsDeleted = false
                    };

                    var tariffError = ApplyTariff(waybill, tariffs[contract.ID]);
                    if (tariffError != null)
                    {
                        errors.Add($"Qator {row}: {tariffError}");
                        continue;
                    }

                    // LOG-B12(a): har qator alohida saqlanadi — bitta noto'g'ri qator (uzunlik, FK) butun faylni
                    // yiqitmaydi va xato qator raqami bilan ko'rsatiladi
                    db.F_WAYBILLS.Add(waybill);
                    try
                    {
                        await db.SaveChangesAsync();
                        existingWaybillNos.Add(waybillNo);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        db.Entry(waybill).State = EntityState.Detached;
                        errors.Add($"Qator {row}: saqlab bo'lmadi — {DescribeSaveError(ex)}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// LOG-B06: qatorni shartnomaning faol tarifiga solishtiradi. Bo'sh asos/stavka/valyuta tarifdan to'ldiriladi,
        /// summa berilmagan bo'lsa MinimumCharge bilan hisoblanadi. Xato bo'lsa matn qaytaradi.
        /// </summary>
        private static string ApplyTariff(F_WAYBILLS waybill, IEnumerable<TariffRow> contractTariffs)
        {
            bool hasReason = !string.IsNullOrWhiteSpace(waybill.ManualCorrectionReason);
            var tariff = MatchTariff(contractTariffs, waybill.TransportTypeID, waybill.FromLocation, waybill.ToLocation);

            if (tariff == null)
            {
                if (!hasReason)
                    return "shartnomada ushbu transport turi va yo'nalish uchun faol tarif topilmadi — FreightBasis, Rate va ManualCorrectionReason kiriting.";
                if (!waybill.Amount.HasValue && (!waybill.FreightBasis.HasValue || !waybill.Rate.HasValue))
                    return "tarif yo'q: Amount yoki FreightBasis + Rate majburiy.";
            }
            else
            {
                var mismatches = new List<string>();
                if (waybill.FreightBasis.HasValue && waybill.FreightBasis.Value != tariff.FreightBasis)
                    mismatches.Add($"asos ({LogisticsEnums.FreightBasisText(waybill.FreightBasis)} ≠ {LogisticsEnums.FreightBasisText(tariff.FreightBasis)})");
                if (waybill.Rate.HasValue && Math.Abs(waybill.Rate.Value - tariff.Rate) > 0.005m)
                    mismatches.Add($"stavka ({waybill.Rate.Value:0.##} ≠ {tariff.Rate:0.##})");
                if (waybill.CurrencyID.HasValue && tariff.CurrencyID.HasValue && waybill.CurrencyID != tariff.CurrencyID)
                    mismatches.Add("valyuta");

                if (mismatches.Count > 0 && !hasReason)
                    return $"tarif #{tariff.ID} dan farq qiladi: {string.Join(", ", mismatches)} — ManualCorrectionReason kiriting.";

                waybill.TariffID = tariff.ID;
                waybill.FreightBasis = waybill.FreightBasis ?? tariff.FreightBasis;
                waybill.Rate = waybill.Rate ?? tariff.Rate;
                waybill.CurrencyID = waybill.CurrencyID ?? tariff.CurrencyID;
            }

            if (!waybill.Amount.HasValue)
            {
                waybill.Amount = FreightCalculator.Calculate(waybill.FreightBasis, waybill.Rate, waybill.GrWeight,
                    waybill.DistanceKm, tariff?.MinimumCharge);
            }
            return null;
        }

        /// <summary>
        /// Transport turi mos (yoki tarifda bo'sh) tariflar orasidan eng aniq mosini tanlaydi: tarifdagi
        /// shahar/viloyat joy matnida uchrasa ustunlik, teng bo'lsa eng yangisi.
        /// </summary>
        private static TariffRow MatchTariff(IEnumerable<TariffRow> contractTariffs, int? transportTypeId, string from, string to)
        {
            TariffRow best = null;
            int bestScore = -1;
            foreach (var t in contractTariffs.Where(t => t.TransportTypeID == null || t.TransportTypeID == transportTypeId))
            {
                // Joylar erkin matn ("Supplier A") bo'lishi mumkin, shuning uchun yo'nalish faqat ustunlik beradi,
                // tarifni chiqarib tashlamaydi (formada ham tarif yo'nalish bo'yicha cheklanmaydi)
                int score = (t.TransportTypeID == transportTypeId ? 1 : 0)
                    + Mentions(from, t.FromCity, 4) + Mentions(to, t.ToCity, 4)
                    + Mentions(from, t.FromRegion, 2) + Mentions(to, t.ToRegion, 2);
                if (score > bestScore || (score == bestScore && t.CreatedDT > best.CreatedDT))
                {
                    best = t;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int Mentions(string location, string place, int weight)
        {
            return !string.IsNullOrWhiteSpace(location) && !string.IsNullOrWhiteSpace(place) &&
                   location.IndexOf(place.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ? weight : 0;
        }

        private static string DescribeSaveError(Exception ex)
        {
            var validation = ex as System.Data.Entity.Validation.DbEntityValidationException;
            if (validation != null)
            {
                return string.Join("; ", validation.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => e.PropertyName + ": " + e.ErrorMessage));
            }

            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            var sql = inner as System.Data.SqlClient.SqlException;
            if (sql != null && sql.Number == 547)
                return "bog'langan hujjat (invoys/buyurtma/TTNK ID) topilmadi.";
            if (sql != null && (sql.Number == 2601 || sql.Number == 2627))
                return "raqam takrorlangan.";
            if (sql != null && sql.Number == 8152)
                return "matn juda uzun.";
            return "ma'lumot formati noto'g'ri.";
        }
    }
}
