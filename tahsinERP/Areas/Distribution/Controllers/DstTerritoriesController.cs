using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Hududlar: viloyat → tuman → hudud. Agentlar va mijozlar hududga biriktiriladi.</summary>
    public class DstTerritoriesController : DstControllerBase
    {
        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                var usage = db.CustomerProfiles.Where(p => p.TerritoryID != null)
                    .GroupBy(p => p.TerritoryID.Value)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionary(x => x.Key, x => x.Count);

                var rows = db.Territories.AsNoTracking().ToList();
                ViewBag.Usage = usage;
                return View(TerritoryTree.Flatten(rows));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(int? id, int? parentId, string name, string nameRu)
        {
            return DstJson(() =>
            {
                name = name?.Trim();
                if (string.IsNullOrEmpty(name)) throw new DstValidationException(DstLocalization.T("Error_NameRequired"));

                using (var db = NewDb())
                {
                    Territory t;
                    if (id.HasValue)
                    {
                        t = db.Territories.Find(id.Value) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                        DirectoryRules.EnsureNoCycle(t.ID, parentId, x => db.Territories.Where(z => z.ID == x).Select(z => z.ParentID).FirstOrDefault());
                        byte level = DirectoryRules.TerritoryLevel(parentId, x => db.Territories.Find(x));
                        if (level != t.Level && db.Territories.Any(c => c.ParentID == t.ID))
                            throw new DstValidationException(DstLocalization.T("Error_TerritoryHasChildren"));
                        t.ParentID = parentId;
                        t.Level = level;
                    }
                    else
                    {
                        t = new Territory { ParentID = parentId, Level = DirectoryRules.TerritoryLevel(parentId, x => db.Territories.Find(x)) };
                        db.Territories.Add(t);
                    }
                    t.Name = name;
                    t.NameRu = string.IsNullOrWhiteSpace(nameRu) ? null : nameRu.Trim();
                    db.SaveChanges();
                    return new { t.ID };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Toggle(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var t = db.Territories.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    t.IsActive = !t.IsActive;
                    db.SaveChanges();
                    return new { t.IsActive };
                }
            });
        }

        /// <summary>O'chirish faqat bo'sh hududni (ostida hudud yoki mijoz yo'q); aks holda nofaol qiling.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Delete(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var t = db.Territories.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    if (db.Territories.Any(c => c.ParentID == id) || db.CustomerProfiles.Any(c => c.TerritoryID == id))
                        throw new DstValidationException(DstLocalization.T("Error_InUse"));
                    db.Territories.Remove(t);
                    db.SaveChanges();
                    return null;
                }
            });
        }
    }
}
