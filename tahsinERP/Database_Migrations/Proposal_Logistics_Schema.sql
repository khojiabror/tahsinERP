/*
  Logistika moduli — DB sxema TAKLIFLARI (audit 2026-10). ISHGA TUSHIRILMAGAN.
  Kod bu o'zgarishlarsiz ishlaydi; ular qo'llansa EDMX'ni yangilash kerak (1-band).
  Har bandni alohida, zaxira nusxadan keyin va avval tekshiruv SELECT'lari bilan bajaring.
*/

/* 1) LOG-B05: BAJARILDI (2026-10-18, foydalanuvchi ruxsati bilan) —
      Logistics_2026_10_B05_WaybillTariffID.sql (ustun, FK, indeks, eski yozuvlarni to'ldirish) va EDMX. */

/* 2) LOG-B07/P04: WaybillNo takrorlanmasin (o'chirilmaganlar orasida).
      Kod DocumentNumberService qulfi va ModelState tekshiruvi bilan himoyalangan; indeks oxirgi kafolat.
      Avval takrorlarni toping: */
SELECT WaybillNo, COUNT(*) AS Cnt
FROM dbo.F_WAYBILLS
WHERE ISNULL(IsDeleted, 0) = 0
GROUP BY WaybillNo
HAVING COUNT(*) > 1;
-- (2-band IsDeleted NOT NULL bo'lgandan keyin)
-- CREATE UNIQUE INDEX UX_F_WAYBILLS_WaybillNo_Active ON dbo.F_WAYBILLS(WaybillNo) WHERE IsDeleted = 0;

/* 3) LOG-B14: IsDeleted NULL bo'lmasin (hozir kod "IsDeleted != true" bilan NULL'ni faol deb oladi). */
SELECT 'F_WAYBILLS' AS T, COUNT(*) AS NullRows FROM dbo.F_WAYBILLS WHERE IsDeleted IS NULL
UNION ALL SELECT 'FORWARDERS', COUNT(*) FROM dbo.FORWARDERS WHERE IsDeleted IS NULL
UNION ALL SELECT 'F_TRANSPORT_TYPES', COUNT(*) FROM dbo.F_TRANSPORT_TYPES WHERE IsDeleted IS NULL;
-- UPDATE dbo.F_WAYBILLS SET IsDeleted = 0 WHERE IsDeleted IS NULL;
-- UPDATE dbo.FORWARDERS SET IsDeleted = 0 WHERE IsDeleted IS NULL;
-- UPDATE dbo.F_TRANSPORT_TYPES SET IsDeleted = 0 WHERE IsDeleted IS NULL;
-- ALTER TABLE dbo.F_WAYBILLS ADD CONSTRAINT DF_F_WAYBILLS_IsDeleted DEFAULT 0 FOR IsDeleted;
-- ALTER TABLE dbo.F_WAYBILLS ALTER COLUMN IsDeleted BIT NOT NULL;
-- ALTER TABLE dbo.FORWARDERS ADD CONSTRAINT DF_FORWARDERS_IsDeleted DEFAULT 0 FOR IsDeleted;
-- ALTER TABLE dbo.FORWARDERS ALTER COLUMN IsDeleted BIT NOT NULL;
-- ALTER TABLE dbo.F_TRANSPORT_TYPES ADD CONSTRAINT DF_F_TRANSPORT_TYPES_IsDeleted DEFAULT 0 FOR IsDeleted;
-- ALTER TABLE dbo.F_TRANSPORT_TYPES ALTER COLUMN IsDeleted BIT NOT NULL;

/* 4) Issiq so'rovlar uchun indekslar (WaybillLedgerService, Waybill Index sahifalash, MonthlyReport).
      IX_F_WPS_WaybillID allaqachon Audit_2026_10_Indexes.sql'da. sys.indexes bilan tekshirib qo'shing. */
-- CREATE INDEX IX_F_WPS_Payer ON dbo.F_WAYBILL_PAYMENT_SPLITS(PayerType, PayerID) INCLUDE (WaybillID, Amount);
-- CREATE INDEX IX_F_WAYBILL_PAYMENTS_Waybill ON dbo.F_WAYBILL_PAYMENTS(WaybillID) INCLUDE (IsDeleted, Payment_Amount);
-- CREATE INDEX IX_F_WAYBILL_PAYMENTS_Forwarder_Date ON dbo.F_WAYBILL_PAYMENTS(ForwarderID, PaymentDate) INCLUDE (IsDeleted, Payment_Amount, WaybillID);
-- CREATE INDEX IX_F_WAYBILLS_IssueDate ON dbo.F_WAYBILLS(IssueDate DESC) INCLUDE (IsDeleted, ForwarderID, Amount, CurrencyID);
-- CREATE INDEX IX_F_WAYBILLS_Forwarder_Date ON dbo.F_WAYBILLS(ForwarderID, IssueDate) INCLUDE (IsDeleted, Amount);
-- CREATE INDEX IX_FIN_TRANSACTIONS_SourceDoc ON dbo.FIN_TRANSACTIONS(SourceDocType, SourceDocID) INCLUDE (IsDeleted);
-- CREATE INDEX IX_FIN_TRANSACTIONS_Counterparty ON dbo.FIN_TRANSACTIONS(CounterpartyType, CounterpartyID, TxnDate) INCLUDE (Direction, Amount, IsDeleted, SourceDocType);
