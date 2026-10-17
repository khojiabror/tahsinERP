using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// FinCategories, FinBalanceCategories va FinPlItems daraxtlari uchun umumiy amallar (FIN-D15, FIN-D16):
    /// daraxt qurish, avlodlar ro'yxati (FIN-P16: avval har tugun uchun alohida so'rov, rekursiv)
    /// va tranzaksiya kategoriyalarini bog'lash.
    /// </summary>
    public static class FinCategoryTree
    {
        /// <summary>
        /// Tekis ro'yxatdan daraxt. Bolalar tartibi — kirish ro'yxatidagi tartib (chaqiruvchi SortOrder, Name bo'yicha saralaydi).
        /// </summary>
        public static List<TItem> Build<T, TItem>(IList<T> all, Func<T, int> id, Func<T, int?> parent, int? rootId,
            Func<T, List<TItem>, TItem> make)
        {
            var byParent = all.ToLookup(x => parent(x) ?? 0);
            var visited = new HashSet<int>();

            List<TItem> Children(int? parentId)
            {
                var result = new List<TItem>();
                foreach (var node in byParent[parentId ?? 0])
                {
                    if (!visited.Add(id(node))) continue; // aylana (circular parent) bo'lsa — cheksiz rekursiya yo'q
                    result.Add(make(node, Children(id(node))));
                }
                return result;
            }

            return Children(rootId);
        }

        /// <summary>
        /// Berilgan tugunning barcha avlodlari (o'zisiz). (ID, ParentID) juftliklari bir marta yuklanadi,
        /// daraxt xotirada aylanib chiqiladi.
        /// </summary>
        public static List<int> Descendants(IEnumerable<KeyValuePair<int, int?>> nodes, int rootId)
        {
            var byParent = nodes.Where(n => n.Value.HasValue).ToLookup(n => n.Value.Value, n => n.Key);
            var result = new List<int>();
            var seen = new HashSet<int> { rootId };
            var stack = new Stack<int>();
            stack.Push(rootId);
            while (stack.Count > 0)
            {
                foreach (var child in byParent[stack.Pop()])
                {
                    if (!seen.Add(child)) continue;
                    result.Add(child);
                    stack.Push(child);
                }
            }
            return result;
        }

        public static List<int> BalanceCategoryDescendants(DBTHSNEntities db, int id)
        {
            return Descendants(db.FIN_BALANCE_CATEGORIES.AsNoTracking().Where(c => c.IsActive)
                .Select(c => new { c.ID, c.ParentID }).ToList()
                .Select(c => new KeyValuePair<int, int?>(c.ID, c.ParentID)), id);
        }

        public static List<int> CategoryDescendants(DBTHSNEntities db, int id)
        {
            return Descendants(db.FIN_CATEGORIES.AsNoTracking().Where(c => c.IsActive)
                .Select(c => new { c.ID, c.ParentID }).ToList()
                .Select(c => new KeyValuePair<int, int?>(c.ID, c.ParentID)), id);
        }

        public static List<int> PlItemDescendants(DBTHSNEntities db, int id)
        {
            return Descendants(db.FIN_PL_ITEMS.AsNoTracking().Where(i => i.IsActive)
                .Select(i => new { i.ID, i.ParentID }).ToList()
                .Select(i => new KeyValuePair<int, int?>(i.ID, i.ParentID)), id);
        }

        /// <summary>Bog'langan tranzaksiya kategoriyalarini <paramref name="categoryIds"/> bilan almashtiradi (SaveChanges chaqiruvchida).</summary>
        public static void ReplaceMappings(DBTHSNEntities db, ICollection<FIN_CATEGORIES> mapped, int[] categoryIds)
        {
            mapped.Clear();
            if (categoryIds == null || categoryIds.Length == 0) return;
            foreach (var category in db.FIN_CATEGORIES.Where(c => categoryIds.Contains(c.ID)).ToList())
            {
                mapped.Add(category);
            }
        }
    }
}
