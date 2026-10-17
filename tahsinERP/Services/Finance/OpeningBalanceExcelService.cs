using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using tahsinERP.Helpers;
using tahsinERP.Models;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Opening balans Excel shabloni va importi (AR/AP varaqlari; FIN-M02: controllerdan ajratildi).
    /// Import faqat formaga qatorlar qaytaradi — saqlash "Saqlash" tugmasi orqali.
    /// </summary>
    public static class OpeningBalanceExcelService
    {
        private static readonly string[] Headers =
        {
            "CounterpartyName", "CounterpartyID", "ContractNo", "ContractID", "TotalAmount", "PaidAmount", "Currency"
        };

        public sealed class ImportedLine
        {
            public int CounterpartyID { get; set; }
            public string CounterpartyName { get; set; }
            public int ContractID { get; set; }
            public string ContractNo { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal Amount { get; set; }
            public int CurrencyID { get; set; }
        }

        public sealed class ImportResult
        {
            public List<ImportedLine> CustomerLines { get; } = new List<ImportedLine>();
            public List<ImportedLine> SupplierLines { get; } = new List<ImportedLine>();
            public List<string> Errors { get; } = new List<string>();
        }

        public static byte[] Template()
        {
            using (var workbook = new XLWorkbook())
            {
                var ar = workbook.Worksheets.Add("AR");
                var ap = workbook.Worksheets.Add("AP");
                for (int i = 0; i < Headers.Length; i++)
                {
                    ar.Cell(1, i + 1).Value = Headers[i];
                    ap.Cell(1, i + 1).Value = Headers[i];
                }

                ar.Cell(2, 1).Value = "Uz-Koram Kompani";
                ar.Cell(2, 2).Value = "";
                ar.Cell(2, 3).Value = "27/10-2025";
                ar.Cell(2, 4).Value = "";
                ar.Cell(2, 5).Value = 56576700;
                ar.Cell(2, 6).Value = 51467720;
                ar.Cell(2, 7).Value = "UZS";

                ap.Cell(2, 1).Value = "Supplier name";
                ap.Cell(2, 2).Value = "";
                ap.Cell(2, 3).Value = "Contract-001";
                ap.Cell(2, 4).Value = "";
                ap.Cell(2, 5).Value = 10000000;
                ap.Cell(2, 6).Value = 2500000;
                ap.Cell(2, 7).Value = "UZS";

                ar.Range(1, 1, 1, Headers.Length).Style.Font.Bold = true;
                ap.Range(1, 1, 1, Headers.Length).Style.Font.Bold = true;
                ar.Columns().AdjustToContents();
                ap.Columns().AdjustToContents();

                return workbook.ToByteArray();
            }
        }

        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();
        }

        private static decimal? ParseDec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var cleaned = s.Replace(" ", "").Replace(",", ".");
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.CurrentCulture, out v)) return v;
            return null;
        }

        private sealed class Party
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        private sealed class Contract
        {
            public int ID { get; set; }
            public int CounterpartyID { get; set; }
            public string ContractNo { get; set; }
        }

        /// <summary>
        /// AR (mijozlar) va AP (ta'minotchilar) varaqlarini o'qiydi. Kontragent/shartnoma ID yoki nom/raqam
        /// bo'yicha topiladi; TotalAmount bo'sh bo'lsa — shartnoma bo'yicha sana holatidagi summa olinadi.
        /// </summary>
        public static ImportResult Import(Stream stream, DateTime snapshotDate, DBTHSNEntities db)
        {
            var result = new ImportResult();
            var defaultCurrencyId = new OpeningBalanceService(db).DefaultCurrencyId();
            var currencyByCode = db.CURRENCIES.AsNoTracking().Select(x => new { x.ID, x.Shortname }).ToList()
                .GroupBy(x => (x.Shortname ?? "").Trim().ToUpper())
                .ToDictionary(g => g.Key, g => g.First().ID);

            var customers = db.CUSTOMERS.AsNoTracking().Where(x => x.IsDeleted != true)
                .Select(x => new Party { ID = x.ID, Name = x.Name }).ToList();
            var suppliers = db.SUPPLIERS.AsNoTracking().Where(x => x.IsDeleted != true)
                .Select(x => new Party { ID = x.ID, Name = x.Name }).ToList();
            var sContracts = db.S_CONTRACTS.AsNoTracking().Where(x => x.IsDeleted != true)
                .Select(x => new Contract { ID = x.ID, CounterpartyID = x.CustomerID, ContractNo = x.ContractNo }).ToList();
            var pContracts = db.P_CONTRACTS.AsNoTracking().Where(x => x.IsDeleted != true)
                .Select(x => new Contract { ID = x.ID, CounterpartyID = x.SupplierID, ContractNo = x.ContractNo }).ToList();
            var lookup = new FinCounterpartyLookupService(db);

            using (var workbook = new XLWorkbook(stream))
            {
                void ImportSheet(string sheetName, bool isCustomer)
                {
                    var ws = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
                    if (ws == null || ws.LastRowNumber() == 0) return;

                    var parties = isCustomer ? customers : suppliers;
                    var contracts = isCustomer ? sContracts : pContracts;
                    var target = isCustomer ? result.CustomerLines : result.SupplierLines;

                    var rowCount = ws.LastRowNumber();
                    for (int r = 2; r <= rowCount; r++)
                    {
                        var cpNameRaw = ws.Cell(r, 1).GetFormattedString()?.Trim();
                        var cpIdRaw = ws.Cell(r, 2).GetFormattedString()?.Trim();
                        var contractNoRaw = ws.Cell(r, 3).GetFormattedString()?.Trim();
                        var contractIdRaw = ws.Cell(r, 4).GetFormattedString()?.Trim();
                        var totalRaw = ws.Cell(r, 5).GetFormattedString()?.Trim();
                        var paidRaw = ws.Cell(r, 6).GetFormattedString()?.Trim();
                        var currencyRaw = ws.Cell(r, 7).GetFormattedString()?.Trim();

                        if (string.IsNullOrWhiteSpace(cpNameRaw) &&
                            string.IsNullOrWhiteSpace(cpIdRaw) &&
                            string.IsNullOrWhiteSpace(contractNoRaw) &&
                            string.IsNullOrWhiteSpace(contractIdRaw) &&
                            string.IsNullOrWhiteSpace(totalRaw) &&
                            string.IsNullOrWhiteSpace(paidRaw))
                        {
                            continue;
                        }

                        int cpId = 0;
                        if (!string.IsNullOrWhiteSpace(cpIdRaw) && int.TryParse(cpIdRaw, out var parsedCp))
                            cpId = parsedCp;
                        else if (!string.IsNullOrWhiteSpace(cpNameRaw))
                            cpId = parties.FirstOrDefault(x => Norm(x.Name) == Norm(cpNameRaw))?.ID ?? 0;

                        if (cpId <= 0)
                        {
                            result.Errors.Add(sheetName + " row " + r + ": kontragent topilmadi.");
                            continue;
                        }

                        int contractId = 0;
                        if (!string.IsNullOrWhiteSpace(contractIdRaw) && int.TryParse(contractIdRaw, out var parsedContract))
                            contractId = parsedContract;
                        else if (!string.IsNullOrWhiteSpace(contractNoRaw))
                            contractId = contracts.FirstOrDefault(x => x.CounterpartyID == cpId && Norm(x.ContractNo) == Norm(contractNoRaw))?.ID ?? 0;

                        if (contractId <= 0)
                        {
                            result.Errors.Add(sheetName + " row " + r + ": shartnoma topilmadi.");
                            continue;
                        }

                        var contractNo = contracts.FirstOrDefault(x => x.ID == contractId && x.CounterpartyID == cpId)?.ContractNo ?? "";
                        if (string.IsNullOrWhiteSpace(contractNo))
                        {
                            result.Errors.Add(sheetName + " row " + r + ": shartnoma kontragentga mos emas.");
                            continue;
                        }

                        var totalAmount = ParseDec(totalRaw)
                            ?? lookup.Totals(isCustomer ? FinCounterpartyLookupService.Customer : FinCounterpartyLookupService.Supplier,
                                cpId, contractId, snapshotDate).Total;
                        var paidAmount = ParseDec(paidRaw) ?? 0m;
                        if (paidAmount > totalAmount) paidAmount = totalAmount;
                        if (paidAmount < 0) paidAmount = 0;

                        var currencyCode = Norm(currencyRaw);
                        var currencyId = currencyCode != "" && currencyByCode.TryGetValue(currencyCode, out var cid)
                            ? cid
                            : defaultCurrencyId;

                        target.Add(new ImportedLine
                        {
                            CounterpartyID = cpId,
                            CounterpartyName = parties.FirstOrDefault(x => x.ID == cpId)?.Name ?? "",
                            ContractID = contractId,
                            ContractNo = contractNo,
                            TotalAmount = totalAmount,
                            PaidAmount = paidAmount,
                            Amount = totalAmount - paidAmount,
                            CurrencyID = currencyId
                        });
                    }
                }

                ImportSheet("AR", true);
                ImportSheet("AP", false);
            }

            return result;
        }
    }
}
