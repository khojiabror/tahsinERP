using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using tahsinERP.Areas.Distribution.Data;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>dst.USER_SCOPES.ScopeType qiymatlari.</summary>
    public static class ScopeTypes
    {
        public const string Warehouse = "WAREHOUSE";
        public const string Territory = "TERRITORY";
        public const string Team = "TEAM";
        public const string Customer = "CUSTOMER";

        public static readonly IReadOnlyList<string> All = new[] { Warehouse, Territory, Team, Customer };
    }

    /// <summary>
    /// Bitta tur bo'yicha ko'rinish doirasi. Qoida: foydalanuvchida shu turdagi yozuv bo'lmasa — cheklov yo'q;
    /// bo'lsa — faqat ro'yxatdagilar. Admin/Developer — har doim cheklovsiz.
    /// </summary>
    public sealed class ScopeFilter
    {
        public static readonly ScopeFilter Unrestricted = new ScopeFilter(null);

        /// <summary>Hech narsani ko'rmaydi (masalan, foydalanuvchi topilmadi).</summary>
        public static readonly ScopeFilter Nothing = new ScopeFilter(new HashSet<int>());

        private readonly HashSet<int> _ids;

        private ScopeFilter(HashSet<int> ids)
        {
            _ids = ids;
        }

        public bool IsUnrestricted => _ids == null;

        public IReadOnlyCollection<int> Ids => _ids ?? (IReadOnlyCollection<int>)Array.Empty<int>();

        public bool Allows(int id) => _ids == null || _ids.Contains(id);

        public bool Allows(int? id) => _ids == null || (id.HasValue && _ids.Contains(id.Value));

        public static ScopeFilter FromRows(IEnumerable<int> scopeIds, bool isAdmin)
        {
            if (isAdmin) return Unrestricted;
            var ids = new HashSet<int>(scopeIds ?? Enumerable.Empty<int>());
            return ids.Count == 0 ? Unrestricted : new ScopeFilter(ids);
        }

        /// <summary>So'rovga filtr qo'shadi (cheklovsiz bo'lsa — o'zgarishsiz).</summary>
        public IQueryable<T> Apply<T>(IQueryable<T> query, Expression<Func<T, int>> key)
        {
            if (_ids == null) return query;
            var ids = _ids.ToList();
            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(int) },
                Expression.Constant(ids), key.Body);
            return query.Where(Expression.Lambda<Func<T, bool>>(contains, key.Parameters));
        }

        /// <summary>Nullable kalit bo'yicha filtr: NULL qiymatli qatorlar cheklangan foydalanuvchiga ko'rinmaydi.</summary>
        public IQueryable<T> Apply<T>(IQueryable<T> query, Expression<Func<T, int?>> key)
        {
            if (_ids == null) return query;
            var ids = _ids.Select(i => (int?)i).ToList();
            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(int?) },
                Expression.Constant(ids), key.Body);
            return query.Where(Expression.Lambda<Func<T, bool>>(contains, key.Parameters));
        }
    }

    /// <summary>
    /// Qator darajasidagi ko'rinish (agent — o'z mijozlari, omborchi — o'z ombori...). Web, API va bot
    /// so'rovlari shu servis orqali filtrlanadi.
    /// </summary>
    public class ScopeService
    {
        private readonly DistributionDbContext _db;

        public ScopeService(DistributionDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public ScopeFilter For(int userId, string scopeType, bool isAdmin)
        {
            if (!ScopeTypes.All.Contains(scopeType))
                throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "Noma'lum scope turi");
            if (isAdmin) return ScopeFilter.Unrestricted;

            var ids = _db.UserScopes
                .Where(s => s.UserID == userId && s.ScopeType == scopeType)
                .Select(s => s.ScopeID)
                .ToList();
            return ScopeFilter.FromRows(ids, false);
        }

        /// <summary>
        /// Agent ierarxiyasi bo'yicha cheklov (mijoz/buyurtma ro'yxatlari uchun, kalit — AgentUserID):
        /// supervayzer — o'z jamoalari agentlari va o'zi; agent — faqat o'zi; qolganlar (ofis) — cheklovsiz.
        /// </summary>
        public ScopeFilter AgentsFor(int userId, bool isAdmin)
        {
            if (isAdmin) return ScopeFilter.Unrestricted;

            var teamIds = _db.SalesTeams.Where(t => t.SupervisorUserID == userId && t.IsActive).Select(t => t.ID).ToList();
            if (teamIds.Count > 0)
            {
                var members = _db.SalesAgents.Where(a => a.TeamID != null && teamIds.Contains(a.TeamID.Value))
                    .Select(a => a.UserID).ToList();
                members.Add(userId);
                return ScopeFilter.FromRows(members, false);
            }

            bool isAgent = _db.SalesAgents.Any(a => a.UserID == userId && a.IsActive);
            return isAgent ? ScopeFilter.FromRows(new[] { userId }, false) : ScopeFilter.Unrestricted;
        }

        public ScopeFilter AgentsFor(string userName)
        {
            var userId = tahsinERP.Models.RoleHelper.GetUserId(userName);
            if (!userId.HasValue) return ScopeFilter.Nothing;
            return AgentsFor(userId.Value, tahsinERP.Models.RoleHelper.IsAdminOrDeveloper(userName));
        }

        /// <summary>Joriy web foydalanuvchisi uchun (Admin/Developer — cheklovsiz).</summary>
        public ScopeFilter For(string userName, string scopeType)
        {
            var userId = tahsinERP.Models.RoleHelper.GetUserId(userName);
            if (!userId.HasValue) return ScopeFilter.Nothing;
            return For(userId.Value, scopeType, tahsinERP.Models.RoleHelper.IsAdminOrDeveloper(userName));
        }
    }
}
