//------------------------------------------------------------------------------
// Partial extension for DBTHSNEntities to add Purchase Requisition DbSets
//------------------------------------------------------------------------------

namespace tahsinERP.Models
{
    using System.Data.Entity;
    
    public partial class DBTHSNEntities : DbContext
    {
        public virtual DbSet<P_REQUISITIONS> P_REQUISITIONS { get; set; }
        public virtual DbSet<P_REQUISITION_PRODUCTS> P_REQUISITION_PRODUCTS { get; set; }
        public virtual DbSet<P_REQUISITION_APPROVALS> P_REQUISITION_APPROVALS { get; set; }
    }
}
