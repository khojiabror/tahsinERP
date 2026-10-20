using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Xarid shartnomasi bo'yicha qoldiq: shartnomadagi miqdor vs invoys qilingan miqdor.
    /// </summary>
    public class ContractBalanceService
    {
        /// <summary>UNITS.ID: dona (kesiladigan mahsulotda faqat shu birlik CBM'ga aylantiriladi).</summary>
        private const int PieceUnitId = 1;

        private readonly DBTHSNEntities _db;
        private readonly UnitConversionService _conversionService;

        public ContractBalanceService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>Bitta mahsulot bo'yicha shartnoma qoldig'i.</summary>
        public async Task<ContractBalanceInfo> GetContractBalanceAsync(int contractId, int productId)
        {
            var balances = await GetContractBalancesAsync(contractId, new[] { productId });
            return balances[productId];
        }

        /// <summary>
        /// Bir nechta mahsulot bo'yicha shartnoma qoldig'i — invoys qatorlari bitta so'rovda olinadi
        /// (avval har invoys qatori uchun 4-5 ta so'rov ketardi, SRM-P05).
        ///
        /// Invoys qilingan miqdorga kiradi (SRM-B17): o'chirilmagan invoyslar, ular shartnomaga
        /// to'g'ridan-to'g'ri (P_INVOICES.ContractID) yoki o'chirilmagan buyurtma orqali
        /// (P_ORDERS.ContractID) bog'langan bo'lsa. Kesiladigan mahsulotda faqat donada (UnitID=1)
        /// kiritilgan qatorlar CBM'ga aylantiriladi; m3 da kiritilgani o'zgarishsiz qo'shiladi
        /// (PInvoices/Details bilan bir xil qoida).
        /// </summary>
        public async Task<Dictionary<int, ContractBalanceInfo>> GetContractBalancesAsync(int contractId, IEnumerable<int> productIds)
        {
            var ids = (productIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            var result = new Dictionary<int, ContractBalanceInfo>();
            if (ids.Count == 0)
            {
                return result;
            }

            // Shartnoma qatorlari (bir mahsulot bir necha marta bo'lsa — avvalgidek birinchisi)
            var contractLines = (await _db.P_CONTRACT_PRODUCTS.AsNoTracking()
                    .Where(cp => cp.ContractID == contractId && ids.Contains(cp.ProductID))
                    .Select(cp => new { cp.ID, cp.ProductID, cp.Quantity })
                    .ToListAsync())
                .GroupBy(cp => cp.ProductID)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.ID).First().Quantity);

            var invoiceLines = await _db.P_INVOICE_PRODUCTS.AsNoTracking()
                .Where(ip => ids.Contains(ip.ProductID)
                    && ip.P_INVOICES.IsDeleted != true
                    && (ip.P_INVOICES.ContractID == contractId
                        || (ip.P_INVOICES.P_ORDERS != null
                            && ip.P_INVOICES.P_ORDERS.IsDeleted != true
                            && ip.P_INVOICES.P_ORDERS.ContractID == contractId)))
                .Select(ip => new { ip.ProductID, ip.Quantity, ip.UnitID })
                .ToListAsync();
            var linesByProduct = invoiceLines.ToLookup(l => l.ProductID);
            // FIN-P14: mahsulot ma'lumotlari (birlik konversiyasi uchun) bitta so'rovda
            await _conversionService.PreloadProductsAsync(contractLines.Keys);

            foreach (var productId in ids)
            {
                decimal contractQuantity;
                if (!contractLines.TryGetValue(productId, out contractQuantity))
                {
                    result[productId] = new ContractBalanceInfo();
                    continue;
                }

                // Vazn bo'yicha hisoblanadigan mahsulotda shartnoma ham, invoys ham kg da — konversiya yo'q
                bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(productId);
                bool requiresCutting = !calculateByWeight && await _conversionService.RequiresCuttingAsync(productId);

                decimal contractQty = contractQuantity;
                decimal invoicedQty = 0;
                decimal piecesToConvert = 0;
                decimal invoicedQtyCBM = 0;

                foreach (var line in linesByProduct[productId])
                {
                    decimal qty = line.Quantity;
                    invoicedQty += qty;

                    if (requiresCutting && line.UnitID == PieceUnitId)
                    {
                        piecesToConvert += qty;
                    }
                    else
                    {
                        invoicedQtyCBM += qty;
                    }
                }

                if (piecesToConvert > 0)
                {
                    try
                    {
                        // dona -> CBM chiziqli (qty × L×W×H), shuning uchun yig'indini bir marta aylantiramiz
                        invoicedQtyCBM += await _conversionService.ConvertPieceToCBMAsync(productId, piecesToConvert);
                    }
                    catch (InvalidOperationException)
                    {
                        // O'lcham kiritilmagan bo'lsa — avvalgidek miqdorni o'zgarishsiz olamiz
                        invoicedQtyCBM += piecesToConvert;
                    }
                }

                result[productId] = new ContractBalanceInfo
                {
                    ContractQuantity = contractQty,
                    ContractQuantityCBM = contractQty,
                    InvoicedQuantity = invoicedQty,
                    InvoicedQuantityCBM = invoicedQtyCBM,
                    RemainingQuantity = contractQty - invoicedQty,
                    RemainingQuantityCBM = contractQty - invoicedQtyCBM,
                    FulfillmentPercentage = contractQty > 0 ? invoicedQtyCBM / contractQty * 100 : 0
                };
            }

            return result;
        }
    }

    /// <summary>
    /// DTO for contract balance information
    /// </summary>
    public class ContractBalanceInfo
    {
        public decimal ContractQuantity { get; set; }
        public decimal ContractQuantityCBM { get; set; }
        public decimal InvoicedQuantity { get; set; }
        public decimal InvoicedQuantityCBM { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal RemainingQuantityCBM { get; set; }
        public decimal FulfillmentPercentage { get; set; }
    }
}
