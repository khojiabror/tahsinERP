using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Xodim ustamalari (moddiy javobgarlik, maxsus ustama). Jadval EDMX'da bor —
    /// avvalgi har chaqiruvdagi <c>IF OBJECT_ID ... CREATE TABLE</c> DDL va raw SQL olib tashlandi (HRM-P13).
    /// </summary>
    public sealed class HrEmployeeAllowanceService
    {
        private readonly DBTHSNEntities _db;

        public HrEmployeeAllowanceService(DBTHSNEntities db)
        {
            _db = db;
        }

        public sealed class EmployeeAllowanceRow
        {
            public int EmployeeID { get; set; }
            public decimal MaterialResponsibilityPercent { get; set; }
            public decimal SpecialAllowancePercent { get; set; }

            /// <summary>Jami ustama foizi (manfiy qiymatlar 0 deb olinadi).</summary>
            public decimal TotalPercent =>
                Math.Max(0m, MaterialResponsibilityPercent) + Math.Max(0m, SpecialAllowancePercent);
        }

        public EmployeeAllowanceRow GetByEmployeeId(int employeeId)
        {
            return _db.HR_EMPLOYEE_ALLOWANCES.AsNoTracking()
                .Where(a => a.EmployeeID == employeeId)
                .Select(a => new EmployeeAllowanceRow
                {
                    EmployeeID = a.EmployeeID,
                    MaterialResponsibilityPercent = a.MaterialResponsibilityPercent,
                    SpecialAllowancePercent = a.SpecialAllowancePercent
                })
                .FirstOrDefault();
        }

        public Dictionary<int, EmployeeAllowanceRow> GetByEmployeeIds(IEnumerable<int> employeeIds)
        {
            var ids = employeeIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
            {
                return new Dictionary<int, EmployeeAllowanceRow>();
            }

            return _db.HR_EMPLOYEE_ALLOWANCES.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeID))
                .Select(a => new EmployeeAllowanceRow
                {
                    EmployeeID = a.EmployeeID,
                    MaterialResponsibilityPercent = a.MaterialResponsibilityPercent,
                    SpecialAllowancePercent = a.SpecialAllowancePercent
                })
                .ToList()
                .ToDictionary(x => x.EmployeeID, x => x);
        }

        /// <summary>Qo'shadi yoki yangilaydi va darhol saqlaydi (avvalgi MERGE kabi).</summary>
        public void Upsert(int employeeId, decimal materialResponsibilityPercent, decimal specialAllowancePercent)
        {
            var row = _db.HR_EMPLOYEE_ALLOWANCES.Find(employeeId);
            var now = DateTime.Now;
            if (row == null)
            {
                _db.HR_EMPLOYEE_ALLOWANCES.Add(new HR_EMPLOYEE_ALLOWANCES
                {
                    EmployeeID = employeeId,
                    MaterialResponsibilityPercent = materialResponsibilityPercent,
                    SpecialAllowancePercent = specialAllowancePercent,
                    CreatedDT = now,
                    UpdatedDT = now
                });
            }
            else
            {
                row.MaterialResponsibilityPercent = materialResponsibilityPercent;
                row.SpecialAllowancePercent = specialAllowancePercent;
                row.UpdatedDT = now;
            }
            _db.SaveChanges();
        }
    }
}
