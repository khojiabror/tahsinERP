using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>
    /// Mavjud dbo jadvallarining distributsiya kontekstidagi FAQAT O'QISH uchun ko'rinishi —
    /// profil jadvallari bilan bitta so'rovda join qilish uchun. Yozish EDMX (DBTHSNEntities) orqali;
    /// <see cref="DistributionDbContext"/> SaveChanges bularni o'zgartirishga yo'l qo'ymaydi.
    /// </summary>
    public interface IReadOnlyRef
    {
    }

    [Table("CUSTOMERS", Schema = "dbo")]
    public class CustomerRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string ContactPersonName { get; set; }
        public string DirectorName { get; set; }
        public string DUNS { get; set; }
        public bool? IsDeleted { get; set; }
    }

    [Table("PRODUCTS", Schema = "dbo")]
    public class ProductRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string PNo { get; set; }
        public string Name { get; set; }
        public int UnitID { get; set; }
        public int? CategoryID { get; set; }
        // Weight bu yerda yo'q: dbo.PRODUCTS.Weight float -> decimal ga o'tkazilmoqda (Audit DB_FLOAT_DECIMAL_PLAN);
        // distributsiyada og'irlik dst.PRODUCT_PROFILES.BaseWeightKg da
        public bool IsDeleted { get; set; }
    }

    [Table("UNITS", Schema = "dbo")]
    public class UnitRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string UnitName { get; set; }
        public string ShortName { get; set; }
        public bool? IsDeleted { get; set; }
    }

    [Table("PRODUCT_CATEGORIES", Schema = "dbo")]
    public class CategoryRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public bool? IsDeleted { get; set; }
    }

    [Table("USERS", Schema = "dbo")]
    public class UserRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string Uname { get; set; }
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public bool? IsDeleted { get; set; }
    }

    [Table("WAREHOUSES", Schema = "dbo")]
    public class WarehouseRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string WHName { get; set; }
        public string WarehouseType { get; set; }
        public int? MRP { get; set; }
        public bool? IsDeleted { get; set; }
    }

    /// <summary>dbo.PRODUCT_STOCKS — haqiqiy qoldiq (ombor ledgeri yozadi).</summary>
    [Table("PRODUCT_STOCKS", Schema = "dbo")]
    public class StockRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public int WHID { get; set; }
        public int ProductID { get; set; }
        public decimal Amount { get; set; }
        public int UnitID { get; set; }
    }

    [Table("ROLES", Schema = "dbo")]
    public class RoleRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string RName { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
