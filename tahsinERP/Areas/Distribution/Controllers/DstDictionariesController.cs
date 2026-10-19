using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Oddiy ma'lumotnomalar bitta sahifada: brendlar, ishlab chiqaruvchilar, subkategoriyalar, narx turlari, sabablar.
    /// Ishlatilgan yozuv o'chirilmaydi — nofaol qilinadi.
    /// </summary>
    public class DstDictionariesController : DstControllerBase
    {
        public const string Brands = "brands";
        public const string Manufacturers = "manufacturers";
        public const string SubCategories = "subcategories";
        public const string PriceTypes = "pricetypes";
        public const string Reasons = "reasons";

        public static readonly string[] Kinds = { Brands, Manufacturers, SubCategories, PriceTypes, Reasons };

        /// <summary>Ro'yxat qatori (barcha turlar uchun umumiy shakl).</summary>
        public sealed class Row
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public string NameRu { get; set; }
            public string Extra { get; set; }      // subkategoriya: kategoriya; sabab: tur; ishlab chiqaruvchi: davlat
            public string ExtraValue { get; set; }
            public bool IsActive { get; set; }
            public bool IsDefault { get; set; }
            public int SortOrder { get; set; }
        }

        public ActionResult Index(string kind)
        {
            kind = Kinds.Contains(kind) ? kind : Brands;
            using (var db = NewDb())
            {
                ViewBag.Kind = kind;
                ViewBag.Categories = db.Categories.Where(c => c.IsDeleted != true && c.IsActive)
                    .OrderBy(c => c.Name).Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }).ToList();
                return View(Load(db, kind));
            }
        }

        private static List<Row> Load(DistributionDbContext db, string kind)
        {
            switch (kind)
            {
                case Manufacturers:
                    return db.Manufacturers.OrderBy(x => x.Name).Select(x => new Row
                    { ID = x.ID, Name = x.Name, Extra = x.Country, ExtraValue = x.Country, IsActive = x.IsActive }).ToList();
                case SubCategories:
                    return (from s in db.SubCategories
                            join c in db.Categories on s.CategoryID equals c.ID
                            orderby c.Name, s.Name
                            select new Row
                            {
                                ID = s.ID, Name = s.Name, NameRu = s.NameRu, Extra = c.Name,
                                ExtraValue = s.CategoryID.ToString(), IsActive = s.IsActive
                            }).ToList();
                case PriceTypes:
                    return db.PriceTypes.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new Row
                    {
                        ID = x.ID, Name = x.Name, NameRu = x.NameRu, IsActive = x.IsActive, IsDefault = x.IsDefault, SortOrder = x.SortOrder
                    }).ToList();
                case Reasons:
                    return db.Reasons.OrderBy(x => x.Type).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new Row
                    {
                        ID = x.ID, Name = x.Name, NameRu = x.NameRu, Extra = x.Type, ExtraValue = x.Type,
                        IsActive = x.IsActive, SortOrder = x.SortOrder
                    }).ToList();
                default:
                    return db.Brands.OrderBy(x => x.Name).Select(x => new Row { ID = x.ID, Name = x.Name, IsActive = x.IsActive }).ToList();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(string kind, int? id, string name, string nameRu, string extra, int? sortOrder)
        {
            return DstJson(() =>
            {
                name = name?.Trim();
                nameRu = string.IsNullOrWhiteSpace(nameRu) ? null : nameRu.Trim();
                extra = string.IsNullOrWhiteSpace(extra) ? null : extra.Trim();
                if (string.IsNullOrEmpty(name)) throw new DstValidationException(DstLocalization.T("Error_NameRequired"));

                using (var db = NewDb())
                {
                    switch (kind)
                    {
                        case Brands:
                            Upsert(db.Brands, id, x => x.Name = name);
                            break;
                        case Manufacturers:
                            Upsert(db.Manufacturers, id, x => { x.Name = name; x.Country = extra; });
                            break;
                        case SubCategories:
                            if (!int.TryParse(extra, out int categoryId) || !db.Categories.Any(c => c.ID == categoryId))
                                throw new DstValidationException(DstLocalization.T("Error_CategoryRequired"));
                            Upsert(db.SubCategories, id, x => { x.Name = name; x.NameRu = nameRu; x.CategoryID = categoryId; });
                            break;
                        case PriceTypes:
                            Upsert(db.PriceTypes, id, x => { x.Name = name; x.NameRu = nameRu; x.SortOrder = sortOrder ?? 0; });
                            break;
                        case Reasons:
                            if (!ReasonTypes.All.Contains(extra)) throw new DstValidationException(DstLocalization.T("Error_ReasonType"));
                            Upsert(db.Reasons, id, x => { x.Name = name; x.NameRu = nameRu; x.Type = extra; x.SortOrder = sortOrder ?? 0; });
                            break;
                        default:
                            throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    }
                    db.SaveChanges();
                    return null;
                }
            });
        }

        private static void Upsert<T>(System.Data.Entity.DbSet<T> set, int? id, Action<T> apply) where T : class, new()
        {
            T entity;
            if (id.HasValue)
                entity = set.Find(id.Value) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            else
                set.Add(entity = new T());
            apply(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Toggle(string kind, int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    switch (kind)
                    {
                        case Brands: { var x = Find(db.Brands, id); x.IsActive = !x.IsActive; break; }
                        case Manufacturers: { var x = Find(db.Manufacturers, id); x.IsActive = !x.IsActive; break; }
                        case SubCategories: { var x = Find(db.SubCategories, id); x.IsActive = !x.IsActive; break; }
                        case Reasons: { var x = Find(db.Reasons, id); x.IsActive = !x.IsActive; break; }
                        case PriceTypes:
                            {
                                var x = Find(db.PriceTypes, id);
                                if (x.IsDefault && x.IsActive) throw new DstValidationException(DstLocalization.T("Error_DefaultPriceTypeInactive"));
                                x.IsActive = !x.IsActive;
                                break;
                            }
                        default: throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    }
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Standart narx turi — narx turi berilmagan mijozlar uchun (bittagina).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SetDefaultPriceType(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                using (var tx = db.Database.BeginTransaction())
                {
                    var target = Find(db.PriceTypes, id);
                    if (!target.IsActive) throw new DstValidationException(DstLocalization.T("Error_DefaultPriceTypeInactive"));
                    foreach (var t in db.PriceTypes.Where(t => t.IsDefault && t.ID != id))
                        t.IsDefault = false;
                    db.SaveChanges();   // UNIQUE (IsDefault=1) indeksi: avval eskisini o'chiramiz
                    target.IsDefault = true;
                    db.SaveChanges();
                    tx.Commit();
                    return null;
                }
            });
        }

        private static T Find<T>(System.Data.Entity.DbSet<T> set, int id) where T : class
        {
            return set.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }
    }
}
