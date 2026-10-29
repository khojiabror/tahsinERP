using System;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>Mijoz kartasi: dbo.CUSTOMERS asosiy maydonlari + dst.CUSTOMER_PROFILES.</summary>
    [System.Web.Mvc.ModelBinder(typeof(DstModelBinder))]
    public class CustomerInput
    {
        public int? ID { get; set; }

        // dbo.CUSTOMERS
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Telephone { get; set; }
        public string ContactPersonName { get; set; }

        // dst.CUSTOMER_PROFILES
        public string INN { get; set; }
        public string Phone2 { get; set; }
        public string OwnerName { get; set; }
        public string Landmark { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public string PointType { get; set; }
        public string Category { get; set; }
        public int? TerritoryID { get; set; }
        public int? AgentUserID { get; set; }
        public int? PriceTypeID { get; set; }
        public byte VisitDays { get; set; }
        public decimal CreditLimit { get; set; }
        public int PaymentDays { get; set; }
    }

    /// <summary>
    /// Mijozni yaratish/tahrirlash — web, Excel import va (keyin) bot bir xil yo'ldan. Chaqiruvchi
    /// <see cref="DistributionUnitOfWork"/> ochadi va Commit qiladi (CUSTOMERS + profil bitta tranzaksiyada).
    /// Agent qo'shgan yangi nuqta tasdiqlanmagan (IsApproved = false) bo'ladi.
    /// </summary>
    public static class CustomerService
    {
        public const int NameMaxLength = 50;     // dbo.CUSTOMERS.Name varchar(50)

        public static int Save(DistributionUnitOfWork uow, CustomerInput input, int? userId, string via, DateTime now)
        {
            Validate(input, uow.Dst);

            CUSTOMER customer;
            if (input.ID.HasValue)
            {
                customer = uow.Erp.CUSTOMERS.Find(input.ID.Value);
                if (customer == null || customer.IsDeleted == true)
                    throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            }
            else
            {
                customer = new CUSTOMER { IsDeleted = false, Type = "DST" };
                uow.Erp.CUSTOMERS.Add(customer);
            }

            customer.Name = input.Name.Trim();
            customer.Address = Clean(input.Address);
            customer.City = Truncate(Clean(input.City), 50);
            customer.Telephone = DirectoryRules.NormalizePhone(input.Telephone);
            customer.ContactPersonName = Truncate(Clean(input.ContactPersonName), 30);
            customer.DUNS = DirectoryRules.NormalizeInn(input.INN);   // eski hujjatlar (SF) STIR'ni shu ustundan oladi
            uow.Erp.SaveChanges();

            var profile = uow.Dst.CustomerProfiles.Find(customer.ID);
            if (profile == null)
            {
                bool byAgent = via == AuditActor.Bot || via == AuditActor.Shop;   // agent yoki mijozning o'zi — operator tasdiqlaydi
                profile = new CustomerProfile
                {
                    CustomerID = customer.ID, CreatedAt = now, CreatedBy = userId, CreatedVia = via ?? AuditActor.Web,
                    IsApproved = !byAgent,
                    ApprovedBy = byAgent ? null : userId,
                    ApprovedAt = byAgent ? (DateTime?)null : now
                };
                uow.Dst.CustomerProfiles.Add(profile);
            }

            profile.INN = customer.DUNS;
            profile.Phone2 = DirectoryRules.NormalizePhone(input.Phone2);
            profile.OwnerName = Truncate(Clean(input.OwnerName), 100);
            profile.Landmark = Truncate(Clean(input.Landmark), 200);
            profile.Lat = input.Lat;
            profile.Lng = input.Lng;
            profile.PointType = PointTypes.All.Contains(input.PointType) ? input.PointType : PointTypes.Shop;
            profile.Category = DirectoryRules.NormalizeCategory(input.Category);
            profile.TerritoryID = input.TerritoryID;
            profile.AgentUserID = input.AgentUserID;
            profile.PriceTypeID = input.PriceTypeID;
            profile.VisitDays = (byte)(input.VisitDays & 0x7F);
            profile.CreditLimit = input.CreditLimit;
            profile.PaymentDays = input.PaymentDays;
            uow.Dst.SaveChanges();
            return customer.ID;
        }

        public static void Approve(DistributionDbContext db, int customerId, int userId, DateTime now)
        {
            var p = db.CustomerProfiles.Find(customerId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (p.IsApproved) return;
            p.IsApproved = true;
            p.ApprovedBy = userId;
            p.ApprovedAt = now;
        }

        private static void Validate(CustomerInput input, DistributionDbContext db)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new DstValidationException(DstLocalization.T("Error_NameRequired"));
            if (input.Name.Trim().Length > NameMaxLength)
                throw new DstValidationException(DstLocalization.T("Error_NameTooLong", NameMaxLength));

            DirectoryRules.ValidateGeo(input.Lat, input.Lng);
            DirectoryRules.ValidateCredit(input.CreditLimit, input.PaymentDays);

            if (input.TerritoryID.HasValue && !db.Territories.Any(t => t.ID == input.TerritoryID && t.IsActive))
                throw new DstValidationException(DstLocalization.T("Error_TerritoryParent"));
            if (input.PriceTypeID.HasValue && !db.PriceTypes.Any(t => t.ID == input.PriceTypeID && t.IsActive))
                throw new DstValidationException(DstLocalization.T("Error_PriceTypeRequired"));
            if (input.AgentUserID.HasValue && !db.SalesAgents.Any(a => a.UserID == input.AgentUserID && a.IsActive && !a.IsExpeditor))
                throw new DstValidationException(DstLocalization.T("Error_AgentRequired"));

            var inn = DirectoryRules.NormalizeInn(input.INN);
            if (inn != null && db.CustomerProfiles.Any(p => p.INN == inn && p.CustomerID != (input.ID ?? 0)))
                throw new DstValidationException(DstLocalization.T("Error_InnDuplicate"));
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Truncate(string value, int max) => value == null || value.Length <= max ? value : value.Substring(0, max);
    }
}
