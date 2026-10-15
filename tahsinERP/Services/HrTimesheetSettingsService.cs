using System;
using System.Data.SqlClient;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    public sealed class HrTimesheetSettingsService
    {
        private const decimal DefaultFundHours = 160m;
        private readonly DBTHSNEntities _db;

        public HrTimesheetSettingsService(DBTHSNEntities db)
        {
            _db = db;
        }

        public sealed class TimesheetSettingRow
        {
            public int TimesheetID { get; set; }
            public decimal FundHours { get; set; }
        }

        public void EnsureTable()
        {
            _db.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.HR_TIMESHEET_SETTINGS', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HR_TIMESHEET_SETTINGS
    (
        TimesheetID INT NOT NULL PRIMARY KEY,
        FundHours DECIMAL(10,2) NOT NULL CONSTRAINT DF_HR_TIMESHEET_SETTINGS_FundHours DEFAULT(160),
        CreatedDT DATETIME NOT NULL CONSTRAINT DF_HR_TIMESHEET_SETTINGS_Created DEFAULT(GETDATE()),
        UpdatedDT DATETIME NULL,
        CONSTRAINT FK_HR_TIMESHEET_SETTINGS_HEADER FOREIGN KEY (TimesheetID) REFERENCES dbo.HR_TIMESHEET_HEADERS(ID)
    );
END");
        }

        public TimesheetSettingRow GetByTimesheetId(int timesheetId)
        {
            EnsureTable();
            return _db.Database.SqlQuery<TimesheetSettingRow>(@"
SELECT TimesheetID, FundHours
FROM dbo.HR_TIMESHEET_SETTINGS
WHERE TimesheetID = @timesheetId",
                new SqlParameter("@timesheetId", timesheetId)).FirstOrDefault();
        }

        public decimal GetFundHoursOrDefault(int timesheetId)
        {
            var row = GetByTimesheetId(timesheetId);
            if (row == null || row.FundHours <= 0)
            {
                return DefaultFundHours;
            }

            return row.FundHours;
        }

        public void UpsertFundHours(int timesheetId, decimal fundHours)
        {
            EnsureTable();
            var normalizedFundHours = fundHours > 0 ? fundHours : DefaultFundHours;

            _db.Database.ExecuteSqlCommand(@"
MERGE dbo.HR_TIMESHEET_SETTINGS AS target
USING (SELECT @timesheetId AS TimesheetID) AS source
ON target.TimesheetID = source.TimesheetID
WHEN MATCHED THEN
    UPDATE SET FundHours = @fundHours,
               UpdatedDT = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (TimesheetID, FundHours, CreatedDT, UpdatedDT)
    VALUES (@timesheetId, @fundHours, GETDATE(), GETDATE());",
                new SqlParameter("@timesheetId", timesheetId),
                new SqlParameter("@fundHours", normalizedFundHours));
        }
    }
}
