using System.Collections.Generic;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.ViewModels
{
    public class AssignDlocPageViewModel
    {
        public int? WarehouseId { get; set; }
        public List<UnassignedDlocStockRow> Rows { get; set; } = new List<UnassignedDlocStockRow>();
    }

    public class AssignDlocPostViewModel
    {
        public int WarehouseId { get; set; }
        public List<AssignDlocPostLine> Lines { get; set; } = new List<AssignDlocPostLine>();
    }

    public class AssignDlocPostLine
    {
        public int ProductID { get; set; }
        public int LocationID { get; set; }
        public double AssignQty { get; set; }
        public bool Selected { get; set; }
    }
}
