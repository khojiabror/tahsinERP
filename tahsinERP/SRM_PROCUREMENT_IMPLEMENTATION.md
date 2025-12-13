# SRM Module Procurement Logic Implementation

## Overview
This document describes the implementation of procurement logic for the SRM (Supplier Relationship Management) module in tahsinERP, including unit conversions, BOM explosion, and contract balance tracking.

## Services Created

### 1. UnitConversionService
**Location:** `Services/UnitConversionService.cs`

**Purpose:** Handles unit conversions for products that require cutting (`requires_cutting = true`).

**Key Methods:**
- `ConvertMeterToPieceAsync(int productId, decimal meterQuantity)`: Converts meter quantity to pieces using product length
  - Formula: `pieceQty = requiredMeter / (Length / 1000)`
- `ConvertPieceToCBMAsync(int productId, decimal pieceQuantity)`: Converts pieces to cubic meters (CBM)
  - Formula: `cbm = pieces × (L/1000 × W/1000 × H/1000)`
- `RequiresCuttingAsync(int productId)`: Checks if a product requires cutting
- `GetUnitIdAsync(int productId, bool forPieces)`: Gets appropriate unit ID based on product category

**Conversion Rules:**
- Conversions apply ONLY if `product_categories.requires_cutting = true`
- Other products are processed without conversion

### 2. POrderGenerationService
**Location:** `Services/POrderGenerationService.cs`

**Purpose:** Generates Purchase Orders from Sales Orders using BOM explosion.

**Key Methods:**
- `GenerateRequirementsFromSalesOrdersAsync(int[] salesOrderIds)`: 
  - Loads finished goods from Sales Orders
  - Explodes BOM to get raw material requirements
  - Converts meter to piece if needed (for cutting products)
  - Returns list of `RawMaterialRequirement` objects

**Conversion Logic:**
- If BOM unit is METER (UnitID = 5) AND product requires cutting:
  - Converts meter → piece using product length
- Otherwise, uses BOM quantity as-is

### 3. ContractBalanceService
**Location:** `Services/ContractBalanceService.cs`

**Purpose:** Tracks contract balances and fulfillment percentages.

**Key Methods:**
- `GetContractBalanceAsync(int contractId, int productId)`: Gets balance info for a specific product
- `GetAllContractBalancesAsync(int contractId)`: Gets balances for all products in a contract

**Returns:**
- Contract quantity (in CBM for cutting products)
- Invoiced quantity (converted to CBM)
- Remaining quantity
- Fulfillment percentage

## Controller Updates

### 1. POrdersController
**Location:** `Controllers/SRM/POrdersController.cs`

**Changes:**
- Updated `GenerateFromSalesOrders` method to use `POrderGenerationService`
- Now includes conversion logic and calculation explanations
- Returns conversion details in JSON response

**Key Features:**
- BOM explosion from Sales Orders
- Automatic meter-to-piece conversion for cutting products
- Calculation explanations for each material

### 2. PContractsController
**Location:** `Controllers/SRM/PContractsController.cs`

**Changes:**
- Updated `Create` and `Edit` methods to handle unit selection
- For `requires_cutting = true` products: Uses CBM (UnitID = 11 for m³)
- For other products: Uses user-selected unit

**Key Features:**
- Quantity entered in CBM for cutting products
- Price per CBM for cutting products
- User-selected unit for non-cutting products
- No conversion applied (data stored as-is)

### 3. PInvoicesController
**Location:** `Controllers/SRM/PInvoicesController.cs`

**Changes:**
- Updated `Create` and `Edit` methods to handle pieces
- Updated `Details` method to show contract balances and CBM conversions
- For `requires_cutting = true` products: Quantity in PIECES (UnitID = 1)
- Converts PIECES → CBM for display and contract balance calculations

**Key Features:**
- Invoice quantity stored in PIECES for cutting products
- CBM equivalent calculated and displayed
- Contract remaining quantity shown
- Contract fulfillment percentage displayed

### 4. IncomesController
**Location:** `Controllers/SRM/IncomesController.cs`

**Changes:**
- Updated `Create` method to handle pieces
- For `requires_cutting = true` products: Records in PIECES (UnitID = 1)
- Updates `PRODUCT_STOCKS` in PIECES for cutting products

**Key Features:**
- Income recorded in PIECES for cutting products
- `PRODUCT_STOCKS` updated in same unit
- User-selected unit for non-cutting products

## Business Rules Summary

### Purchase Order (POrder)
1. Created from Sales Orders
2. BOM explosion for finished goods
3. **Conversion:** Meter → Piece (if BOM unit = METER and requires_cutting = true)
4. Conversion done in service layer
5. Display includes calculation explanations

### Purchase Contract (PContract)
1. Manual product selection
2. **For cutting products:**
   - Quantity in CBM (m³)
   - Price per CBM
   - UnitID = 11 (m³)
3. **For other products:**
   - User decides unit
   - Quantity and price in selected unit
4. **No conversion** - data stored as-is

### Purchase Invoice (PInvoice)
1. **For cutting products:**
   - Quantity entered in PIECES
   - UnitID = 1 (Piece)
   - **Conversion:** PIECES → CBM for display
2. **For other products:**
   - User decides unit
   - Quantity in selected unit
3. Shows:
   - Invoice quantity (PIECES)
   - Equivalent CBM
   - Contract remaining quantity (CBM)
   - Contract fulfillment percentage

### Income (Warehouse Receipt)
1. **For cutting products:**
   - Recorded in PIECES
   - UnitID = 1 (Piece)
   - `PRODUCT_STOCKS` updated in PIECES
2. **For other products:**
   - User decides unit
   - `PRODUCT_STOCKS` updated in selected unit
3. Reports calculate contract fulfillment in CBM dynamically

## Unit IDs Reference
- **1** = dona (Piece)
- **5** = m (Meter)
- **11** = m³ (Cubic Meter / CBM)

## Database Schema
No schema changes required. All existing tables and fields are used:
- `P_ORDERS`, `P_ORDER_PRODUCTS`
- `P_CONTRACTS`, `P_CONTRACT_PRODUCTS`
- `P_INVOICES`, `P_INVOICE_PRODUCTS`
- `INCOMES`, `INCOME_PRODUCTS`
- `PRODUCT_STOCKS`
- `BOM_HEADERS`, `BOM_LINES`
- `PRODUCTS`, `PRODUCT_CATEGORIES`
- `UNITS`

## Testing Recommendations

1. **POrder Generation:**
   - Test with Sales Orders containing finished goods
   - Verify BOM explosion works correctly
   - Verify meter-to-piece conversion for cutting products
   - Check calculation explanations

2. **PContract Creation:**
   - Test with cutting products (should use CBM)
   - Test with non-cutting products (should use user-selected unit)
   - Verify no conversion is applied

3. **PInvoice Creation:**
   - Test with cutting products (should accept PIECES)
   - Verify CBM conversion for display
   - Check contract balance calculations
   - Verify fulfillment percentage

4. **Income Recording:**
   - Test with cutting products (should record in PIECES)
   - Verify `PRODUCT_STOCKS` updates correctly
   - Test with non-cutting products

## Implementation Status: ✅ COMPLETED

### ViewModels Updated
1. **POrderProductViewModel** - Added:
   - `CalculationExplanation` - Shows how quantity was calculated/converted
   - `IsConverted` - Indicates if conversion was applied
   - `OriginalQuantity` and `OriginalUnitID` - Original values before conversion

2. **PInvoicePartViewModel** (Create & View) - Added:
   - `RequiresCutting` - Indicates if product requires cutting
   - `QuantityCBM` - Calculated CBM equivalent
   - Contract balance fields: `ContractQuantityCBM`, `InvoicedQuantityCBM`, `RemainingQuantityCBM`, `FulfillmentPercentage`

3. **PContractProductViewModel** - Added:
   - `RequiresCutting` - For UI display purposes

4. **WhrsIncomePart** - Added:
   - `RequiresCutting` - For UI display purposes

### Views Updated
1. **POrders/Create.cshtml** - ✅ Updated:
   - Added "Hisob tushuntirishi" (Calculation Explanation) column to products table
   - Displays conversion explanations from BOM explosion
   - Shows how meter quantities were converted to pieces

2. **PInvoices/Details.cshtml** - ✅ Updated:
   - Added "CBM (ekvivalent)" column showing CBM equivalents for cutting products
   - Added "Shartnoma balansi" (Contract Balance) column showing remaining quantities
   - Added "Bajarilish %" (Fulfillment %) column with progress bar
   - Shows contract balance information dynamically
   - Added informational alert explaining CBM calculations

### Next Steps (Optional Enhancements)
1. **PContracts/Create.cshtml** - Add UI hints for CBM input for cutting products
2. **Incomes/Create.cshtml** - Add UI hints for pieces input for cutting products
3. **Reports:** Update contract/inventory reports to show fulfillment in CBM dynamically
4. **Validation:** Add client-side validation for unit selection based on product category

## Notes

- All conversions are handled in the service layer, not controllers
- Transactions are used for stock updates in IncomesController
- Conversion logic is centralized in `UnitConversionService`
- Contract balance calculations are performed dynamically
- System maintains data integrity by storing quantities in appropriate units
