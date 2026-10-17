using System;
using System.Collections.Concurrent;
using System.Linq;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    /// <summary>
    /// Detects whether DLOC/FIFO tables exist (migration applied).
    /// </summary>
    public static class DlocFifoSchema
    {
        // Har tenant bazasida migratsiya holati har xil bo'lishi mumkin — kalit = tenant prefiksi
        private static readonly ConcurrentDictionary<string, bool> _isEnabled =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        public static bool IsEnabled(DBTHSNEntities db)
        {
            string key = TenantContext.CacheKeyPrefix;
            bool enabled;
            if (_isEnabled.TryGetValue(key, out enabled))
                return enabled;

            try
            {
                var exists = db.Database.SqlQuery<int>(
                    "SELECT CASE WHEN OBJECT_ID('dbo.WAREHOUSE_LOCATIONS','U') IS NOT NULL THEN 1 ELSE 0 END").FirstOrDefault();
                enabled = exists == 1;
            }
            catch
            {
                enabled = false;
            }

            _isEnabled[key] = enabled;
            return enabled;
        }

    }
}
