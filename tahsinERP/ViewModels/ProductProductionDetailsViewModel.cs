using System;
using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Details sahifasi uchun: BOM/Norm sarf va chiqim hujjatlari.
    /// </summary>
    public class ProductProductionDetailsViewModel
    {
        public PRODUCT_PRODUCTION Production { get; set; }
        /// <summary>BOM/Norm bo'yicha hisoblangan sarf qatorlari (material, talab, chiqindi, jami).</summary>
        public List<BomConsumptionLineViewModel> BomConsumptionLines { get; set; }
        /// <summary>Ushbu ishlab chiqarishga bog'liq chiqim hujjatlari (BOM chiqimi).</summary>
        public List<ProductionExpenseLinkViewModel> ExpenseDocuments { get; set; }

        public ProductProductionDetailsViewModel()
        {
            BomConsumptionLines = new List<BomConsumptionLineViewModel>();
            ExpenseDocuments = new List<ProductionExpenseLinkViewModel>();
        }
    }

    public class BomConsumptionLineViewModel
    {
        public int MaterialID { get; set; }
        public string MaterialPNo { get; set; }
        public string MaterialName { get; set; }
        public double RequiredQtyPieces { get; set; }
        public double TotalConsumption { get; set; }
        public string UnitName { get; set; }
        public bool TotalConsumptionIsInKg { get; set; }
        /// <summary>Dona narxi (chiqim hujjatidan).</summary>
        public double PiecePrice { get; set; }
        /// <summary>Jami summa (chiqim hujjatidan).</summary>
        public double TotalPrice { get; set; }
    }

    public class ProductionExpenseLinkViewModel
    {
        public int ExpenseID { get; set; }
        public string DocNo { get; set; }
        public DateTime IssueDateTime { get; set; }
        public double Amount { get; set; }
        public double TotalPrice { get; set; }
        public int ProductCount { get; set; }
        /// <summary>Chiqim sahifasiga to'g'ridan-to'g'ri havola (URL).</summary>
        public string DetailsUrl { get; set; }
    }
}
