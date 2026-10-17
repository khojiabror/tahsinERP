using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace tahsinERP.Services.Crm
{
    /// <summary>
    /// CRM Index sahifalari uchun server tomonida sahifalash (CRM-D16: oltita controllerda bir xil
    /// blok takrorlanardi). Natija Views/SalesInvoices/_Pager.cshtml o'qiydigan
    /// ViewBag.CurrentPage / TotalPages / TotalItems / PageSize maydonlariga yoziladi.
    /// </summary>
    public static class CrmPaging
    {
        public const int DefaultPageSize = 25;
        public const int MaxPageSize = 200;

        /// <param name="query">Tartiblangan so'rov (Skip uchun OrderBy shart).</param>
        /// <param name="viewData">Controller.ViewData (ViewBag bilan bir lug'at).</param>
        public static async Task<List<T>> ToPageAsync<T>(this IQueryable<T> query, int? page, int? pageSize, ViewDataDictionary viewData)
        {
            int itemsPerPage = Math.Min(Math.Max(pageSize ?? DefaultPageSize, 1), MaxPageSize);
            int totalItems = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / itemsPerPage));
            int currentPage = Math.Min(Math.Max(page ?? 1, 1), totalPages);

            var items = await query
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToListAsync();

            viewData["CurrentPage"] = currentPage;
            viewData["TotalPages"] = totalPages;
            viewData["TotalItems"] = totalItems;
            viewData["PageSize"] = itemsPerPage;
            return items;
        }
    }
}
