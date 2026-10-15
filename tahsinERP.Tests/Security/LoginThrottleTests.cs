using System;
using System.Runtime.Caching;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Helpers;

namespace tahsinERP.Tests.Security
{
    [TestClass]
    public class LoginThrottleTests
    {
        private DateTime _now;
        private MemoryCache _cache;
        private LoginThrottle _throttle;

        [TestInitialize]
        public void Init()
        {
            _now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
            _cache = new MemoryCache("login-throttle-test");
            _throttle = new LoginThrottle(_cache, () => _now) { MaxFailures = 5, LockoutDuration = TimeSpan.FromMinutes(15) };
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cache.Dispose();
        }

        [TestMethod]
        public void FourFailures_NotLocked_FifthLocks()
        {
            TimeSpan remaining;
            for (int i = 0; i < 4; i++)
                Assert.IsFalse(_throttle.RegisterFailure("admin", "10.0.0.1"));
            Assert.IsFalse(_throttle.IsLockedOut("admin", "10.0.0.1", out remaining));

            Assert.IsTrue(_throttle.RegisterFailure("admin", "10.0.0.1"));
            Assert.IsTrue(_throttle.IsLockedOut("admin", "10.0.0.1", out remaining));
            Assert.AreEqual(TimeSpan.FromMinutes(15), remaining);
        }

        [TestMethod]
        public void Lockout_IsPerUserAndIp_CaseInsensitiveUser()
        {
            for (int i = 0; i < 5; i++)
                _throttle.RegisterFailure("Admin", "10.0.0.1");

            TimeSpan remaining;
            Assert.IsTrue(_throttle.IsLockedOut("ADMIN", "10.0.0.1", out remaining));
            Assert.IsFalse(_throttle.IsLockedOut("admin", "10.0.0.2", out remaining));
            Assert.IsFalse(_throttle.IsLockedOut("other", "10.0.0.1", out remaining));
        }

        [TestMethod]
        public void Lockout_ExpiresAfterDuration()
        {
            for (int i = 0; i < 5; i++)
                _throttle.RegisterFailure("admin", "ip");

            TimeSpan remaining;
            _now = _now.AddMinutes(14);
            Assert.IsTrue(_throttle.IsLockedOut("admin", "ip", out remaining));

            _now = _now.AddMinutes(1).AddSeconds(1);
            Assert.IsFalse(_throttle.IsLockedOut("admin", "ip", out remaining));
        }

        [TestMethod]
        public void Reset_ClearsFailures()
        {
            for (int i = 0; i < 4; i++)
                _throttle.RegisterFailure("admin", "ip");
            _throttle.Reset("admin", "ip");

            Assert.IsFalse(_throttle.RegisterFailure("admin", "ip"), "Reset'dan keyin hisob noldan boshlanadi");
        }
    }
}
