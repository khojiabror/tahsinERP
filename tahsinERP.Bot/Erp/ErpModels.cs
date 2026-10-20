using System.Text.Json;

namespace tahsinERP.Bot.Erp;

// ERP /Distribution/DstApi javoblari (camelCase; o'qish katta-kichik harfga sezgir emas).

public sealed record ErpAccount(int? UserId, int? CustomerId, string Role, string? Name, string? Language);

public sealed record ErpDay(DateTime? StartAt, DateTime? EndAt, int Planned, int Visited, int Orders, decimal OrdersTotal,
    decimal Collected, decimal CashOnHand, int NoOrder);

public sealed record ErpMe(int UserId, string? Name, bool IsAgent, bool IsExpeditor, bool CanOverrideCredit, int VisitRadiusM, ErpDay? Day);

public sealed record ErpDayStarted(int Id, DateTime StartAt);

public sealed record ErpRouteStop(int CustomerId, string Name, string? Address, string? Landmark, string? Phone, decimal? Lat, decimal? Lng,
    bool Planned, bool Visited, string? Result, decimal Debt, decimal Overdue);

public sealed record ErpRecommendation(int ProductId, string? Product, int Times, decimal AvgQtyBase, int DaysSinceLast, bool Overdue);

public sealed record ErpCustomerCard(int CustomerId, string Name, string? Address, string? Landmark, string? Phone, decimal? Lat, decimal? Lng,
    bool Approved, string? PriceType, decimal CreditLimit, decimal Debt, decimal Overdue, int PaymentDays,
    List<JsonElement>? LastOrders, List<ErpRecommendation>? Recommended, int? OpenVisitId);

public sealed record ErpCustomerShort(int Id, string Name, string? Address, bool Approved);

public sealed record ErpNamed(int Id, string Name);

public sealed record ErpCatalogUnit(int? ProductUnitId, string Name, decimal Factor, decimal? Price, bool IsDefault);

public sealed record ErpCatalogItem(int Id, string PNo, string Name, string? NameRu, string? Text, int? CategoryId, string? Category,
    decimal Available, List<ErpCatalogUnit> Units);

public sealed record ErpOrderLine(int ProductId, int? ProductUnitId, decimal Qty, decimal DiscountPct = 0);

public sealed record ErpOrderInput(int CustomerId, DateTime? DeliveryDate, string? Comment, decimal DiscountPct, Guid ClientRequestId,
    int? VisitId, string? SignatureKey, List<ErpOrderLine> Lines);

public sealed record ErpOrderResult(int Id, string DocNo, decimal Total);

public sealed record ErpOrderShort(int Id, string DocNo, string Date, decimal Total, string Status, string Customer);

public sealed record ErpPaymentInput(int CustomerId, int? OrderId, decimal Amount, string Method, string? Note, Guid ClientRequestId, int? VisitId);

public sealed record ErpPaymentResult(int Id, string DocNo, decimal Amount, decimal OnHand);

public sealed record ErpCheckIn(int VisitId, int? DistanceM, int RadiusM);

public sealed record ErpHandover(int Id, string DocNo, decimal AmountDeclared);

public sealed record ErpPending(long Id, long ChatId, string Type, string Text, string? Payload);

public sealed record ErpOverrideResult(int Id, string DocNo, string Status);

/// <summary>ERP xatosi: 400 — biznes qoidasi (matn foydalanuvchiga ko'rsatiladi), 409 — kredit, boshqalari — texnik.</summary>
public sealed class ErpException(int status, string message, IReadOnlyList<string>? problems = null) : Exception(message)
{
    public int Status { get; } = status;
    public IReadOnlyList<string> Problems { get; } = problems ?? [];
    public bool IsBusiness => Status is 400 or 409;
    /// <summary>ERP kompaniya litsenziyasi faol emas (DstApiAuth: 403 "License") — aloqa xatosi ham, rol xatosi ham emas.</summary>
    public bool IsLicense => Status == 403 && Message == LicenseMessage;
    public const string LicenseMessage = "License";
}
