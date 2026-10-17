using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.ViewModels;

namespace tahsinERP.Services.Procurement
{
    /// <summary>
    /// SRM xarid ro'yxatlari uchun server-side DataTables sahifasi (SRM-P03): jami soni, qidiruv bo'yicha soni,
    /// saralash va Skip/Take — hammasi SQL'da. Har Index uchun bir xil count/search/order/page kodi takrorlanmasin.
    /// </summary>
    public static class ServerTablePage
    {
        public sealed class Result<T>
        {
            public int Total { get; set; }
            public int Filtered { get; set; }
            public List<T> Rows { get; set; }
        }

        /// <param name="query">Filtrlangan (qidiruvsiz) proyeksiya.</param>
        /// <param name="search">Qidiruv sharti (qidiruv matni bo'sh bo'lsa chaqirilmaydi).</param>
        /// <param name="order">Ustun raqami va yo'nalish bo'yicha saralash (barqaror bo'lishi uchun ID bilan yakunlansin).</param>
        public static async Task<Result<T>> LoadAsync<T>(
            IQueryable<T> query,
            DataTablesPageRequest page,
            Func<IQueryable<T>, string, IQueryable<T>> search,
            Func<IQueryable<T>, int, bool, IOrderedQueryable<T>> order)
        {
            int total = await query.CountAsync();
            var filteredQuery = page.Search == null ? query : search(query, page.Search);
            int filtered = page.Search == null ? total : await filteredQuery.CountAsync();

            var rows = await order(filteredQuery, page.OrderColumn, page.OrderDescending)
                .Skip(page.Start)
                .Take(page.Length)
                .ToListAsync();

            return new Result<T> { Total = total, Filtered = filtered, Rows = rows };
        }

        /// <summary>Ustun bo'yicha o'sish/kamayish tartibi (switch'larni qisqartirish uchun).</summary>
        public static IOrderedQueryable<T> By<T, TKey>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, TKey>> key, bool desc)
        {
            return desc ? query.OrderByDescending(key) : query.OrderBy(key);
        }
    }
}
