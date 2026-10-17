using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.HR
{
    /// <summary>
    /// HR spravochniklari (bo'lim, lavozim, tarif tarmog'i, ishbay operatsiyasi) uchun keshlangan dropdown manbalari (HRM-D09, HRM-P15).
    /// Avval har HR sahifasi va har validatsiya xatosida to'liq entity'lar bazadan qayta yuklanardi (85+ joy).
    ///
    /// Invalidatsiya: HR CRUD controllerlari saqlagandan keyin <see cref="Invalidate"/> chaqiradi;
    /// boshqa yo'l bilan o'zgarsa — <see cref="Ttl"/> ichida o'zi yangilanadi.
    /// Umumiy <see cref="ReferenceDataCache"/> bilan bir xil <see cref="RefItem"/> shaklidan foydalanadi.
    /// </summary>
    public static class HrLookups
    {
        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.HrLookups");
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

        public const string OrgUnits = "HrOrgUnits";
        public const string Positions = "HrPositions";
        public const string SalaryGrades = "HrSalaryGrades";
        public const string Operations = "HrOperations";

        private static readonly Dictionary<string, Func<DBTHSNEntities, List<RefItem>>> Loaders =
            new Dictionary<string, Func<DBTHSNEntities, List<RefItem>>>
            {
                { OrgUnits, db => db.HR_ORG_UNITS.AsNoTracking()
                    .Where(o => o.IsActive && o.IsDeleted != true)
                    .OrderBy(o => o.Name)
                    .Select(o => new RefItem { ID = o.ID, Name = o.Name, Extra = o.Code, ExtraId = o.ParentID }).ToList() },
                { Positions, db => db.HR_POSITIONS.AsNoTracking()
                    .Where(p => p.IsActive && p.IsDeleted != true)
                    .OrderBy(p => p.Name)
                    .Select(p => new RefItem { ID = p.ID, Name = p.Name, Extra = p.Code }).ToList() },
                { SalaryGrades, db => db.HR_SALARY_GRADES.AsNoTracking()
                    .Where(g => g.IsDeleted != true)
                    .OrderBy(g => g.GradeCode)
                    .Select(g => new RefItem { ID = g.ID, Name = g.GradeCode + " - " + g.Name, Extra = g.CurrencyCode }).ToList() },
                { Operations, db => db.HR_PIECE_RATE_OPERATIONS.AsNoTracking()
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
                    .Select(o => new RefItem { ID = o.ID, Name = o.Name, Extra = o.Code }).ToList() },
            };

        /// <summary>Keshlangan ro'yxat (o'zgartirmang — umumiy nusxa). Tartib: loader'dagi tartib.</summary>
        public static IReadOnlyList<RefItem> Get(string kind)
        {
            var cached = Cache.Get(kind) as List<RefItem>;
            if (cached != null) return cached;

            Func<DBTHSNEntities, List<RefItem>> loader;
            if (!Loaders.TryGetValue(kind, out loader))
                throw new ArgumentException("Noma'lum HR spravochnigi: " + kind, nameof(kind));

            List<RefItem> items;
            using (var db = new DBTHSNEntities())
            {
                items = loader(db);
            }
            Cache.Set(kind, items, DateTimeOffset.Now.Add(Ttl));
            return items;
        }

        /// <summary>Dropdown uchun SelectList (Value = ID, Text = Name).</summary>
        public static SelectList SelectList(string kind, object selectedValue = null)
        {
            return new SelectList(Get(kind), "ID", "Name", selectedValue);
        }

        public static string NameOf(string kind, int? id)
        {
            if (!id.HasValue) return null;
            return Get(kind).FirstOrDefault(x => x.ID == id.Value)?.Name;
        }

        public static void Invalidate(string kind)
        {
            Cache.Remove(kind);
        }
    }
}
