using System;
using System.Collections.Generic;
using System.Globalization;
using OfficeOpenXml;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// Excel import uchun sarlavha xaritasi va katak o'qish (umumiy; WaybillController'dan ajratildi, LOG-D05).
    /// LOG-B12: raqam va sanalar formatlangan matndan (cell.Text, madaniyatga bog'liq — "1,875,000.00"
    /// jimgina null bo'lardi) emas, katak qiymatidan (cell.Value) o'qiladi; matn bo'lsa InvariantCulture,
    /// so'ng joriy madaniyat bilan.
    /// Sarlavha nomi o'zgaruvchan bo'lsa (masalan "PNo" / "Mahsulot") — <see cref="Alias"/> bilan
    /// kanonik kalitga bog'lang.
    /// </summary>
    public sealed class ExcelCellReader
    {
        private readonly ExcelWorksheet _sheet;
        private readonly Dictionary<string, int> _map;

        public ExcelCellReader(ExcelWorksheet sheet, int headerRow = 1)
        {
            _sheet = sheet;
            _map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = sheet.Dimension?.End.Column ?? 0;
            for (int col = 1; col <= lastCol; col++)
            {
                var header = sheet.Cells[headerRow, col].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(header) && !_map.ContainsKey(header))
                    _map[header] = col;
            }
        }

        public bool Has(string key) => _map.ContainsKey(key);

        /// <summary>
        /// Sarlavhasida <paramref name="fragments"/> dan birortasi (katta-kichik harf farqsiz) bo'lgan
        /// birinchi ustunni <paramref name="key"/> nomi bilan bog'laydi. Kalit allaqachon bo'lsa — o'zgarmaydi.
        /// </summary>
        public ExcelCellReader Alias(string key, params string[] fragments)
        {
            if (_map.ContainsKey(key)) return this;
            int best = int.MaxValue;
            foreach (var pair in _map)
            {
                foreach (var fragment in fragments)
                {
                    if (pair.Key.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0 && pair.Value < best)
                        best = pair.Value;
                }
            }
            if (best != int.MaxValue)
                _map[key] = best;
            return this;
        }

        private object Value(int row, string key)
        {
            int col;
            return _map.TryGetValue(key, out col) ? _sheet.Cells[row, col].Value : null;
        }

        public string String(int row, string key)
        {
            int col;
            if (!_map.TryGetValue(key, out col)) return null;
            var text = _sheet.Cells[row, col].Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        public double? Double(int row, string key)
        {
            var value = Value(row, key);
            if (value == null) return null;
            if (value is double d) return d;
            if (value is decimal m) return (double)m;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is float f) return f;

            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().Replace(" ", "").Replace(" ", "");
            if (string.IsNullOrEmpty(text)) return null;

            double parsed;
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out parsed))
                return parsed;
            return null;
        }

        public int? Int(int row, string key)
        {
            var d = Double(row, key);
            return d.HasValue && d.Value >= int.MinValue && d.Value <= int.MaxValue && Math.Abs(d.Value % 1) < 1e-9
                ? (int?)d.Value
                : null;
        }

        public long? Long(int row, string key)
        {
            var d = Double(row, key);
            return d.HasValue && Math.Abs(d.Value % 1) < 1e-9 ? (long?)d.Value : null;
        }

        /// <summary>Son (0, 1, ...) yoki matnli nom (parser orqali).</summary>
        public byte? Byte(int row, string key, Func<string, byte?> parser = null)
        {
            var d = Double(row, key);
            if (d.HasValue && d.Value >= 0 && d.Value <= 255 && Math.Abs(d.Value % 1) < 1e-9)
                return (byte)d.Value;
            var text = String(row, key);
            return text != null && parser != null ? parser(text) : null;
        }

        public DateTime? Date(int row, string key)
        {
            var value = Value(row, key);
            if (value == null) return null;
            if (value is DateTime dt) return dt;
            if (value is double oa) return DateTime.FromOADate(oa);

            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            DateTime parsed;
            string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd HH:mm", "dd.MM.yyyy HH:mm" };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed;
            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
                return parsed;
            return null;
        }
    }
}
