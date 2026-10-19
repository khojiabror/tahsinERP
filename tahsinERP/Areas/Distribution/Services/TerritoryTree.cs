using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>Hududlar daraxti tekis ro'yxat ko'rinishida (ota-ona → bolalar, nom bo'yicha).</summary>
    public static class TerritoryTree
    {
        public sealed class Node
        {
            public Territory Item { get; set; }
            public int Depth { get; set; }
            /// <summary>"Viloyat / Tuman / Hudud" — tanlash ro'yxatlari uchun to'liq yo'l.</summary>
            public string Path { get; set; }
        }

        public static List<Node> Flatten(IEnumerable<Territory> all, bool russian = false)
        {
            var list = all.ToList();
            var byParent = list.ToLookup(t => t.ParentID);
            var result = new List<Node>();
            var ids = new HashSet<int>(list.Select(t => t.ID));

            void Walk(int? parentId, int depth, string prefix)
            {
                foreach (var t in byParent[parentId].OrderBy(t => t.Name))
                {
                    string name = russian && !string.IsNullOrWhiteSpace(t.NameRu) ? t.NameRu : t.Name;
                    string path = prefix == null ? name : prefix + " / " + name;
                    result.Add(new Node { Item = t, Depth = depth, Path = path });
                    if (depth < 3) Walk(t.ID, depth + 1, path);
                }
            }

            Walk(null, 0, null);
            // ota-onasi topilmagan (yetim) yozuvlar ham ko'rinsin
            foreach (var orphan in list.Where(t => t.ParentID.HasValue && !ids.Contains(t.ParentID.Value)).OrderBy(t => t.Name))
                result.Add(new Node { Item = orphan, Depth = 0, Path = orphan.Name });
            return result;
        }
    }
}
