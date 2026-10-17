using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.HR
{
    /// <summary>
    /// Xodim lavozim davrlari (HR_EMPLOYEE_HISTORY) — Transfer, Dismiss va Delete uchun yagona qoida (HRM-B06).
    ///
    /// Model: har qator — xodim FromOrgUnitID/FromPositionID'da ishlagan davr [EffectiveDateFrom, EffectiveDateTo];
    /// EffectiveDateTo = NULL — joriy (ochiq) davr. HR_EMPLOYEES.EmploymentDate — ishga qabul sanasi, o'zgarmaydi
    /// (avval Transfer uni o'tkazish sanasiga almashtirardi va staj buzilardi).
    /// </summary>
    public static class EmployeeHistoryService
    {
        public const string ActionInitial = "Initial Employment";
        public const string ActionTransfer = "Transfer";
        public const string ActionHistory = "History";

        /// <summary>
        /// Joriy davrni <paramref name="endDate"/> bilan yopadi (SaveChanges chaqiruvchida).
        /// Ochiq qator bo'lmasa (tarixsiz eski ma'lumot) — EmploymentDate..endDate davri yaratiladi.
        /// Davr boshidan oldingi sana berilsa, davr bir kunlik qilib yopiladi.
        /// </summary>
        public static async Task CloseCurrentPeriodAsync(DBTHSNEntities db, HR_EMPLOYEES employee, DateTime endDate,
            string actionTypeIfMissing, string reasonIfMissing, int? userId)
        {
            var end = endDate.Date;
            var openRows = await db.HR_EMPLOYEE_HISTORY
                .Where(h => h.EmployeeID == employee.ID && h.EffectiveDateTo == null)
                .OrderByDescending(h => h.EffectiveDateFrom)
                .ToListAsync();

            if (openRows.Count > 0)
            {
                // Bir nechta ochiq qator (eski xato) bo'lsa — hammasi yopiladi
                foreach (var row in openRows)
                {
                    row.EffectiveDateTo = end >= row.EffectiveDateFrom.Date ? end : row.EffectiveDateFrom.Date;
                }
                return;
            }

            bool hasAnyHistory = await db.HR_EMPLOYEE_HISTORY.AnyAsync(h => h.EmployeeID == employee.ID);
            if (hasAnyHistory || !employee.EmploymentDate.HasValue ||
                !employee.OrgUnitID.HasValue || !employee.PositionID.HasValue)
            {
                return;
            }

            var start = employee.EmploymentDate.Value.Date;
            db.HR_EMPLOYEE_HISTORY.Add(new HR_EMPLOYEE_HISTORY
            {
                EmployeeID = employee.ID,
                ActionType = actionTypeIfMissing,
                FromOrgUnitID = employee.OrgUnitID,
                FromPositionID = employee.PositionID,
                ToOrgUnitID = null,
                ToPositionID = null,
                EffectiveDateFrom = start,
                EffectiveDateTo = end >= start ? end : start,
                Reason = reasonIfMissing,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            });
        }
    }
}
