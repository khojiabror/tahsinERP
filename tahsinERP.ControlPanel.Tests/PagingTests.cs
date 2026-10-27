using System.Text.RegularExpressions;
using tahsinERP.ControlPanel.Data;
using static tahsinERP.ControlPanel.Tests.PortalTestKit;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Ro'yxatlar serverda sahifalanadi: ?page/&amp;size (10/25/50/100), filtrlar saqlanadi, qatorlar soni cookie'da.</summary>
public class PagingTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private static int RowCount(string html) => Regex.Matches(html, "href=\"/Tenants/Details/\\d+\"").Count;

    [Fact]
    public async Task Tenants_ArePaged_FilterKept_SizeRemembered()
    {
        for (int i = 1; i <= 23; i++) await f.CreateTenantAsync($"pg-{i:00}");
        var (staff, _) = await StaffLoginAsync(f, "paging-owner@test.uz", StaffRole.Owner);

        string p1 = Html(await staff.GetAsync("/Tenants?Q=pg-&size=10"));
        Assert.Contains("1–10 / 23", p1);
        Assert.Equal(10, RowCount(p1));
        Assert.Contains("href=\"/Tenants?Q=pg-&page=3&size=10\"", p1); // filtr sahifa havolasida saqlanadi
        Assert.Contains("<option value=\"10\" selected=\"selected\">10</option>", p1);

        string p3 = Html(await staff.GetAsync("/Tenants?Q=pg-&page=3&size=10"));
        Assert.Contains("21–23 / 23", p3);
        Assert.Equal(3, RowCount(p3));

        // Tanlangan qatorlar soni eslab qolinadi; ruxsat etilmagan qiymat va katta sahifa raqami e'tiborsiz.
        Assert.Contains("1–10 / 23", Html(await staff.GetAsync("/Tenants?Q=pg-")));
        Assert.Contains("1–10 / 23", Html(await staff.GetAsync("/Tenants?Q=pg-&size=7")));
        Assert.Contains("21–23 / 23", Html(await staff.GetAsync("/Tenants?Q=pg-&page=99")));

        string all = Html(await staff.GetAsync("/Tenants?Q=pg-&size=50"));
        Assert.Equal(23, RowCount(all));
        Assert.DoesNotContain("class=\"pager-pages\"", all); // bitta sahifa — raqamlar ko'rsatilmaydi
    }
}
