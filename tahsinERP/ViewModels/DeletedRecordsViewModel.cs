using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// O'chirilgan ma'lumotlarni tiklash moduli uchun ViewModels
    /// </summary>
    public class DeletedModuleInfo
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
    }

    public class DeletedRecordItem
    {
        public int Id { get; set; }
        public string DisplayText { get; set; }
        public Dictionary<string, object> Columns { get; set; }
    }
}
