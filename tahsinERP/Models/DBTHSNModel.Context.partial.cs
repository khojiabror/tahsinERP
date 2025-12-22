//------------------------------------------------------------------------------
// Partial extension for DBTHSNEntities to add Quality Inspection DbSet
//------------------------------------------------------------------------------

namespace tahsinERP.Models
{
    using System.Data.Entity;
    
    public partial class DBTHSNEntities : DbContext
    {
        public virtual DbSet<QUALITY_INSPECTION> QUALITY_INSPECTION { get; set; }
        public virtual DbSet<TANNARX> TANNARXes { get; set; }
    }
}

