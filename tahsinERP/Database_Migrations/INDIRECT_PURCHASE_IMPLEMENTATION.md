# Indirect Purchase Implementation Guide

## Overview
This implementation extends the SRM (Supplier Relationship Management) module to support indirect purchases in addition to direct purchases. Indirect purchases include equipment, tools, consumables, and auxiliary products that are not part of the BOM (Bill of Materials).

## Database Migrations

### Step 1: Run Database Migrations
Execute the following SQL scripts in order:

1. **IndirectPurchase_AddPurchaseType.sql**
   - Adds `PurchaseType` field to P_CONTRACTS, P_ORDERS, P_INVOICES
   - Adds `RequisitionID`, `DepartmentID`, `RequesterID`, `ContactPhone`, `Purpose` to P_ORDERS
   - Adds status tracking fields (`Status`, `ContractStatus`, `InvoiceStatus`, `WarehouseStatus`) to P_ORDERS
   - Adds `Priority` and `RequiredDate` to P_ORDER_PRODUCTS

2. **IndirectPurchase_CreateRequisitionsTable.sql**
   - Creates P_REQUISITIONS table for purchase requisition forms
   - Creates P_REQUISITION_PRODUCTS table
   - Creates P_REQUISITION_APPROVALS table for approval workflow
   - Adds foreign key constraints and indexes

3. **IndirectPurchase_PermissionModules.sql**
   - Adds permission modules for PRequisitions controller
   - Adds permission modules for PurchaseOrderTracking controller

### Step 2: Update Entity Framework Model
After running the migrations:
1. Open the EDMX file in Visual Studio
2. Right-click and select "Update Model from Database"
3. Select the new tables and columns
4. Save and regenerate the model

## New Features

### 1. Purchase Requisitions (PRequisitions)
- **Purpose**: Employees can create purchase requisition forms for indirect purchases
- **Workflow**: Draft → Submitted → Approved → Converted to Order
- **Approval Process**: Multi-level approval (Requester → Department Head → Financier → Purchase Manager)
- **Location**: `Controllers/SRM/PRequisitionsController.cs`

### 2. Indirect Purchase Orders
- **Purpose**: Purchase orders can now be created from requisitions or manually for indirect purchases
- **Features**:
  - Link to purchase requisitions
  - Support for equipment, tools, consumables, auxiliary products
  - Priority and required date tracking
  - Department and requester information
- **Location**: `Controllers/SRM/POrdersController.cs` (updated)

### 3. Indirect Purchase Contracts
- **Purpose**: Contracts can be created for indirect purchases
- **Features**:
  - PurchaseType field distinguishes Direct (0) vs Indirect (1)
  - Product filtering based on purchase type
- **Location**: `Controllers/SRM/PContractsController.cs` (updated)

### 4. Indirect Purchase Invoices
- **Purpose**: Invoices support indirect purchases
- **Features**:
  - PurchaseType automatically inherited from order
  - Product filtering based on purchase type
- **Location**: `Controllers/SRM/PInvoicesController.cs` (updated)

### 5. Purchase Order Tracking
- **Purpose**: Visual tracking of purchase order status
- **Status Flow**: 
  - Order Created (Green) → 
  - Contract Created (Green/Red) → 
  - Invoice Created (Green/Red) → 
  - Warehouse Received (Green/Red)
- **Location**: `Controllers/SRM/PurchaseOrderTrackingController.cs`

## Product Categories

Indirect purchase products should be categorized in `PRODUCT_CATEGORIES` with:
- `IsConsumable = true` OR
- `AllowPurchase = true`
- `IsFinishedGood = false`

Categories for indirect purchases:
- Equipment (Jihozlar)
- Tools (Dastgohlar)
- Consumables (Sarf mahsulotlar)
- Auxiliary Products (Yordamchilar)

## Usage Flow

### Indirect Purchase Flow:
1. **Employee creates Purchase Requisition**
   - Fills out requisition form with items, quantities, priorities, required dates
   - Submits for approval

2. **Approval Process**
   - Department Head approves
   - Financier approves
   - Purchase Manager approves

3. **Convert to Purchase Order**
   - Approved requisition is converted to purchase order
   - Purchase order is created with PurchaseType = 1 (Indirect)

4. **Create Contract** (Optional)
   - Contract can be created from purchase order
   - PurchaseType = 1 (Indirect)

5. **Create Invoice**
   - Invoice created from purchase order
   - PurchaseType automatically set to 1 (Indirect)

6. **Warehouse Receipt**
   - Income document created when goods arrive
   - Links to invoice

7. **Tracking**
   - View order status in PurchaseOrderTracking
   - See visual status indicators (green/red) for each stage

## API Endpoints

### Purchase Requisitions
- `GET /PRequisitions` - List all requisitions
- `GET /PRequisitions/Create` - Create new requisition
- `POST /PRequisitions/Create` - Submit requisition
- `GET /PRequisitions/Details/{id}` - View requisition details
- `GET /PRequisitions/ConvertToOrder/{id}` - Convert requisition to order

### Purchase Orders
- `POST /POrders/LoadRequisitionProducts` - Load products from requisition
- Updated `POST /POrders/Create` - Now supports requisitionId parameter

### Purchase Order Tracking
- `GET /PurchaseOrderTracking` - View all orders with tracking status
- `GET /PurchaseOrderTracking/Details/{id}` - View detailed tracking for an order

## Permission Modules

New permission modules added:
- `PRequisitions` - Index, Create, Edit, Delete, Approve, ConvertToOrder
- `PurchaseOrderTracking` - Index, Details

## Notes

1. **Direct vs Indirect Purchase**:
   - Direct Purchase (PurchaseType = 0): BOM materials, raw materials
   - Indirect Purchase (PurchaseType = 1): Equipment, tools, consumables, auxiliary products

2. **Product Filtering**:
   - Direct purchases: Products where `IsFinishedGood = false`
   - Indirect purchases: Products where `IsConsumable = true` OR `AllowPurchase = true`

3. **Status Tracking**:
   - Order status is automatically updated when contracts, invoices, or warehouse receipts are created
   - Visual indicators (green/red) show completion status at each stage

4. **Database Fields**:
   - All new fields have been added via migrations
   - Models will be auto-generated after EDMX update
   - Manual model classes created for P_REQUISITIONS tables

## Testing Checklist

- [ ] Run all database migrations
- [ ] Update EDMX model
- [ ] Test creating purchase requisition
- [ ] Test approval workflow
- [ ] Test converting requisition to order
- [ ] Test creating indirect purchase order manually
- [ ] Test creating contract for indirect purchase
- [ ] Test creating invoice for indirect purchase
- [ ] Test warehouse receipt for indirect purchase
- [ ] Test purchase order tracking view
- [ ] Verify product filtering works correctly
- [ ] Test permissions

## Future Enhancements

- Email notifications for requisition approvals
- Dashboard widgets for pending approvals
- Reporting for indirect purchases
- Budget tracking for indirect purchases
- Integration with expense management

