using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Services.Finance
{
    /// <summary>Excel importi natijasi: saqlashga tayyor qatorlar, xatolar (o'tkazib yuborilgan) va ogohlantirishlar.</summary>
    public sealed class FinTransactionImportResult
    {
        public List<FIN_TRANSACTIONS> Transactions { get; } = new List<FIN_TRANSACTIONS>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// FinTransactions Excel shabloni va importi (FIN-M03: controllerdan ajratildi; sana o'qish bloki
    /// endi bitta — avval ikki marta yozilgan edi).
    /// FIN-B23: dublikat kod/nomli hisob-kategoriya ToDictionary'da yiqilmaydi (birinchisi olinadi),
    /// hisob valyutasi tekshiriladi (Create'dagi kabi), bazada aynan shunday tranzaksiya bo'lsa qator o'tkazib yuboriladi.
    /// </summary>
    public static class FinTransactionImportService
    {
        private static readonly string[] DateFormats =
        {
            "dd.MM.yyyy", "dd/MM/yyyy", "d.M.yyyy", "d/M/yyyy",
            "MM/dd/yyyy", "M/d/yyyy", "M/dd/yyyy", "MM/d/yyyy",
            "yyyy-MM-dd", "yyyy/MM/dd",
            "dd-MM-yyyy", "d-M-yyyy"
        };

        private static string DefaultCurrencyShortname(DBTHSNEntities db)
        {
            return db.CURRENCIES
                .Where(c => c.IsDeleted != true)
                .OrderBy(c => c.ID)
                .Select(c => c.Shortname)
                .FirstOrDefault() ?? "UZS";
        }

        public static string CurrencyHint(DBTHSNEntities db)
        {
            return DefaultCurrencyShortname(db);
        }

        /// <summary>Import shabloni: namuna qatori + Hisoblar/Kategoriyalar/Valyutalar ma'lumotnoma varaqlari.</summary>
        public static byte[] Template(DBTHSNEntities db)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add(L.T("Fin_Transactions"));
                var headers = new[]
                {
                    L.T("Fin_ColDate"), L.T("Fin_ColTime"), L.T("Fin_AccountCode"), L.T("Fin_CategoryName"),
                    L.T("Fin_ColDirection", L.T("Fin_Inflow"), L.T("Fin_Expense")), L.T("Amount"),
                    L.T("Fin_CurrencyShortNameHint", DefaultCurrencyShortname(db)),
                    L.T("Fin_CounterpartyName") + " (" + L.T("Optional") + ")", L.T("Fin_Purpose") + " (" + L.T("Optional") + ")", L.T("Fin_ContractNo") + " (" + L.T("Optional") + ")", L.T("Comment") + " (" + L.T("Optional") + ")",
                    L.T("Quantity") + " (" + L.T("Optional") + ")", L.T("Fin_UnitPrice") + " (" + L.T("Optional") + ")"
                };
                for (int c = 0; c < headers.Length; c++)
                    worksheet.Cell(1, c + 1).Value = headers[c];

                var range = worksheet.Range(1, 1, 1, headers.Length);
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = XLFillPatternValues.Solid;
                range.Style.Fill.BackgroundColor = XLColor.FromColor(System.Drawing.Color.LightGray);

                var sample = new[] { "01.11.2025", "10:30", "9000", "Sotuv", "1", "1000000.00", "UZS", "Mijoz nomi", "Mahsulot sotuvi", "K-001", "Qo'shimcha izoh" };
                for (int c = 0; c < sample.Length; c++)
                    worksheet.Cell(2, c + 1).Value = sample[c];

                var accounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Code)
                    .Select(a => new { a.Code, a.Name }).ToList();
                var categories = db.FIN_CATEGORIES.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
                    .Select(c => c.Name).ToList();
                var currencies = db.CURRENCIES.AsNoTracking().OrderBy(c => c.Shortname)
                    .Select(c => c.Shortname).ToList();

                var accountsSheet = workbook.Worksheets.Add(L.T("Fin_Accounts"));
                accountsSheet.Cell(1, 1).Value = L.T("Code");
                accountsSheet.Cell(1, 2).Value = L.T("Name");
                for (int i = 0; i < accounts.Count; i++)
                {
                    accountsSheet.Cell(i + 2, 1).Value = accounts[i].Code;
                    accountsSheet.Cell(i + 2, 2).Value = accounts[i].Name;
                }

                var categoriesSheet = workbook.Worksheets.Add(L.T("Fin_Categories"));
                categoriesSheet.Cell(1, 1).Value = L.T("Name");
                for (int i = 0; i < categories.Count; i++)
                    categoriesSheet.Cell(i + 2, 1).Value = categories[i];

                var currenciesSheet = workbook.Worksheets.Add(L.T("Fin_Currencies"));
                currenciesSheet.Cell(1, 1).Value = L.T("Fin_ShortName");
                currenciesSheet.Cell(1, 2).Value = L.T("Fin_FullNameOf");
                for (int i = 0; i < currencies.Count; i++)
                {
                    currenciesSheet.Cell(i + 2, 1).Value = currencies[i];
                    currenciesSheet.Cell(i + 2, 2).Value = currencies[i];
                }

                worksheet.Columns().AdjustToContents();
                accountsSheet.Columns().AdjustToContents();
                categoriesSheet.Columns().AdjustToContents();
                currenciesSheet.Columns().AdjustToContents();

                return workbook.ToByteArray();
            }
        }

        /// <summary>Sana katagi: DateTime, Excel seriya raqami yoki matn. Bo'sh katak — <paramref name="empty"/> = true.</summary>
        private static bool TryReadDate(IXLCell cell, out DateTime date, out bool empty, out string raw)
        {
            date = default(DateTime);
            empty = false;
            raw = null;

            var value = cell.GetRawValue();
            if (value is double serial)
            {
                date = DateTime.FromOADate(serial);
                return true;
            }

            raw = (value != null ? value.ToString() : cell.GetFormattedString())?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                empty = true;
                return false;
            }

            return DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                   DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        /// <summary>Vaqt katagi (ixtiyoriy): DateTime, kun ulushi (0..1) yoki "HH:mm" matni.</summary>
        private static DateTime ApplyTime(DateTime date, IXLCell cell)
        {
            var value = cell.GetRawValue();
            if (value is double serial)
            {
                // Vaqt — kun ulushi (0..1); to'liq sana-vaqt bo'lsa — uning vaqt qismi
                return date.Date.Add(serial < 1.0 ? TimeSpan.FromDays(serial) : DateTime.FromOADate(serial).TimeOfDay);
            }

            var text = (value != null ? value.ToString() : cell.GetFormattedString())?.Trim();
            return !string.IsNullOrEmpty(text) && TimeSpan.TryParse(text, out var time)
                ? date.Date.Add(time)
                : date;
        }

        private static bool TryDecimal(string text, out decimal value)
        {
            value = 0m;
            return !string.IsNullOrEmpty(text) &&
                   decimal.TryParse(text.Replace(" ", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static string Key(DateTime date, int accountId, int categoryId, byte direction, decimal amount, int currencyId)
        {
            return string.Join("|", date.ToString("yyyyMMddHHmmss"), accountId, categoryId, direction,
                amount.ToString("0.####", CultureInfo.InvariantCulture), currencyId);
        }

        public static FinTransactionImportResult Parse(IXLWorksheet worksheet, DBTHSNEntities db, int currentUserId)
        {
            var result = new FinTransactionImportResult();
            var rowCount = worksheet.LastRowNumber();

            // FIN-B23: GroupBy().First() — takroriy kod/nom bo'lsa butun import yiqilmaydi
            var accounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsActive)
                .Select(a => new { a.ID, a.Code, a.CurrencyID }).ToList()
                .Where(a => !string.IsNullOrEmpty(a.Code))
                .GroupBy(a => a.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.ID).First(), StringComparer.OrdinalIgnoreCase);
            var categories = db.FIN_CATEGORIES.AsNoTracking().Where(c => c.IsActive)
                .Select(c => new { c.ID, c.Name }).ToList()
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.ID).First().ID, StringComparer.OrdinalIgnoreCase);
            var currencies = db.CURRENCIES.AsNoTracking()
                .Select(c => new { c.ID, c.Shortname }).ToList()
                .Where(c => !string.IsNullOrEmpty(c.Shortname))
                .GroupBy(c => c.Shortname.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.ID).First().ID, StringComparer.OrdinalIgnoreCase);

            var rowKeys = new Dictionary<string, int>();
            var parsed = new List<KeyValuePair<int, FIN_TRANSACTIONS>>();

            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    if (!TryReadDate(worksheet.Cell(row, 1), out var txnDate, out var empty, out var rawDate))
                    {
                        if (empty) continue; // bo'sh qator
                        result.Errors.Add(L.T("Fin_Imp_BadDate", row, rawDate));
                        continue;
                    }
                    txnDate = ApplyTime(txnDate, worksheet.Cell(row, 2));

                    var accountCode = worksheet.Cell(row, 3).GetFormattedString()?.Trim();
                    if (string.IsNullOrEmpty(accountCode) || !accounts.TryGetValue(accountCode, out var account))
                    {
                        result.Errors.Add(L.T("Fin_Imp_AccountNotFound", row, accountCode));
                        continue;
                    }

                    var categoryName = worksheet.Cell(row, 4).GetFormattedString()?.Trim();
                    if (string.IsNullOrEmpty(categoryName) || !categories.TryGetValue(categoryName, out var categoryId))
                    {
                        result.Errors.Add(L.T("Fin_Imp_CategoryNotFound", row, categoryName));
                        continue;
                    }

                    var directionStr = worksheet.Cell(row, 5).GetFormattedString()?.Trim();
                    if (!byte.TryParse(directionStr, out var direction) || (direction != 1 && direction != 2))
                    {
                        result.Errors.Add(L.T("Fin_Imp_BadDirection", row, directionStr));
                        continue;
                    }

                    var amountStr = worksheet.Cell(row, 6).GetFormattedString()?.Trim();
                    if (!TryDecimal(amountStr, out var amount))
                    {
                        result.Errors.Add(L.T("Fin_Imp_BadAmount", row, amountStr));
                        continue;
                    }

                    var currencyShortname = worksheet.Cell(row, 7).GetFormattedString()?.Trim();
                    if (string.IsNullOrEmpty(currencyShortname) || !currencies.TryGetValue(currencyShortname, out var currencyId))
                    {
                        result.Errors.Add(L.T("Fin_Imp_CurrencyNotFound", row, currencyShortname));
                        continue;
                    }

                    // FIN-B23: Create'dagi kabi hisob valyutasi tranzaksiya valyutasiga mos bo'lishi shart
                    if (account.CurrencyID != currencyId)
                    {
                        result.Errors.Add(L.T("Fin_Imp_AccountCurrencyMismatch", row, accountCode, currencyShortname));
                        continue;
                    }

                    var transaction = new FIN_TRANSACTIONS
                    {
                        TxnDate = txnDate,
                        AccountID = account.ID,
                        CategoryID = categoryId,
                        Direction = direction,
                        Amount = amount,
                        CurrencyID = currencyId,
                        CounterpartyName = NullIfEmpty(worksheet.Cell(row, 8).GetFormattedString()?.Trim()),
                        Purpose = NullIfEmpty(worksheet.Cell(row, 9).GetFormattedString()?.Trim()),
                        ContractNo = NullIfEmpty(worksheet.Cell(row, 10).GetFormattedString()?.Trim()),
                        Comment = NullIfEmpty(worksheet.Cell(row, 11).GetFormattedString()?.Trim()),
                        Qty = TryDecimal(worksheet.Cell(row, 12).GetFormattedString()?.Trim(), out var qty) ? qty : (decimal?)null,
                        PricePerUnit = TryDecimal(worksheet.Cell(row, 13).GetFormattedString()?.Trim(), out var price) ? price : (decimal?)null,
                        SourceDocType = FinSelectLists.Manual,
                        CreatedByUserID = currentUserId,
                        CreatedDT = DateTime.Now
                    };

                    var key = Key(txnDate, account.ID, categoryId, direction, amount, currencyId);
                    if (rowKeys.TryGetValue(key, out var firstRow))
                    {
                        result.Warnings.Add(L.T("Fin_Imp_DuplicateRow", row, firstRow));
                    }
                    else
                    {
                        rowKeys[key] = row;
                    }
                    parsed.Add(new KeyValuePair<int, FIN_TRANSACTIONS>(row, transaction));
                }
                catch (Exception ex)
                {
                    var errorMsg = L.T("Fin_Imp_RowError", row, ex.Message);
                    if (ex.InnerException != null)
                        errorMsg += $" ({ex.InnerException.Message})";
                    result.Errors.Add(errorMsg);
                }
            }

            // FIN-B23: shu fayl qayta yuklansa — bazada aynan shunday (sana-vaqt, hisob, kategoriya, yo'nalish, summa) tranzaksiya bor
            var existing = ExistingKeys(db, parsed.Select(p => p.Value).ToList());
            foreach (var pair in parsed)
            {
                var t = pair.Value;
                if (existing.Contains(Key(t.TxnDate, t.AccountID, t.CategoryID, t.Direction, t.Amount, t.CurrencyID)))
                {
                    result.Errors.Add(L.T("Fin_Imp_AlreadyExists", pair.Key));
                    continue;
                }
                result.Transactions.Add(t);
            }

            return result;
        }

        private static HashSet<string> ExistingKeys(DBTHSNEntities db, List<FIN_TRANSACTIONS> rows)
        {
            var keys = new HashSet<string>();
            if (rows.Count == 0) return keys;

            var from = rows.Min(r => r.TxnDate);
            var to = rows.Max(r => r.TxnDate);
            var accountIds = rows.Select(r => r.AccountID).Distinct().ToList();

            var existing = FinQueries.Active(db)
                .Where(t => t.TxnDate >= from && t.TxnDate <= to && accountIds.Contains(t.AccountID))
                .Select(t => new { t.TxnDate, t.AccountID, t.CategoryID, t.Direction, t.Amount, t.CurrencyID })
                .ToList();
            foreach (var t in existing)
                keys.Add(Key(t.TxnDate, t.AccountID, t.CategoryID, t.Direction, t.Amount, t.CurrencyID));
            return keys;
        }
    }
}
