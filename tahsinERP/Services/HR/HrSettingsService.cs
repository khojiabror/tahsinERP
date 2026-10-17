using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace tahsinERP.Services.HR
{
    /// <summary>HR sozlamalari: ish kalendari va ish haqi koeffitsientlari (HRM-B15).</summary>
    public sealed class HrSettings
    {
        /// <summary>Ish kunlari (DayOfWeek: 0 = yakshanba ... 6 = shanba).</summary>
        public List<int> WorkDays { get; set; } = new List<int> { 1, 2, 3, 4, 5 };

        /// <summary>Bayram / dam olish kunlari (yyyy-MM-dd).</summary>
        public List<string> Holidays { get; set; } = new List<string>();

        /// <summary>Kunlik me'yor (soat): ish kunida undan oshgani qo'shimcha soat.</summary>
        public decimal DailyNormHours { get; set; } = 8m;

        /// <summary>Qo'shimcha soat koeffitsienti.</summary>
        public decimal OvertimeMultiplier { get; set; } = 1.5m;

        /// <summary>Yangi hisob uchun standart daromad solig'i foizi.</summary>
        public decimal DefaultIncomeTaxPercent { get; set; } = 12m;

        public bool IsWorkingDay(DateTime date)
        {
            if (!WorkDays.Contains((int)date.DayOfWeek)) return false;
            return !Holidays.Contains(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        public int WorkingDaysInMonth(int year, int month)
        {
            int days = DateTime.DaysInMonth(year, month);
            int count = 0;
            for (int d = 1; d <= days; d++)
            {
                if (IsWorkingDay(new DateTime(year, month, d))) count++;
            }
            return count;
        }

        /// <summary>Oylik fond soati = ish kunlari × kunlik me'yor.</summary>
        public decimal FundHoursForMonth(int year, int month) => WorkingDaysInMonth(year, month) * DailyNormHours;

        public HrSettings Clone() => JsonConvert.DeserializeObject<HrSettings>(JsonConvert.SerializeObject(this));
    }

    /// <summary>
    /// HR sozlamalarini saqlash. DB sxemasi o'zgarmasligi uchun App_Data/HrSettings.json faylida, xotirada keshlanadi.
    /// Sozlamalar sahifasi: /HrSettings.
    /// </summary>
    public static class HrSettingsService
    {
        private static readonly object Sync = new object();
        private static HrSettings _current;

        private static string FilePath =>
            HostingEnvironment.MapPath("~/App_Data/HrSettings.json") ??
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "HrSettings.json");

        /// <summary>Joriy sozlamalar (o'zgartirmang — nusxa uchun <see cref="HrSettings.Clone"/>).</summary>
        public static HrSettings Current
        {
            get
            {
                var s = _current;
                if (s != null) return s;
                lock (Sync)
                {
                    if (_current == null) _current = Load();
                    return _current;
                }
            }
        }

        private static HrSettings Load()
        {
            try
            {
                var path = FilePath;
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<HrSettings>(File.ReadAllText(path));
                    if (loaded != null) return Normalize(loaded);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrSettings] O'qishda xato, standart qiymatlar olinadi: " + ex);
            }
            return new HrSettings();
        }

        public static void Save(HrSettings settings)
        {
            var normalized = Normalize(settings.Clone());
            lock (Sync)
            {
                var path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(normalized, Formatting.Indented));
                _current = normalized;
            }
        }

        private static HrSettings Normalize(HrSettings s)
        {
            s.WorkDays = (s.WorkDays ?? new List<int>()).Where(d => d >= 0 && d <= 6).Distinct().OrderBy(d => d).ToList();
            s.Holidays = (s.Holidays ?? new List<string>())
                .Select(h => DateTime.TryParse(h, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null)
                .Where(h => h != null).Distinct().OrderBy(h => h).ToList();
            if (s.DailyNormHours <= 0 || s.DailyNormHours > 24) s.DailyNormHours = 8m;
            if (s.OvertimeMultiplier < 1 || s.OvertimeMultiplier > 10) s.OvertimeMultiplier = 1.5m;
            if (s.DefaultIncomeTaxPercent < 0 || s.DefaultIncomeTaxPercent > 100) s.DefaultIncomeTaxPercent = 12m;
            return s;
        }
    }
}
