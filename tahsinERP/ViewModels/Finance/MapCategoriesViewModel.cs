using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.Finance
{
    /// <summary>
    /// Balans kategoriyasi yoki P&amp;L elementiga tranzaksiya kategoriyalarini bog'lash sahifasi (FIN-D16:
    /// ikki controller uchun bitta view — Views/FinPlItems/MapCategories.cshtml).
    /// </summary>
    public class MapCategoriesViewModel
    {
        public int ID { get; set; }
        public string Name { get; set; }
        /// <summary>Forma yuboriladigan controller (FinBalanceCategories / FinPlItems).</summary>
        public string Controller { get; set; }
        /// <summary>Breadcrumb'dagi ro'yxat nomi.</summary>
        public string ListTitle { get; set; }
        public List<FIN_CATEGORIES> AllCategories { get; set; }
        public List<int> MappedCategoryIds { get; set; }
    }
}
