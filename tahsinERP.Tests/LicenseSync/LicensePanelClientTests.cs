using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using tahsinERP.Services.LicenseSync;

namespace tahsinERP.Tests.LicenseSync
{
    /// <summary>
    /// Soxta panel: 01_LICENSE_API.md dagi qoidalarni ERP klientidan MUSTAQIL qayta amalga oshiradi
    /// (imzo qatori, vaqt oynasi, nonce, ETag) — klient kontraktdan chetga chiqsa test yiqiladi.
    /// </summary>
    internal sealed class FakeLicensePanel : HttpMessageHandler
    {
        public const string Token = "RT-TEST-0001";
        public string Secret = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
        public string BoundInstallation;
        public string CurrentKey = "KEY-v2-AAA";
        public DateTime ServerNowUtc = DateTime.UtcNow;
        public bool Revoked;
        public bool Down;
        public readonly List<HttpRequestMessage> Requests = new List<HttpRequestMessage>();
        private readonly HashSet<string> _nonces = new HashSet<string>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            if (Down)
                throw new HttpRequestException("connection refused");

            string path = request.RequestUri.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/api/license/register")
            {
                var body = JObject.Parse(await request.Content.ReadAsStringAsync());
                if ((string)body["reg_token"] != Token)
                    return Json(HttpStatusCode.NotFound, new JObject { ["error"] = "token_not_found" });
                string inst = (string)body["installation_id"];
                Guid g;
                if (!Guid.TryParse(inst, out g) || inst != g.ToString("D").ToUpperInvariant())
                    return Json(HttpStatusCode.BadRequest, new JObject { ["error"] = "bad_request" });
                if (BoundInstallation != null && BoundInstallation != inst)
                    return Json(HttpStatusCode.Conflict, new JObject { ["error"] = "token_used" });
                BoundInstallation = inst;
                return Json(HttpStatusCode.OK, new JObject { ["tenant_secret"] = Secret, ["license_key"] = CurrentKey, ["poll_hours"] = 6 });
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/license/"))
            {
                string inst = path.Substring("/api/license/".Length);
                if (inst != BoundInstallation)
                    return Json(HttpStatusCode.NotFound, new JObject { ["error"] = "unknown_installation" });

                string ts = Header(request, "X-Tahsin-Timestamp");
                string nonce = Header(request, "X-Tahsin-Nonce");
                string sig = Header(request, "X-Tahsin-Signature");
                string etag = request.Headers.IfNoneMatch.Select(e => e.Tag.Trim('"')).FirstOrDefault();

                long tsVal;
                if (!long.TryParse(ts, out tsVal) || Math.Abs(tsVal - ToUnix(ServerNowUtc)) > 300)
                    return Json(HttpStatusCode.Unauthorized, new JObject { ["error"] = "clock_skew" });
                if (nonce == null || nonce.Length != 32 || !_nonces.Add(nonce))
                    return Json(HttpStatusCode.Unauthorized, new JObject { ["error"] = "replay" });

                string data = "GET\n" + path + "\n" + ts + "\n" + nonce + "\n" + (etag ?? "");
                string expected;
                using (var h = new HMACSHA256(Convert.FromBase64String(Secret)))
                    expected = Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(data)));
                if (sig != expected)
                    return Json(HttpStatusCode.Unauthorized, new JObject { ["error"] = "bad_signature" });

                if (Revoked)
                    return Json(HttpStatusCode.Gone, new JObject { ["error"] = "revoked" });
                string currentEtag = Sha256Hex(CurrentKey);
                if (etag == currentEtag)
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
                return Json(HttpStatusCode.OK, new JObject { ["license_key"] = CurrentKey, ["etag"] = currentEtag });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        public static string Sha256Hex(string s)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")));
        }

        private static long ToUnix(DateTime utc) { return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }

        private static string Header(HttpRequestMessage r, string name)
        {
            IEnumerable<string> v;
            return r.Headers.TryGetValues(name, out v) ? v.FirstOrDefault() : null;
        }

        private static HttpResponseMessage Json(HttpStatusCode code, JObject body)
        {
            return new HttpResponseMessage(code) { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") };
        }
    }

    [TestClass]
    public class LicensePanelClientTests
    {
        private const string Inst = "3F2504E0-4F89-11D3-9A0C-0305E82C3301";
        private FakeLicensePanel _panel;
        private LicensePanelClient _client;

        [TestInitialize]
        public void Init()
        {
            _panel = new FakeLicensePanel();
            _client = new LicensePanelClient("https://panel.test", _panel);
        }

        [TestMethod]
        public async Task Register_ReturnsSecretAndKey()
        {
            var r = await _client.RegisterAsync(FakeLicensePanel.Token, Inst, 1, "1.0", "firma1.example.uz");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(_panel.Secret, r.TenantSecret);
            Assert.AreEqual("KEY-v2-AAA", r.LicenseKey);
            Assert.AreEqual(6, r.PollHours);
            Assert.AreEqual(Inst, _panel.BoundInstallation);
        }

        [TestMethod]
        public async Task Register_Idempotent_SameInstallation()
        {
            await _client.RegisterAsync(FakeLicensePanel.Token, Inst, 1, "1.0", null);
            var again = await _client.RegisterAsync(FakeLicensePanel.Token, Inst, 1, "1.0", null);
            Assert.IsTrue(again.Ok);
        }

        [TestMethod]
        public async Task Register_Errors()
        {
            var bad = await _client.RegisterAsync("WRONG-TOKEN", Inst, 1, "1.0", null);
            Assert.IsFalse(bad.Ok);
            Assert.AreEqual("token_not_found", bad.Error);
            Assert.AreEqual(404, bad.HttpStatus);

            await _client.RegisterAsync(FakeLicensePanel.Token, Inst, 1, "1.0", null);
            var other = await _client.RegisterAsync(FakeLicensePanel.Token, "9B1DEB4D-3B7D-4BAD-9BDD-2B0D7B3DCB6D", 1, "1.0", null);
            Assert.AreEqual("token_used", other.Error);
        }

        [TestMethod]
        public async Task Register_PanelDown_NoThrow()
        {
            _panel.Down = true;
            var r = await _client.RegisterAsync(FakeLicensePanel.Token, Inst, 1, "1.0", null);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains(r.Error, "ulanib bo'lmadi");
        }

        [TestMethod]
        public async Task Pull_SignedRequest_AcceptedByIndependentVerifier()
        {
            _panel.BoundInstallation = Inst;
            var r = await _client.PullAsync(Inst, _panel.Secret, null);
            Assert.AreEqual(LicensePanelClient.PullStatus.Updated, r.Status, r.Error);
            Assert.AreEqual("KEY-v2-AAA", r.LicenseKey);
        }

        [TestMethod]
        public async Task Pull_SameKey_NotModifiedViaETag()
        {
            _panel.BoundInstallation = Inst;
            var r = await _client.PullAsync(Inst, _panel.Secret, "KEY-v2-AAA");
            Assert.AreEqual(LicensePanelClient.PullStatus.NotModified, r.Status, r.Error);
            Assert.AreEqual(304, r.HttpStatus);

            _panel.CurrentKey = "KEY-v2-BBB";
            r = await _client.PullAsync(Inst, _panel.Secret, "KEY-v2-AAA");
            Assert.AreEqual(LicensePanelClient.PullStatus.Updated, r.Status);
            Assert.AreEqual("KEY-v2-BBB", r.LicenseKey);
        }

        [TestMethod]
        public async Task Pull_WrongSecret_Unauthorized()
        {
            _panel.BoundInstallation = Inst;
            string wrong = Convert.ToBase64String(new byte[32]);
            var r = await _client.PullAsync(Inst, wrong, null);
            Assert.AreEqual(LicensePanelClient.PullStatus.Unauthorized, r.Status);
            Assert.AreEqual("bad_signature", r.Error);
        }

        [TestMethod]
        public async Task Pull_ClockSkew_Unauthorized()
        {
            _panel.BoundInstallation = Inst;
            var skewed = new LicensePanelClient("https://panel.test", _panel, () => DateTime.UtcNow.AddMinutes(-10));
            var r = await skewed.PullAsync(Inst, _panel.Secret, null);
            Assert.AreEqual(LicensePanelClient.PullStatus.Unauthorized, r.Status);
            Assert.AreEqual("clock_skew", r.Error);
        }

        [TestMethod]
        public async Task Pull_UnknownRevokedDown()
        {
            var unknown = await _client.PullAsync(Inst, _panel.Secret, null);
            Assert.AreEqual(LicensePanelClient.PullStatus.NotFound, unknown.Status);

            _panel.BoundInstallation = Inst;
            _panel.Revoked = true;
            Assert.AreEqual(LicensePanelClient.PullStatus.Revoked, (await _client.PullAsync(Inst, _panel.Secret, null)).Status);

            _panel.Down = true;
            var down = await _client.PullAsync(Inst, _panel.Secret, null);
            Assert.AreEqual(LicensePanelClient.PullStatus.Failed, down.Status);
        }

        [TestMethod]
        public async Task Pull_NonceIsFreshEachTime()
        {
            _panel.BoundInstallation = Inst;
            await _client.PullAsync(Inst, _panel.Secret, null);
            var second = await _client.PullAsync(Inst, _panel.Secret, null);
            Assert.AreEqual(LicensePanelClient.PullStatus.Updated, second.Status, "takroriy nonce bo'lsa replay deb rad etilardi");
        }

        [TestMethod]
        public void Signature_KnownVector()
        {
            // Panel (C#/.NET 10) shu vektor bilan o'z implementatsiyasini tekshiradi
            string secret = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
            string sig = LicensePanelClient.ComputeSignature(secret, "GET", "/api/license/" + Inst, "1792224000",
                "00112233445566778899aabbccddeeff", null);
            string expected;
            using (var h = new HMACSHA256(Convert.FromBase64String(secret)))
                expected = Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(
                    "GET\n/api/license/" + Inst + "\n1792224000\n00112233445566778899aabbccddeeff\n")));
            Assert.AreEqual(expected, sig);
            Assert.AreEqual(FakeLicensePanel.Sha256Hex("abc"), LicensePanelClient.ComputeETag("abc"));
        }

        [TestMethod]
        public void BaseUrl_RequiresHttpsExceptLoopback()
        {
            Assert.ThrowsException<ArgumentException>(() => LicensePanelClient.ParseBaseUrl("http://panel.example.uz"));
            Assert.ThrowsException<ArgumentException>(() => LicensePanelClient.ParseBaseUrl(""));
            Assert.AreEqual("https://panel.example.uz/", LicensePanelClient.ParseBaseUrl("https://panel.example.uz").ToString());
            Assert.AreEqual("http://localhost:5000/", LicensePanelClient.ParseBaseUrl("http://localhost:5000/").ToString());
        }

        [TestMethod]
        public void Secret_MinimumLength()
        {
            Assert.IsFalse(LicensePanelClient.IsValidSecret(null));
            Assert.IsFalse(LicensePanelClient.IsValidSecret(Convert.ToBase64String(new byte[16])));
            Assert.IsFalse(LicensePanelClient.IsValidSecret("not base64!"));
            Assert.IsTrue(LicensePanelClient.IsValidSecret(Convert.ToBase64String(new byte[32])));
        }
    }
}
