using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using tahsinERP.Controllers;
using tahsinERP.Models;

namespace tahsinERP.Services.Crm.EDocuments
{
    /// <summary>
    /// SF va TTNK'ni elektron hujjat sifatida tayyorlash va (sozlangan bo'lsa) EDO provayderiga yuborish.
    /// Hujjatlar har doim avval tizimda yaratiladi (IntegrationStatusCode = LOCAL); tashqi yuborish —
    /// alohida, foydalanuvchi bosadigan amal. Holat mavjud integratsiya ustunlarida saqlanadi:
    /// ExternalSystemCode, ExternalSFKey/ExternalDocKey, IntegrationStatusCode, LastSyncDT, TaxMessageID,
    /// XmlPayload (yuborilgan hujjat nusxasi), XmlSchemaVersion.
    /// </summary>
    public static class EDocumentService
    {
        public const string SchemaVersion = "tahsin-edoc/1";

        // ───────────────────────────── hujjat modelini qurish ─────────────────────────────

        public static EInvoiceDocument BuildInvoice(DBTHSNEntities db, long sfId)
        {
            var sf = db.SALES_SF_HEADERS.AsNoTracking()
                .Include(h => h.SALES_SF_LINES.Select(l => l.PRODUCT.UNIT))
                .Include(h => h.SALES_SF_LINES.Select(l => l.PRODUCT.HSCODE))
                .FirstOrDefault(h => h.ID == sfId);
            if (sf == null) return null;

            var invoice = sf.InvoiceID.HasValue
                ? db.SALES_INVOICES.AsNoTracking().Where(i => i.ID == sf.InvoiceID.Value)
                    .Select(i => new { i.InvoiceNo, i.CurrencyID, i.ContractID }).FirstOrDefault()
                : null;
            var contract = invoice?.ContractID != null
                ? db.S_CONTRACTS.AsNoTracking().Where(c => c.ID == invoice.ContractID.Value)
                    .Select(c => new { c.ContractNo, c.IssuedDate }).FirstOrDefault()
                : null;

            var doc = new EInvoiceDocument
            {
                Uid = sf.IntegrationUID,
                Number = sf.SFNumber,
                Date = sf.SFDate,
                ContractNo = contract?.ContractNo,
                ContractDate = contract?.IssuedDate,
                Currency = SalesLookups.CurrencyCode(invoice?.CurrencyID),
                Seller = Seller(db, sf.SupplierINN),
                Buyer = Buyer(db, sf.CustomerID, sf.CustomerINN)
            };

            foreach (var l in sf.SALES_SF_LINES.OrderBy(l => l.LineNo))
            {
                doc.Items.Add(new EInvoiceItem
                {
                    OrdNo = l.LineNo,
                    Name = string.IsNullOrWhiteSpace(l.Description) ? l.PRODUCT?.Name : l.Description,
                    CatalogCode = null, // MXIK — PRODUCTS'da hali ustun yo'q
                    MeasureName = l.PRODUCT?.UNIT?.ShortName,
                    HsCode = l.PRODUCT?.HSCODE?.HSCODE1,
                    Count = l.Qty,
                    Price = l.UnitPrice,
                    DeliverySum = l.LineAmount,
                    VatRate = l.VATRate,
                    VatSum = l.VATAmount,
                    DeliverySumWithVat = l.TotalWithVAT
                });
            }

            doc.TotalSum = doc.Items.Sum(i => i.DeliverySum);
            doc.TotalVat = doc.Items.Sum(i => i.VatSum);
            doc.TotalWithVat = doc.Items.Sum(i => i.DeliverySumWithVat);
            return doc;
        }

        public static EWaybillDocument BuildWaybill(DBTHSNEntities db, long ttnkId)
        {
            var t = db.SALES_TTNK_HEADERS.AsNoTracking()
                .Include(h => h.SALES_TTNK_LINES.Select(l => l.PRODUCT.UNIT))
                .Include(h => h.SALES_TTNK_LINES.Select(l => l.HSCODE1))
                .Include(h => h.S_CONTRACTS)
                .Include(h => h.SALES_INVOICES)
                .FirstOrDefault(h => h.ID == ttnkId);
            if (t == null) return null;

            var company = db.COMPANIES.AsNoTracking().Select(c => new { c.Name, c.Address, c.DUNS }).FirstOrDefault();
            var doc = new EWaybillDocument
            {
                Uid = t.IntegrationUID,
                Number = t.TTNKNumber,
                Date = t.TTNKDate,
                ContractNo = t.S_CONTRACTS?.ContractNo,
                ContractDate = t.S_CONTRACTS?.IssuedDate,
                InvoiceNo = t.SALES_INVOICES?.InvoiceNo,
                Shipper = new EDocumentParty
                {
                    Tin = FirstNonEmpty(t.ShipperINN, EDocumentSettings.SellerTin, company?.DUNS),
                    Name = FirstNonEmpty(t.ShipperName, company?.Name),
                    Address = company?.Address
                },
                Consignee = new EDocumentParty { Tin = t.ConsigneeINN, Name = t.ConsigneeName },
                Carrier = new EDocumentParty { Tin = t.CarrierINN, Name = t.CarrierName },
                ResponsiblePerson = t.ResponsiblePersonName,
                ResponsiblePersonPinfl = t.ResponsiblePersonPINFL,
                DriverName = t.DriverName,
                DriverPinfl = t.DriverPINFL,
                TruckNo = t.TruckNo,
                TruckModel = t.TruckModel,
                TrailerNo = t.TrailerNo,
                TrailerModel = t.TrailerModel,
                DepartureAddress = t.DepartureAddress,
                DepartureRegion = t.DepartureRegion,
                ArrivalAddress = t.ArrivalAddress,
                ArrivalRegion = t.ArrivalRegion,
                DistanceKm = t.DistanceKm,
                WeightGrossT = t.CargoWeightGrossT,
                WeightNetT = t.CargoWeightNetT,
                Currency = t.CurrencyID.HasValue ? SalesLookups.CurrencyCode(t.CurrencyID) : t.CurrencyCode,
                DeliveryCost = t.DeliveryCost,
                VatAmount = t.VATAmount,
                TotalDeliveryCost = t.TotalDeliveryCost
            };

            foreach (var l in t.SALES_TTNK_LINES.OrderBy(l => l.LineNo))
            {
                doc.Items.Add(new EWaybillItem
                {
                    OrdNo = l.LineNo,
                    Name = string.IsNullOrWhiteSpace(l.Description) ? l.PRODUCT?.Name : l.Description,
                    HsCode = FirstNonEmpty(l.HSCode, l.HSCODE1?.HSCODE1),
                    MeasureName = FirstNonEmpty(l.UnitOfMeasure, l.PRODUCT?.UNIT?.ShortName),
                    Count = l.Qty,
                    Price = l.UnitPrice,
                    Sum = l.LineAmount,
                    WeightNet = l.WeightNetT,
                    WeightGross = l.WeightGrossT,
                    PlaceCount = l.PlaceCount,
                    PackageType = l.PackageType
                });
            }
            return doc;
        }

        // ───────────────────────────── tekshiruv ─────────────────────────────

        /// <summary>Tashqi yuborishdan oldingi kamchiliklar (bo'sh ro'yxat — yuborish mumkin).</summary>
        public static List<string> Validate(EInvoiceDocument doc)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(doc.Number)) errors.Add("SF raqami yo'q.");
            if (!IsTin(doc.Seller?.Tin)) errors.Add("Sotuvchi INN (9 raqam) ko'rsatilmagan — SF'dagi \"Ta'minotchi INN\" yoki EDoc:SellerTin.");
            if (!IsTin(doc.Buyer?.Tin)) errors.Add("Xaridor INN (9 raqam) ko'rsatilmagan.");
            if (!doc.Items.Any()) errors.Add("SF'da qator yo'q.");
            var noCatalog = doc.Items.Where(i => string.IsNullOrWhiteSpace(i.CatalogCode)).Select(i => i.Name).ToList();
            if (noCatalog.Any())
                errors.Add("MXIK (IKPU) kodi yo'q: " + string.Join(", ", noCatalog.Take(5)) + (noCatalog.Count > 5 ? " …" : "") +
                           ". Mahsulotlarga MXIK kodi qo'shilgach yuborish mumkin bo'ladi.");
            return errors;
        }

        public static List<string> Validate(EWaybillDocument doc)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(doc.Number)) errors.Add("TTN raqami yo'q.");
            if (!IsTin(doc.Shipper?.Tin)) errors.Add("Yuk jo'natuvchi INN (9 raqam) ko'rsatilmagan.");
            if (!IsTin(doc.Consignee?.Tin)) errors.Add("Yuk oluvchi INN (9 raqam) ko'rsatilmagan.");
            if (string.IsNullOrWhiteSpace(doc.TruckNo)) errors.Add("Avtomobil raqami ko'rsatilmagan.");
            if (string.IsNullOrWhiteSpace(doc.DriverName)) errors.Add("Haydovchi ko'rsatilmagan.");
            if (!doc.Items.Any()) errors.Add("TTN'da qator yo'q.");
            return errors;
        }

        // ───────────────────────────── yuborish va holat ─────────────────────────────

        /// <summary>SF'ni faol provayderga yuboradi. Ichki rejimda hech narsa yuborilmaydi.</summary>
        public static async Task<EDocumentResult> SendInvoiceAsync(long sfId, string userName)
        {
            var provider = EDocumentProviders.Active();
            if (!provider.IsExternal)
                return EDocumentResult.Fail("Tashqi EDO provayderi ulanmagan — SF tizim ichida saqlangan.");

            EInvoiceDocument doc;
            using (var db = new DBTHSNEntities())
            {
                var sf = db.SALES_SF_HEADERS.Find(sfId);
                if (sf == null) return EDocumentResult.Fail("SF topilmadi.");
                if (EDocumentStatus.IsDelivered(sf.IntegrationStatusCode))
                    return EDocumentResult.Fail("SF allaqachon " + EDocumentStatus.Title(sf.IntegrationStatusCode).ToLower() + " — holatini yangilang.");

                doc = BuildInvoice(db, sfId);
                var errors = Validate(doc);
                if (errors.Any()) return EDocumentResult.Fail(string.Join(" ", errors));
            }

            var result = await provider.SendInvoiceAsync(doc).ConfigureAwait(false);

            using (var db = new DBTHSNEntities())
            {
                var sf = db.SALES_SF_HEADERS.Find(sfId);
                sf.ExternalSystemCode = provider.Code;
                if (!string.IsNullOrEmpty(result.ExternalId)) sf.ExternalSFKey = Truncate(result.ExternalId, 100);
                sf.IntegrationStatusCode = result.Success ? result.Status ?? EDocumentStatus.Sent : EDocumentStatus.Error;
                sf.TaxMessageID = Truncate(result.Message, 100);
                sf.LastSyncDT = DateTime.Now;
                sf.XmlPayload = ToXml(doc);
                sf.XmlSchemaVersion = SchemaVersion;
                db.SaveChanges();
            }

            LogHelper.LogToDatabase(userName, "SalesSFController", $"SF #{sfId} {provider.Title}ga yuborildi: {(result.Success ? "OK" : "xato")}");
            return result;
        }

        public static async Task<EDocumentResult> SendWaybillAsync(long ttnkId, string userName)
        {
            var provider = EDocumentProviders.Active();
            if (!provider.IsExternal)
                return EDocumentResult.Fail("Tashqi EDO provayderi ulanmagan — TTN tizim ichida saqlangan.");

            EWaybillDocument doc;
            using (var db = new DBTHSNEntities())
            {
                var t = db.SALES_TTNK_HEADERS.Find(ttnkId);
                if (t == null) return EDocumentResult.Fail("TTN topilmadi.");
                if (EDocumentStatus.IsDelivered(t.IntegrationStatusCode))
                    return EDocumentResult.Fail("TTN allaqachon " + EDocumentStatus.Title(t.IntegrationStatusCode).ToLower() + " — holatini yangilang.");

                doc = BuildWaybill(db, ttnkId);
                var errors = Validate(doc);
                if (errors.Any()) return EDocumentResult.Fail(string.Join(" ", errors));
            }

            var result = await provider.SendWaybillAsync(doc).ConfigureAwait(false);

            using (var db = new DBTHSNEntities())
            {
                var t = db.SALES_TTNK_HEADERS.Find(ttnkId);
                t.ExternalSystemCode = provider.Code;
                if (!string.IsNullOrEmpty(result.ExternalId)) t.ExternalDocKey = Truncate(result.ExternalId, 100);
                t.IntegrationStatusCode = result.Success ? result.Status ?? EDocumentStatus.Sent : EDocumentStatus.Error;
                t.TaxMessageID = Truncate(result.Message, 100);
                t.LastSyncDT = DateTime.Now;
                t.XmlPayload = ToXml(doc);
                t.XmlSchemaVersion = SchemaVersion;
                t.UpdatedDT = DateTime.Now;
                db.SaveChanges();
            }

            LogHelper.LogToDatabase(userName, "SalesTTNKController", $"TTN #{ttnkId} {provider.Title}ga yuborildi: {(result.Success ? "OK" : "xato")}");
            return result;
        }

        /// <summary>Provayderdan holatni so'raydi va saqlaydi (hujjat yuborilgan provayder orqali).</summary>
        public static async Task<EDocumentResult> RefreshAsync(EDocumentKind kind, long id)
        {
            string providerCode, externalId;
            using (var db = new DBTHSNEntities())
            {
                if (kind == EDocumentKind.Invoice)
                {
                    var h = db.SALES_SF_HEADERS.AsNoTracking().Where(x => x.ID == id).Select(x => new { x.ExternalSystemCode, x.ExternalSFKey }).FirstOrDefault();
                    if (h == null) return EDocumentResult.Fail("SF topilmadi.");
                    providerCode = h.ExternalSystemCode; externalId = h.ExternalSFKey;
                }
                else
                {
                    var h = db.SALES_TTNK_HEADERS.AsNoTracking().Where(x => x.ID == id).Select(x => new { x.ExternalSystemCode, x.ExternalDocKey }).FirstOrDefault();
                    if (h == null) return EDocumentResult.Fail("TTN topilmadi.");
                    providerCode = h.ExternalSystemCode; externalId = h.ExternalDocKey;
                }
            }

            var provider = EDocumentProviders.ByCode(providerCode);
            if (provider == null || !provider.IsExternal)
                return EDocumentResult.Fail("Hujjat tashqi provayderga yuborilmagan.");

            var result = await provider.GetStatusAsync(kind, externalId).ConfigureAwait(false);
            if (!result.Success) return result;

            using (var db = new DBTHSNEntities())
            {
                if (kind == EDocumentKind.Invoice)
                {
                    var h = db.SALES_SF_HEADERS.Find(id);
                    h.IntegrationStatusCode = result.Status ?? h.IntegrationStatusCode;
                    h.TaxMessageID = Truncate(result.Message, 100) ?? h.TaxMessageID;
                    h.LastSyncDT = DateTime.Now;
                }
                else
                {
                    var h = db.SALES_TTNK_HEADERS.Find(id);
                    h.IntegrationStatusCode = result.Status ?? h.IntegrationStatusCode;
                    h.TaxMessageID = Truncate(result.Message, 100) ?? h.TaxMessageID;
                    h.LastSyncDT = DateTime.Now;
                }
                db.SaveChanges();
            }
            return result;
        }

        // ───────────────────────────── Details paneli ─────────────────────────────

        public static EDocumentPanelViewModel Panel(DBTHSNEntities db, SALES_SF_HEADERS sf, bool canChange)
        {
            var vm = BasePanel(EDocumentKind.Invoice, sf.ID, "SalesSF", sf.IntegrationStatusCode, sf.ExternalSystemCode,
                sf.ExternalSFKey, sf.LastSyncDT, sf.TaxMessageID, canChange);
            if (vm.ActiveProviderIsExternal && EDocumentStatus.IsLocal(sf.IntegrationStatusCode))
                vm.Warnings = Validate(BuildInvoice(db, sf.ID));
            return vm;
        }

        public static EDocumentPanelViewModel Panel(DBTHSNEntities db, SALES_TTNK_HEADERS t, bool canChange)
        {
            var vm = BasePanel(EDocumentKind.Waybill, t.ID, "SalesTTNK", t.IntegrationStatusCode, t.ExternalSystemCode,
                t.ExternalDocKey, t.LastSyncDT, t.TaxMessageID, canChange);
            if (vm.ActiveProviderIsExternal && EDocumentStatus.IsLocal(t.IntegrationStatusCode))
                vm.Warnings = Validate(BuildWaybill(db, t.ID));
            return vm;
        }

        private static EDocumentPanelViewModel BasePanel(EDocumentKind kind, long id, string controller, string status,
            string externalSystem, string externalKey, DateTime? lastSync, string message, bool canChange)
        {
            var active = EDocumentProviders.Active();
            return new EDocumentPanelViewModel
            {
                Kind = kind,
                DocumentID = id,
                ControllerName = controller,
                Status = string.IsNullOrEmpty(status) ? EDocumentStatus.Local : status,
                ExternalSystemCode = externalSystem,
                ExternalKey = externalKey,
                LastSyncDT = lastSync,
                LastMessage = message,
                ActiveProviderCode = active.Code,
                ActiveProviderTitle = active.Title,
                ActiveProviderIsExternal = active.IsExternal,
                ActiveProviderConfigured = active.IsConfigured,
                CanChange = canChange
            };
        }

        // ───────────────────────────── yordamchi ─────────────────────────────

        /// <summary>Hujjat nusxasi XmlPayload (SQL xml) ustuni uchun — XML e'lonisiz.</summary>
        public static string ToXml<T>(T doc)
        {
            var serializer = new XmlSerializer(typeof(T));
            var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
            using (var sw = new StringWriter())
            using (var writer = XmlWriter.Create(sw, settings))
            {
                serializer.Serialize(writer, doc, new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
                writer.Flush();
                return sw.ToString();
            }
        }

        private static EDocumentParty Seller(DBTHSNEntities db, string sfSupplierTin)
        {
            var company = db.COMPANIES.AsNoTracking().Select(c => new { c.Name, c.Address, c.DUNS }).FirstOrDefault();
            return new EDocumentParty
            {
                Tin = FirstNonEmpty(sfSupplierTin, EDocumentSettings.SellerTin, company?.DUNS),
                Name = company?.Name,
                Address = company?.Address
            };
        }

        private static EDocumentParty Buyer(DBTHSNEntities db, int customerId, string sfCustomerTin)
        {
            var c = db.CUSTOMERS.AsNoTracking().Where(x => x.ID == customerId)
                .Select(x => new { x.Name, x.Address, x.DUNS, x.DirectorName }).FirstOrDefault();
            return new EDocumentParty
            {
                Tin = FirstNonEmpty(sfCustomerTin, c?.DUNS),
                Name = c?.Name,
                Address = c?.Address,
                Director = c?.DirectorName
            };
        }

        private static bool IsTin(string tin)
        {
            return !string.IsNullOrWhiteSpace(tin) && tin.Trim().Length == 9 && tin.Trim().All(char.IsDigit);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}
