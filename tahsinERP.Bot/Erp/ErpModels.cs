using System.Text.Json;

namespace tahsinERP.Bot.Erp;

// ERP /Distribution/DstApi javoblari (camelCase; o'qish katta-kichik harfga sezgir emas).

public sealed record ErpAccount(int? UserId, int? CustomerId, string Role, string? Name, string? Language);

/// <summary>Telegram do'kon holati (bog'lanmagan foydalanuvchi uchun ham): yoqilganmi, telefon bilan ro'yxatdan o'tish ochiqmi.</summary>
public sealed record ErpShopPublic(bool Enabled, bool Signup);

public sealed record ErpDay(DateTime? StartAt, DateTime? EndAt, int Planned, int Visited, int Orders, decimal OrdersTotal,
    decimal Collected, decimal CashOnHand, int NoOrder);

public sealed record ErpMe(int UserId, string? Name, bool IsAgent, bool IsExpeditor, bool CanOverrideCredit, int VisitRadiusM, ErpDay? Day,
    bool RequireSelfie = false, List<string>? PayMethods = null);

public sealed record ErpTrack(bool Stored, bool DayOpen);

public sealed record ErpIssueResult(int Id, string? IssueNo);

public sealed record ErpNewCustomer(int Id, bool Duplicate);

public sealed record ErpBalancePayment(string Date, decimal Amount);

public sealed record ErpBalance(string? Name, decimal Debt, decimal NotDue, decimal Overdue, decimal Advance, int MaxOverdueDays,
    decimal CreditLimit, int PaymentDays, List<ErpBalancePayment>? LastPayments);

public sealed record ErpStatementRow(string Date, string Type, string? Doc, decimal Debit, decimal Credit, decimal Balance);

public sealed record ErpStatement(string? Name, string From, string To, decimal Opening, decimal TotalDebit, decimal TotalCredit, decimal Closing,
    List<ErpStatementRow>? Rows);

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
    decimal Available, List<ErpCatalogUnit> Units, string? Promo = null);

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

public sealed record ErpWriteOffResult(int Id, string DocNo, string Status, decimal CostTotal);

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

// ---------- 2-bosqich: ekspeditor, nakladnoy, ovozli buyurtma ----------

public sealed record ErpDeliveryMe(bool IsExpeditor, bool IsAgent, bool VoiceOrders, int? TripId);

public sealed record ErpStopLine(int Id, string Product, string? Unit, decimal Qty, decimal Amount, decimal? Delivered);

public sealed record ErpStop(int Id, int Seq, string Customer, string? Address, string? Landmark, string? Phone, decimal? Lat, decimal? Lng,
    string? OrderNo, string? ShipmentNo, decimal Amount, decimal? DeliveredAmount, int Status, string? StatusName, string? Reason, decimal Debt,
    decimal WeightKg, string? NavUrl, List<ErpStopLine>? Lines);

public sealed record ErpTrip(int Id, string DocNo, string Date, int Status, string? StatusName, string? Vehicle, string? Warehouse, decimal WeightKg,
    decimal Total, string? RouteUrl, decimal Expenses, List<ErpStop> Stops)
{
    public const int Planned = 0;
    public const int InTransit = 1;
}

public sealed record ErpTripRef(int Id, string DocNo, int Status);

public sealed record ErpTripStop(ErpTripRef Trip, ErpStop Stop);

public sealed record ErpDeliverResult(string Status, decimal DeliveredAmount, decimal Amount);

public sealed record ErpTripClosed(string? DocNo, int Delivered, int Partial, int Refused, decimal DeliveredAmount, decimal ReturnedAmount,
    decimal Collected, decimal OnHand);

public sealed record ErpInvoiceLine(string Product, string? Unit, decimal Qty, decimal Price, decimal Amount);

public sealed record ErpInvoice(string? Company, string? Warehouse, string ShipmentNo, string? OrderNo, string Date, string Customer, string? Inn,
    string? Address, string? Phone, string? Expeditor, string? Vehicle, string? TripNo, bool Partial, List<ErpInvoiceLine> Lines, decimal Total,
    decimal Debt, string? DeliveredAt);

public sealed record ErpVoiceItem(string Text, decimal Qty, string? Unit);

public sealed record ErpVoiceLine(string Text, int ProductId, int? ProductUnitId, string Product, string? Unit, decimal Qty, decimal? Price,
    decimal? Amount, decimal Available, int Score);

public sealed record ErpVoiceDraft(int? CustomerId, string? Customer, List<ErpNamed>? CustomerOptions, List<ErpVoiceLine>? Lines,
    List<string>? Unmatched, decimal Total);
