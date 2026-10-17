using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Tabel sozlamalari (oylik fond soati). Jadval EDMX'da bor —
    /// avvalgi har chaqiruvdagi <c>IF OBJECT_ID ... CREATE TABLE</c> DDL va raw SQL olib tashlandi (HRM-P13).
    /// </summary>
    public sealed class HrTimesheetSettingsService
    {
        public const decimal DefaultFundHours = 160m;
        private readonly DBTHSNEntities _db;

        public HrTimesheetSettingsService(DBTHSNEntities db)
        {
            _db = db;
        }

        public decimal GetFundHoursOrDefault(int timesheetId)
        {
            var fundHours = _db.HR_TIMESHEET_SETTINGS.AsNoTracking()
                .Where(s => s.TimesheetID == timesheetId)
                .Select(s => (decimal?)s.FundHours)
                .FirstOrDefault();
            return Normalize(fundHours);
        }

        /// <summary>Bir nechta tabel uchun fond soati (topilmasa standart qiymat).</summary>
        public Dictionary<int, decimal> GetFundHoursOrDefault(IEnumerable<int> timesheetIds)
        {
            var ids = timesheetIds?.Distinct().ToList() ?? new List<int>();
            var found = ids.Count == 0
                ? new Dictionary<int, decimal>()
                : _db.HR_TIMESHEET_SETTINGS.AsNoTracking()
                    .Where(s => ids.Contains(s.TimesheetID))
                    .Select(s => new { s.TimesheetID, s.FundHours })
                    .ToList()
                    .ToDictionary(s => s.TimesheetID, s => s.FundHours);
            return ids.ToDictionary(id => id, id => Normalize(found.TryGetValue(id, out var h) ? h : (decimal?)null));
        }

        /// <summary>Qo'shadi yoki yangilaydi va darhol saqlaydi (avvalgi MERGE kabi).</summary>
        public void UpsertFundHours(int timesheetId, decimal fundHours)
        {
            var normalizedFundHours = fundHours > 0 ? fundHours : DefaultFundHours;
            var row = _db.HR_TIMESHEET_SETTINGS.Find(timesheetId);
            var now = DateTime.Now;
            if (row == null)
            {
                _db.HR_TIMESHEET_SETTINGS.Add(new HR_TIMESHEET_SETTINGS
                {
                    TimesheetID = timesheetId,
                    FundHours = normalizedFundHours,
                    CreatedDT = now,
                    UpdatedDT = now
                });
            }
            else
            {
                row.FundHours = normalizedFundHours;
                row.UpdatedDT = now;
            }
            _db.SaveChanges();
        }

        private static decimal Normalize(decimal? fundHours)
        {
            return fundHours.HasValue && fundHours.Value > 0 ? fundHours.Value : DefaultFundHours;
        }
    }
}
