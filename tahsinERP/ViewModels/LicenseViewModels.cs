using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>Litsenziya boshqaruv sahifasi (admin) uchun.</summary>
    public class LicenseIndexViewModel
    {
        // Joriy company litsenziyasi holati
        public bool HasLicense { get; set; }
        public bool SignatureValid { get; set; }
        public bool IsExpired { get; set; }
        public bool IsValid { get; set; }
        public string StatusMessage { get; set; }

        public int MaxUsers { get; set; }
        public int UsedSeats { get; set; }
        public bool Unlimited { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Edition { get; set; }
        public int DaysRemaining { get; set; }

        // O'rnatma va himoya holati
        public string InstallationId { get; set; }
        public int KeyVersion { get; set; }
        public bool InGrace { get; set; }
        public DateTime? GraceUntil { get; set; }
        public bool ExpiringSoon { get; set; }
        /// <summary>Soat orqaga surilgan / vaqt muhri buzilgan bo'lsa — sabab.</summary>
        public string ClockProblem { get; set; }
        /// <summary>Kalit boshqa o'rnatmaga bog'langan bo'lsa true.</summary>
        public bool WrongInstallation { get; set; }

        // Markaziy panel bilan sinxronlash (LicenseSyncService)
        public bool PanelEnabled { get; set; }
        public bool PanelConnected { get; set; }
        public bool HasRegToken { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public string LastSyncResult { get; set; }

        public List<SeatRow> Users { get; set; } = new List<SeatRow>();
        public List<LicenseHistoryRow> History { get; set; } = new List<LicenseHistoryRow>();
    }

    public class SeatRow
    {
        public int UserID { get; set; }
        public string Uname { get; set; }
        public string FullName { get; set; }
        public bool IsActiveUser { get; set; }

        public int? UserLicenseID { get; set; }
        public bool HasSeat { get; set; }
        public DateTime? SeatExpiry { get; set; }
        public bool SeatExpired { get; set; }
    }

    public class LicenseHistoryRow
    {
        public int ID { get; set; }
        public int MaxUsers { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Edition { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
