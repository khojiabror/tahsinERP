using System;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    /// <summary>
    /// Detects whether DLOC/FIFO tables exist (migration applied).
    /// </summary>
    public static class DlocFifoSchema
    {
        private static bool? _isEnabled;

        public static bool IsEnabled(DBTHSNEntities db)
        {
            if (_isEnabled.HasValue)
                return _isEnabled.Value;

            try
            {
                var exists = db.Database.SqlQuery<int>(
                    "SELECT CASE WHEN OBJECT_ID('dbo.WAREHOUSE_LOCATIONS','U') IS NOT NULL THEN 1 ELSE 0 END").FirstOrDefault();
                _isEnabled = exists == 1;
            }
            catch
            {
                _isEnabled = false;
            }

            return _isEnabled.Value;
        }

        public static void ResetCache()
        {
            _isEnabled = null;
        }
    }
}
