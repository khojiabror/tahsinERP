using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
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
        }

        public void EnsureTable()
        {
            _db.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.HR_EMPLOYEE_ALLOWANCES', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HR_EMPLOYEE_ALLOWANCES
    (
        EmployeeID INT NOT NULL PRIMARY KEY,
        MaterialResponsibilityPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_HR_EMP_ALLOW_Material DEFAULT(0),
        SpecialAllowancePercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_HR_EMP_ALLOW_Special DEFAULT(0),
        CreatedDT DATETIME NOT NULL CONSTRAINT DF_HR_EMP_ALLOW_Created DEFAULT(GETDATE()),
        UpdatedDT DATETIME NULL,
        CONSTRAINT FK_HR_EMP_ALLOW_EMP FOREIGN KEY (EmployeeID) REFERENCES dbo.HR_EMPLOYEES(ID)
    );
END");
        }

        public EmployeeAllowanceRow GetByEmployeeId(int employeeId)
        {
            EnsureTable();

            return _db.Database.SqlQuery<EmployeeAllowanceRow>(@"
SELECT EmployeeID,
       MaterialResponsibilityPercent,
       SpecialAllowancePercent
FROM dbo.HR_EMPLOYEE_ALLOWANCES
WHERE EmployeeID = @employeeId",
                new SqlParameter("@employeeId", employeeId)).FirstOrDefault();
        }

        public Dictionary<int, EmployeeAllowanceRow> GetByEmployeeIds(IEnumerable<int> employeeIds)
        {
            EnsureTable();

            var ids = employeeIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
            {
                return new Dictionary<int, EmployeeAllowanceRow>();
            }

            var valuesSql = string.Join(",", ids.Select((id, i) => $"(@id{i})"));
            var parameters = ids.Select((id, i) => new SqlParameter($"@id{i}", id)).ToArray();
            var sql = $@"
SELECT EmployeeID,
       MaterialResponsibilityPercent,
       SpecialAllowancePercent
FROM dbo.HR_EMPLOYEE_ALLOWANCES
WHERE EmployeeID IN ({valuesSql})";

            return _db.Database.SqlQuery<EmployeeAllowanceRow>(sql, parameters)
                .ToDictionary(x => x.EmployeeID, x => x);
        }

        public void Upsert(int employeeId, decimal materialResponsibilityPercent, decimal specialAllowancePercent)
        {
            EnsureTable();

            _db.Database.ExecuteSqlCommand(@"
MERGE dbo.HR_EMPLOYEE_ALLOWANCES AS target
USING (SELECT @employeeId AS EmployeeID) AS source
ON target.EmployeeID = source.EmployeeID
WHEN MATCHED THEN
    UPDATE SET MaterialResponsibilityPercent = @materialResponsibilityPercent,
               SpecialAllowancePercent = @specialAllowancePercent,
               UpdatedDT = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (EmployeeID, MaterialResponsibilityPercent, SpecialAllowancePercent, CreatedDT, UpdatedDT)
    VALUES (@employeeId, @materialResponsibilityPercent, @specialAllowancePercent, GETDATE(), GETDATE());",
                new SqlParameter("@employeeId", employeeId),
                new SqlParameter("@materialResponsibilityPercent", materialResponsibilityPercent),
                new SqlParameter("@specialAllowancePercent", specialAllowancePercent));
        }
    }
}
