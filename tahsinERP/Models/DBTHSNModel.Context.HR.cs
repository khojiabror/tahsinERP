//------------------------------------------------------------------------------
// Partial DbContext extension for HR module tables
// Note: After running Scripts/HR_CreateTables.sql and updating DBTHSNModel.edmx,
// these DbSets should be auto-generated. This file is a temporary workaround.
//------------------------------------------------------------------------------

namespace tahsinERP.Models
{
    using System.Data.Entity;

    public partial class DBTHSNEntities
    {
        // These will be auto-generated after updating EDMX
        // For now, we'll add them manually if needed
        public virtual DbSet<HR_EMPLOYEES> HR_EMPLOYEES { get; set; }
        public virtual DbSet<HR_TIMESHEET_HEADERS> HR_TIMESHEET_HEADERS { get; set; }
        public virtual DbSet<HR_TIMESHEET_LINES> HR_TIMESHEET_LINES { get; set; }
        public virtual DbSet<HR_PAYROLL_HEADERS> HR_PAYROLL_HEADERS { get; set; }
        public virtual DbSet<HR_PAYROLL_LINES> HR_PAYROLL_LINES { get; set; }
        public virtual DbSet<HR_PIECE_RATE_TARIFFS> HR_PIECE_RATE_TARIFFS { get; set; }
        public virtual DbSet<HR_PIECE_RATE_OPERATIONS> HR_PIECE_RATE_OPERATIONS { get; set; }
        public virtual DbSet<HR_PIECE_RATE_WORK_ENTRIES> HR_PIECE_RATE_WORK_ENTRIES { get; set; }
        public virtual DbSet<HR_PIECE_RATE_TIMESHEET_APPROVALS> HR_PIECE_RATE_TIMESHEET_APPROVALS { get; set; }
    }
}

