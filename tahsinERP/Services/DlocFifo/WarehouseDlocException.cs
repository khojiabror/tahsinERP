using System;

namespace tahsinERP.Services.DlocFifo
{
    public class WarehouseDlocException : Exception
    {
        public string ErrorCode { get; }

        public WarehouseDlocException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
