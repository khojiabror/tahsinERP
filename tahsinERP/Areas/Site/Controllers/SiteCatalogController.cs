using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Site.Controllers
{
    /// <summary>
    /// Sayt katalogi: kategoriyalar va tovarlarni nashr qilish, tovar kontenti (nom, tavsif uz/ru, xususiyatlar, belgilar, SEO),
    /// fotogalereya (yuklash, tartib, eski PRODUCTIMAGES dan import). Tovarning o'zi, narx va qoldiq — ERP'dan.
    /// </summary>
    public class SiteCatalogController : SiteControllerBase
    {
        public sealed class AttributeInput
        {
            public string NameUz { get; set; }
            public string NameRu { get; set; }
            public string ValueUz { get; set; }
            public string ValueRu { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Categories = CategoryList(db);
                ViewBag.CategoryContent = (from c in db.Categories
                                           where c.IsDeleted != true
                                           join s in db.SiteCategories on c.ID equals s.CategoryID into sj
                                           from s in sj.DefaultIfEmpty()
                                           orderby c.Name
                                           select new CategoryRow
                                           {
                                               ID = c.ID, Name = c.Name, Content = s,
                                               Products = db.Products.Count(p => p.CategoryID == c.ID && !p.IsDeleted),
                                               Published = db.SiteProducts.Count(p => p.IsPublished && db.Products.Any(x => x.ID == p.ProductID && x.CategoryID == c.ID && !x.IsDeleted))
                                           }).ToList();
                return View();
            }
        }

        public sealed class CategoryRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public SiteCategoryContent Content { get; set; }
            public int Products { get; set; }
            public int Published { get; set; }
        }

        [HttpGet]
        public async Task<ActionResult> ProductsData(int? categoryId, string state)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 2, defaultDescending: false);
            using (var db = NewDb())
            {
                var q = from p in db.Products
                        where !p.IsDeleted
                        join c in db.SiteProducts on p.ID equals c.ProductID into cj
                        from c in cj.DefaultIfEmpty()
                        select new
                        {
                            p.ID, p.PNo, p.Name, p.CategoryID,
                            Category = db.Categories.Where(x => x.ID == p.CategoryID).Select(x => x.Name).FirstOrDefault(),
                            Published = c != null && c.IsPublished,
                            HasContent = c != null,
                            Slug = c.Slug,
                            Photos = db.SiteProductMedia.Count(m => m.ProductID == p.ID),
                            HasDescription = c != null && c.DescriptionUz != null && c.DescriptionUz != "",
                            Updated = (DateTime?)c.UpdatedAt
                        };
                if (categoryId.HasValue) q = q.Where(x => x.CategoryID == categoryId);
                switch (state)
                {
                    case "published": q = q.Where(x => x.Published); break;
                    case "unpublished": q = q.Where(x => !x.Published); break;
                    case "nophoto": q = q.Where(x => x.Published && x.Photos == 0); break;
                    case "nodesc": q = q.Where(x => x.Published && !x.HasDescription); break;
                }
                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.PNo.Contains(s) || r.Name.Contains(s) || r.Slug.Contains(s)),
                    (x, column, desc) =>
                    {
                        switch (column)
                        {
                            case 1: return ServerTablePage.By(x, r => r.PNo, desc).ThenBy(r => r.ID);
                            case 3: return ServerTablePage.By(x, r => r.Category, desc).ThenBy(r => r.ID);
                            case 4: return ServerTablePage.By(x, r => r.Published, desc).ThenBy(r => r.ID);
                            case 5: return ServerTablePage.By(x, r => r.Photos, desc).ThenBy(r => r.ID);
                            case 6: return ServerTablePage.By(x, r => r.Updated, desc).ThenBy(r => r.ID);
                            default: return ServerTablePage.By(x, r => r.Name, desc).ThenBy(r => r.ID);
                        }
                    });
                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, pno = r.PNo, name = r.Name, category = r.Category, published = r.Published, slug = r.Slug,
                        photos = r.Photos, hasDescription = r.HasDescription, updated = r.Updated?.ToString("dd.MM.yyyy HH:mm")
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ---------- nashr ----------

        /// <summary>Tovarlarni nashr qilish / olib tashlash (jadvaldan belgilanganlar).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SetPublished(int[] ids, bool publish)
        {
            return DstJson(() =>
            {
                if (ids == null || ids.Length == 0) return 0;
                using (var db = NewDb())
                {
                    int n = 0;
                    foreach (var id in ids.Distinct().Take(1000))
                    {
                        var content = publish ? EnsureContent(db, id) : db.SiteProducts.Find(id);
                        if (content == null || content.IsPublished == publish) continue;
                        content.IsPublished = publish;
                        content.UpdatedAt = Now;
                        n++;
                    }
                    db.SaveChanges();
                    Published();
                    return n;
                }
            });
        }

        /// <summary>Kategoriyadagi barcha tovarlar (va kategoriyaning o'zi) — bir bosishda.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult PublishCategory(int categoryId, bool publish)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var ids = db.Products.Where(p => p.CategoryID == categoryId && !p.IsDeleted).Select(p => p.ID).ToList();
                    int n = 0;
                    foreach (var id in ids)
                    {
                        var content = publish ? EnsureContent(db, id) : db.SiteProducts.Find(id);
                        if (content == null || content.IsPublished == publish) continue;
                        content.IsPublished = publish;
                        content.UpdatedAt = Now;
                        n++;
                    }
                    var cat = EnsureCategory(db, categoryId);
                    if (publish) cat.IsPublished = true;
                    cat.UpdatedAt = Now;
                    db.SaveChanges();
                    Published();
                    return n;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveCategory(int categoryId, bool isPublished, string slug, string nameUz, string nameRu, string descriptionUz,
            string descriptionRu, int sortOrder, string seoTitle, string seoDescription)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var c = EnsureCategory(db, categoryId);
                    var newSlug = NullIfEmpty(slug)?.ToLowerInvariant() ?? c.Slug;
                    if (!SiteText.IsValidSlug(newSlug)) throw new DstValidationException(DstLocalization.T("Site_SlugFormat"));
                    if (db.SiteCategories.Any(x => x.Slug == newSlug && x.CategoryID != categoryId)) throw new DstValidationException(DstLocalization.T("Site_SlugTaken"));
                    c.Slug = newSlug;
                    c.IsPublished = isPublished;
                    c.NameUz = SiteText.Clean(nameUz, 150);
                    c.NameRu = SiteText.Clean(nameRu, 150);
                    c.DescriptionUz = SiteText.Clean(descriptionUz, 2000);
                    c.DescriptionRu = SiteText.Clean(descriptionRu, 2000);
                    c.SortOrder = sortOrder;
                    c.SeoTitle = SiteText.Clean(seoTitle, 150);
                    c.SeoDescription = SiteText.Clean(seoDescription, 300);
                    c.UpdatedAt = Now;
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult UploadCategoryImage(int categoryId, HttpPostedFileBase file)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var c = EnsureCategory(db, categoryId);
                    var old = c.MediaID;
                    var media = SiteMediaService.Save(db, ReadUpload(file), CurrentUserId, Now);
                    c.MediaID = media.ID;
                    c.UpdatedAt = Now;
                    db.SaveChanges();
                    if (old.HasValue && old != media.ID) { SiteMediaService.DeleteIfUnused(db, old.Value); db.SaveChanges(); }
                    Published();
                    return new { id = media.ID };
                }
            });
        }

        // ---------- tovar kartasi ----------

        public ActionResult Edit(int id)
        {
            using (var db = NewDb())
            {
                var product = db.Products.FirstOrDefault(p => p.ID == id && !p.IsDeleted);
                if (product == null) return HttpNotFound();
                ViewBag.Product = product;
                ViewBag.Category = db.Categories.Where(c => c.ID == product.CategoryID).Select(c => c.Name).FirstOrDefault();
                ViewBag.NameRu = db.ProductProfiles.Where(p => p.ProductID == id).Select(p => p.NameRu).FirstOrDefault();
                ViewBag.Content = db.SiteProducts.Find(id);
                ViewBag.SuggestedSlug = SiteText.Slugify(product.Name);
                ViewBag.Attributes = db.SiteProductAttributes.Where(a => a.ProductID == id).OrderBy(a => a.SortOrder).ThenBy(a => a.ID).ToList();
                ViewBag.Gallery = (from m in db.SiteProductMedia
                                   join md in db.SiteMedia on m.MediaID equals md.ID
                                   where m.ProductID == id
                                   orderby m.SortOrder, m.MediaID
                                   select new GalleryItem { MediaID = md.ID, Width = md.Width, Height = md.Height, Bytes = md.Bytes, AltUz = md.AltUz, AltRu = md.AltRu }).ToList();
                ViewBag.LegacyImages = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM dbo.PRODUCTIMAGES WHERE ProdID = @p0 AND ISNULL(IsDeleted, 0) = 0", id).Single();
                ViewBag.SiteUrl = SiteUrl(db);
                return View();
            }
        }

        public sealed class GalleryItem
        {
            public int MediaID { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Bytes { get; set; }
            public string AltUz { get; set; }
            public string AltRu { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        [ValidateInput(false)]   // tavsif matnida "<" bo'lishi mumkin — saytda HTML sifatida emas, matn sifatida chiqadi
        public JsonResult SaveProduct(int productId, bool isPublished, string slug, string titleUz, string titleRu, string descriptionUz,
            string descriptionRu, string[] badges, int sortOrder, string seoTitle, string seoDescription, string attributes)
        {
            return DstJson(() =>
            {
                var attrs = string.IsNullOrWhiteSpace(attributes) ? new List<AttributeInput>()
                    : JsonConvert.DeserializeObject<List<AttributeInput>>(attributes) ?? new List<AttributeInput>();
                attrs = attrs.Where(a => !string.IsNullOrWhiteSpace(a?.NameUz) && !string.IsNullOrWhiteSpace(a.ValueUz)).Take(50).ToList();
                using (var db = NewDb())
                {
                    var c = EnsureContent(db, productId);
                    var newSlug = NullIfEmpty(slug)?.ToLowerInvariant() ?? c.Slug;
                    if (!SiteText.IsValidSlug(newSlug)) throw new DstValidationException(DstLocalization.T("Site_SlugFormat"));
                    if (db.SiteProducts.Any(x => x.Slug == newSlug && x.ProductID != productId)) throw new DstValidationException(DstLocalization.T("Site_SlugTaken"));
                    c.Slug = newSlug;
                    c.IsPublished = isPublished;
                    c.TitleUz = SiteText.Clean(titleUz, 200);
                    c.TitleRu = SiteText.Clean(titleRu, 200);
                    c.DescriptionUz = SiteText.Clean(descriptionUz, 20000);
                    c.DescriptionRu = SiteText.Clean(descriptionRu, 20000);
                    c.Badges = SiteText.NormalizeBadges(badges);
                    if (c.Badges == "") c.Badges = null;
                    c.SortOrder = sortOrder;
                    c.SeoTitle = SiteText.Clean(seoTitle, 150);
                    c.SeoDescription = SiteText.Clean(seoDescription, 300);
                    c.UpdatedAt = Now;

                    var existing = db.SiteProductAttributes.Where(a => a.ProductID == productId).ToList();
                    foreach (var a in existing) db.SiteProductAttributes.Remove(a);
                    int order = 0;
                    foreach (var a in attrs)
                        db.SiteProductAttributes.Add(new SiteProductAttribute
                        {
                            ProductID = productId, NameUz = SiteText.Clean(a.NameUz, 100), NameRu = SiteText.Clean(a.NameRu, 100),
                            ValueUz = SiteText.Clean(a.ValueUz, 200), ValueRu = SiteText.Clean(a.ValueRu, 200), SortOrder = order++
                        });
                    db.SaveChanges();
                    Published();
                    return new { slug = c.Slug };
                }
            });
        }

        // ---------- galereya ----------

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult UploadImages(int productId, IEnumerable<HttpPostedFileBase> files)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    EnsureContent(db, productId);
                    db.SaveChanges();
                    int order = db.SiteProductMedia.Where(m => m.ProductID == productId).Select(m => (int?)m.SortOrder).Max() ?? -1;
                    int added = 0;
                    foreach (var file in (files ?? Enumerable.Empty<HttpPostedFileBase>()).Where(f => f != null).Take(20))
                    {
                        var media = SiteMediaService.Save(db, ReadUpload(file), CurrentUserId, Now);
                        if (db.SiteProductMedia.Any(m => m.ProductID == productId && m.MediaID == media.ID)) continue;
                        db.SiteProductMedia.Add(new SiteProductMedia { ProductID = productId, MediaID = media.ID, SortOrder = ++order });
                        db.SaveChanges();
                        added++;
                    }
                    Touch(db, productId);
                    return added;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult ImportImages(int productId)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    EnsureContent(db, productId);
                    db.SaveChanges();
                    var n = SiteMediaService.ImportProductImages(db, productId, CurrentUserId, Now);
                    Touch(db, productId);
                    return n;
                }
            });
        }

        /// <summary>Nashr qilingan, fotosi yo'q tovarlarga eski PRODUCTIMAGES rasmlarini ko'chirish.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult ImportAllImages()
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var ids = db.SiteProducts.Where(p => p.IsPublished && !db.SiteProductMedia.Any(m => m.ProductID == p.ProductID))
                        .Select(p => p.ProductID).ToList();
                    int n = 0;
                    foreach (var id in ids) n += SiteMediaService.ImportProductImages(db, id, CurrentUserId, Now);
                    Published();
                    return n;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult DeleteImage(int productId, int mediaId)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var link = db.SiteProductMedia.Find(productId, mediaId) ?? throw NotFound();
                    db.SiteProductMedia.Remove(link);
                    db.SaveChanges();
                    SiteMediaService.DeleteIfUnused(db, mediaId);
                    Touch(db, productId);
                    return null;
                }
            });
        }

        /// <summary>Galereya tartibi: berilgan ketma-ketlik (birinchisi — asosiy rasm).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult ReorderImages(int productId, int[] mediaIds)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var links = db.SiteProductMedia.Where(m => m.ProductID == productId).ToList();
                    var order = (mediaIds ?? new int[0]).ToList();
                    foreach (var l in links)
                    {
                        int i = order.IndexOf(l.MediaID);
                        l.SortOrder = i < 0 ? 1000 + l.SortOrder : i;
                    }
                    Touch(db, productId);
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveImageAlt(int mediaId, string altUz, string altRu)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var m = db.SiteMedia.Find(mediaId) ?? throw NotFound();
                    m.AltUz = SiteText.Clean(altUz, 200);
                    m.AltRu = SiteText.Clean(altRu, 200);
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }

        /// <summary>Admin panel uchun rasm (ruxsat bilan). Ochiq sayt rasmlarni SiteApi orqali oladi.</summary>
        [HttpGet]
        public ActionResult Image(int id)
        {
            using (var db = NewDb())
            {
                var m = db.SiteMedia.Find(id);
                if (m == null) return HttpNotFound();
                Response.Cache.SetCacheability(HttpCacheability.Private);
                Response.Cache.SetMaxAge(TimeSpan.FromDays(7));
                return File(m.Content, m.ContentType);
            }
        }

        // ---------- yordamchilar ----------

        /// <summary>Tovar kontenti bo'lmasa — yaratadi (nom bo'yicha slug, nashr qilinmagan).</summary>
        internal static SiteProductContent EnsureContent(DistributionDbContext db, int productId)
        {
            var c = db.SiteProducts.Find(productId);
            if (c != null) return c;
            var name = db.Products.Where(p => p.ID == productId && !p.IsDeleted).Select(p => new { p.Name, p.PNo }).FirstOrDefault()
                       ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var local = db.SiteProducts.Local.Select(x => x.Slug).ToList();
            var slug = SiteText.Unique(SiteText.Slugify(name.Name), s => local.Contains(s) || db.SiteProducts.Any(x => x.Slug == s), "p-" + productId);
            c = new SiteProductContent { ProductID = productId, Slug = slug, IsPublished = false, UpdatedAt = DateTime.Now };
            db.SiteProducts.Add(c);
            return c;
        }

        internal static SiteCategoryContent EnsureCategory(DistributionDbContext db, int categoryId)
        {
            var c = db.SiteCategories.Find(categoryId);
            if (c != null) return c;
            var name = db.Categories.Where(x => x.ID == categoryId && x.IsDeleted != true).Select(x => x.Name).FirstOrDefault()
                       ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var slug = SiteText.Unique(SiteText.Slugify(name), s => db.SiteCategories.Any(x => x.Slug == s), "c-" + categoryId);
            c = new SiteCategoryContent { CategoryID = categoryId, Slug = slug, IsPublished = false, UpdatedAt = DateTime.Now };
            db.SiteCategories.Add(c);
            return c;
        }

        private void Touch(DistributionDbContext db, int productId)
        {
            var c = db.SiteProducts.Find(productId);
            if (c != null) c.UpdatedAt = Now;
            db.SaveChanges();
            Published();
        }
    }
}
