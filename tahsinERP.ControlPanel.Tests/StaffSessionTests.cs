using System.Net;
using tahsinERP.ControlPanel.Data;
using static tahsinERP.ControlPanel.Tests.PortalTestKit;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Xodim seansi: 15 daqiqa harakatsizlikda tugaydi, har so'rov (KeepAlive ham) muddatni yangilaydi.</summary>
public class StaffSessionTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    [Fact]
    public async Task Session_SlidesOnEveryRequest_AndExpiresAfter15IdleMinutes()
    {
        var start = f.Time.GetUtcNow();
        try
        {
            var (staff, _) = await StaffLoginAsync(f, "idle-owner@test.uz", StaffRole.Owner);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/")).StatusCode);

            // Faol: har 10 daqiqada so'rov — 15 daqiqalik chegaradan uzoq ishlasa ham seans tirik.
            for (int i = 0; i < 3; i++)
            {
                f.Time.Advance(TimeSpan.FromMinutes(10));
                Assert.Equal(HttpStatusCode.NoContent, (await staff.GetAsync("/Account/KeepAlive")).StatusCode);
            }
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/Tenants")).StatusCode);

            // Harakatsiz 16 daqiqa — kirish sahifasiga.
            f.Time.Advance(TimeSpan.FromMinutes(16));
            var expired = await staff.GetAsync("/Tenants");
            Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
            Assert.Contains("/Account/Login", expired.Headers.Location!.OriginalString);
            Assert.NotEqual(HttpStatusCode.NoContent, (await staff.GetAsync("/Account/KeepAlive")).StatusCode);
        }
        finally { f.Time.SetUtcNow(start); }
    }

    [Fact]
    public async Task LoginPage_ShowsExpiredReason_AndFavicon()
    {
        var (c, _) = Client(f, AdminBase);
        string html = Html(await c.GetAsync("/Account/Login?expired=true"));
        Assert.Contains("15 daqiqa harakatsizlik", html);
        Assert.Contains("tahsinERP Admin</title>", html);
        Assert.Contains("href=\"/favicon.ico\"", html);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/favicon.ico")).StatusCode);
    }
}
