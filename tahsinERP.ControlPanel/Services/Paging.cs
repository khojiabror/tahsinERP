using Microsoft.EntityFrameworkCore;

namespace tahsinERP.ControlPanel.Services;

/// <summary>
/// Ro'yxat sahifasi holati: joriy sahifa, qatorlar soni va jami yozuvlar (<c>Pages/Shared/_Pager.cshtml</c>).
/// <see cref="Prefix"/> — bitta sahifada bir nechta jadval bo'lsa parametr nomlari (<c>{prefix}page</c>, <c>{prefix}size</c>).
/// </summary>
public sealed record PageInfo(int Page, int Size, int Total, string Prefix = "")
{
    public static readonly int[] Sizes = [10, 25, 50, 100];
    public const int DefaultSize = 25;

    public int Pages => Math.Max(1, (Total + Size - 1) / Size);
    public int From => Total == 0 ? 0 : (Page - 1) * Size + 1;
    public int To => Math.Min(Total, Page * Size);
    public string PageParam => Prefix + "page";
    public string SizeParam => Prefix + "size";
}

/// <summary>
/// Server tomonda sahifalash: <c>?page=N&amp;size=10|25|50|100</c>. Tanlangan qatorlar soni cookie'da eslab qolinadi
/// (barcha ro'yxatlar uchun bitta), bo'lmasa — <see cref="PageInfo.DefaultSize"/>. So'rov tartiblangan bo'lishi shart.
/// </summary>
public static class Paging
{
    public const string SizeCookie = "cp-rows";

    public static async Task<(List<T> Rows, PageInfo Info)> ToPageAsync<T>(this IOrderedQueryable<T> query, HttpContext http, string prefix = "", CancellationToken ct = default)
    {
        var (page, size) = Resolve(http, prefix, await query.CountAsync(ct), out int total);
        var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (rows, new PageInfo(page, size, total, prefix));
    }

    /// <summary>Xotiradagi (allaqachon tartiblangan) ro'yxat uchun — masalan versiya bo'yicha C#'da saralangan relizlar.</summary>
    public static (List<T> Rows, PageInfo Info) ToPage<T>(this IReadOnlyList<T> items, HttpContext http, string prefix = "")
    {
        var (page, size) = Resolve(http, prefix, items.Count, out int total);
        return (items.Skip((page - 1) * size).Take(size).ToList(), new PageInfo(page, size, total, prefix));
    }

    private static (int Page, int Size) Resolve(HttpContext http, string prefix, int count, out int total)
    {
        total = count;
        int size = ResolveSize(http, prefix + "size");
        int pages = Math.Max(1, (total + size - 1) / size);
        int page = int.TryParse(http.Request.Query[prefix + "page"], out int p) && p > 0 ? Math.Min(p, pages) : 1;
        return (page, size);
    }

    private static int ResolveSize(HttpContext http, string param)
    {
        if (int.TryParse(http.Request.Query[param], out int s) && PageInfo.Sizes.Contains(s))
        {
            if (http.Request.Cookies[SizeCookie] != s.ToString() && !http.Response.HasStarted)
                http.Response.Cookies.Append(SizeCookie, s.ToString(), new CookieOptions
                {
                    HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps, Path = "/", MaxAge = TimeSpan.FromDays(365),
                });
            return s;
        }
        return int.TryParse(http.Request.Cookies[SizeCookie], out s) && PageInfo.Sizes.Contains(s) ? s : PageInfo.DefaultSize;
    }
}
