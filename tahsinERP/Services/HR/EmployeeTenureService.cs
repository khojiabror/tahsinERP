using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.HR
{
    public class EmployeeTenureService
    {
        private DBTHSNEntities db;

        public EmployeeTenureService(DBTHSNEntities context)
        {
            db = context;
        }

        /// <summary>
        /// Calculate total tenure for an employee based on all HR_EMPLOYEE_HISTORY records and current employment
        /// Formula: EmploymentDate dan Today() gacha + barcha history record periodlari
        /// </summary>
        public async Task<TenureResult> CalculateTotalTenureAsync(int employeeId)
        {
            var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
            if (employee == null || !employee.EmploymentDate.HasValue)
            {
                return new TenureResult { TotalDays = 0, Years = 0, Months = 0, Days = 0 };
            }

            var historyRecords = await db.Database.SqlQuery<TenureInfo>(
                @"SELECT EffectiveDateFrom, EffectiveDateTo
                  FROM HR_EMPLOYEE_HISTORY
                  WHERE EmployeeID = {0}
                  ORDER BY EffectiveDateFrom",
                employeeId)
                .ToListAsync();

            int totalDays = 0;
            DateTime employmentStart = employee.EmploymentDate.Value;
            DateTime today = DateTime.Now;

            // If employee is active, always add period from EmploymentDate to Today
            if (employee.IsActive)
            {
                if (today >= employmentStart)
                {
                    totalDays += (today - employmentStart).Days;
                }
            }

            // Add all history records periods
            foreach (var record in historyRecords)
            {
                DateTime startDate = record.EffectiveDateFrom;
                DateTime endDate = record.EffectiveDateTo ?? (employee.IsActive ? today : (employee.DismissalDate ?? today));

                if (endDate >= startDate)
                {
                    totalDays += (endDate - startDate).Days;
                }
            }

            // Remove overlapping periods to avoid double counting
            // If history record overlaps with EmploymentDate to Today period, subtract overlap
            if (employee.IsActive && historyRecords.Any())
            {
                foreach (var record in historyRecords)
                {
                    DateTime histStart = record.EffectiveDateFrom;
                    DateTime histEnd = record.EffectiveDateTo ?? today;
                    
                    // Check if history record overlaps with EmploymentDate to Today
                    if (histStart <= today && histEnd >= employmentStart)
                    {
                        // Calculate overlap
                        DateTime overlapStart = histStart > employmentStart ? histStart : employmentStart;
                        DateTime overlapEnd = histEnd < today ? histEnd : today;
                        
                        if (overlapEnd >= overlapStart)
                        {
                            int overlapDays = (overlapEnd - overlapStart).Days;
                            totalDays -= overlapDays; // Remove overlap to avoid double counting
                        }
                    }
                }
            }

            int years = totalDays / 365;
            int months = (totalDays % 365) / 30;
            int days = (totalDays % 365) % 30;

            return new TenureResult
            {
                TotalDays = totalDays,
                Years = years,
                Months = months,
                Days = days
            };
        }

        /// <summary>
        /// Calculate tenure for a specific history record
        /// </summary>
        public TenureResult CalculateTenure(DateTime effectiveDateFrom, DateTime? effectiveDateTo)
        {
            DateTime endDate = effectiveDateTo ?? DateTime.Now;
            DateTime startDate = effectiveDateFrom;

            if (endDate < startDate)
            {
                return new TenureResult { TotalDays = 0, Years = 0, Months = 0, Days = 0 };
            }

            int totalDays = (endDate - startDate).Days;
            int years = totalDays / 365;
            int months = (totalDays % 365) / 30;
            int days = (totalDays % 365) % 30;

            return new TenureResult
            {
                TotalDays = totalDays,
                Years = years,
                Months = months,
                Days = days
            };
        }
    }

    public class TenureInfo
    {
        public DateTime EffectiveDateFrom { get; set; }
        public DateTime? EffectiveDateTo { get; set; }
    }

    public class TenureResult
    {
        public int TotalDays { get; set; }
        public int Years { get; set; }
        public int Months { get; set; }
        public int Days { get; set; }
    }
}

