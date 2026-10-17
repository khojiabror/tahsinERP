using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for tracking contract balances and fulfillment
    /// </summary>
    public class ContractBalanceService
    {
        private readonly DBTHSNEntities _db;
        private readonly UnitConversionService _conversionService;

        public ContractBalanceService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>
        /// Gets contract balance information for a product
        /// </summary>
        /// <param name="contractId">Contract ID</param>
        /// <param name="productId">Product ID</param>
        /// <returns>Contract balance information</returns>
        public async Task<ContractBalanceInfo> GetContractBalanceAsync(int contractId, int productId)
        {
            // Get contract product
            var contractProduct = await _db.P_CONTRACT_PRODUCTS
                .Include(cp => cp.PRODUCT)
                .Include(cp => cp.PRODUCT.PRODUCT_CATEGORIES)
                .Include(cp => cp.UNIT)
                .FirstOrDefaultAsync(cp => cp.ContractID == contractId && cp.ProductID == productId);

            if (contractProduct == null)
            {
                return new ContractBalanceInfo
                {
                    ContractQuantity = 0,
                    ContractQuantityCBM = 0,
                    InvoicedQuantity = 0,
                    InvoicedQuantityCBM = 0,
                    RemainingQuantity = 0,
                    RemainingQuantityCBM = 0,
                    FulfillmentPercentage = 0
                };
            }

            // Check if product requires cutting
            bool requiresCutting = await _conversionService.RequiresCuttingAsync(productId);
            
            // Check if product should use weight-based calculations
            bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(productId);

            // Contract quantity (already in CBM for cutting products, or in contract unit for others, or kg for weight-based)
            decimal contractQty = (decimal)contractProduct.Quantity;
            decimal contractQtyCBM = contractQty;

            // If product uses weight-based calculations, contract is in kg
            if (calculateByWeight)
            {
                // Contract quantity is already in kg
                contractQtyCBM = contractQty; // Use same value for consistency
            }
            // If contract is in CBM and product requires cutting, contractQty is already in CBM
            // Kesiladigan mahsulotlar uchun shartnoma miqdori CBM'da deb olinadi (UnitID = 11, m3).

            // Get all invoices linked to orders that are linked to this contract
            // FIN-B28: o'chirilgan invoyslar hisobga olinmaydi; buyurtmasiz (to'g'ridan-to'g'ri shartnomaga) invoyslar ham kiradi.
            var allInvoiceProducts = await _db.P_INVOICES
                .Where(pi => pi.IsDeleted != true &&
                             (pi.ContractID == contractId || (pi.P_ORDERS != null && pi.P_ORDERS.ContractID == contractId)))
                .SelectMany(pi => pi.P_INVOICE_PRODUCTS)
                .Where(pp => pp.ProductID == productId)
                .ToListAsync();

            // Calculate invoiced quantities
            decimal invoicedQty = 0;
            decimal invoicedQtyCBM = 0;

            foreach (var invoiceProduct in allInvoiceProducts)
            {
                decimal qty = (decimal)invoiceProduct.Quantity;
                invoicedQty += qty;

                // Convert based on calculation type
                if (calculateByWeight)
                {
                    // Invoice quantity should already be in kg for weight-based products
                    invoicedQtyCBM += qty; // kg
                }
                else if (requiresCutting)
                {
                    // Invoice quantity is in pieces, convert to CBM
                    try
                    {
                        decimal cbm = await _conversionService.ConvertPieceToCBMAsync(productId, qty);
                        invoicedQtyCBM += cbm;
                    }
                    catch
                    {
                        // If conversion fails, use quantity as-is
                        invoicedQtyCBM += qty;
                    }
                }
                else
                {
                    invoicedQtyCBM += qty; // For non-cutting products, no conversion needed
                }
            }

            // Calculate remaining quantities
            decimal remainingQty = contractQty - invoicedQty;
            decimal remainingQtyCBM = contractQtyCBM - invoicedQtyCBM;

            // Calculate fulfillment percentage
            decimal fulfillmentPercentage = contractQtyCBM > 0
                ? (invoicedQtyCBM / contractQtyCBM) * 100
                : 0;

            return new ContractBalanceInfo
            {
                ContractQuantity = contractQty,
                ContractQuantityCBM = contractQtyCBM,
                InvoicedQuantity = invoicedQty,
                InvoicedQuantityCBM = invoicedQtyCBM,
                RemainingQuantity = remainingQty,
                RemainingQuantityCBM = remainingQtyCBM,
                FulfillmentPercentage = fulfillmentPercentage
            };
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
