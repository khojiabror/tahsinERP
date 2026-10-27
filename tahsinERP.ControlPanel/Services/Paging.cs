using Microsoft.EntityFrameworkCore;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Ro'yxat sahifasi holati: joriy sahifa, qatorlar soni va jami yozuvlar (<c>Pages/Shared/_Pager.cshtml</c>).</summary>
public sealed record PageInfo(int Page, int Size, int Total)
{
    public static readonly int[] Sizes = [10, 25, 50, 100];
    public const int DefaultSize = 25;

    public int Pages => Math.Max(1, (Total + Size - 1) / Size);
    public int From => Total == 0 ? 0 : (Page - 1) * Size + 1;
    public int To => Math.Min(Total, Page * Size);
}

/// <summary>
/// Server tomonda sahifalash: <c>?page=N&amp;size=10|25|50|100</c>. Tanlangan qatorlar soni cookie'da eslab qolinadi
/// (barcha ro'yxatlar uchun bitta), bo'lmasa — <see cref="PageInfo.DefaultSize"/>. So'rov tartiblangan bo'lishi shart.
/// </summary>
public static class Paging
{
    public const string SizeCookie = "cp-rows";

    public static async Task<(List<T> Rows, PageInfo Info)> ToPageAsync<T>(this IOrderedQueryable<T> query, HttpContext http, CancellationToken ct = default)
    {
        int size = ResolveSize(http);
        int total = await query.CountAsync(ct);
        int pages = Math.Max(1, (total + size - 1) / size);
        int page = int.TryParse(http.Request.Query["page"], out int p) && p > 0 ? Math.Min(p, pages) : 1;
        var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (rows, new PageInfo(page, size, total));
    }

    private static int ResolveSize(HttpContext http)
    {
        if (int.TryParse(http.Request.Query["size"], out int s) && PageInfo.Sizes.Contains(s))
        {
            if (http.Request.Cookies[SizeCookie] != s.ToString())
                http.Response.Cookies.Append(SizeCookie, s.ToString(), new CookieOptions
                {
                    HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps, Path = "/", MaxAge = TimeSpan.FromDays(365),
                });
            return s;
        }
        return int.TryParse(http.Request.Cookies[SizeCookie], out s) && PageInfo.Sizes.Contains(s) ? s : PageInfo.DefaultSize;
    }
}
