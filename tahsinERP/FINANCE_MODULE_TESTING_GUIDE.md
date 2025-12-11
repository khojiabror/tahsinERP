# Finance Module Testing Guide

## Overview
This guide provides step-by-step instructions for testing the Finance module end-to-end functionality.

## Prerequisites
1. Ensure you have appropriate permissions:
   - `FinAccounts` (View and Change)
   - `FinTransactions` (View and Change)
   - `FinCategories` (View and Change)
   - `FinDashboard` (View)
   - `Shareholder` (View and Change)
   - `SalesInvoices` (Change)
   - `PInvoices` (Change)
   - `WaybillPayment` (Change)

2. Database should have:
   - At least one active Currency
   - Sample Customers and Suppliers
   - Sample Sales Orders and Purchase Orders

## Test Scenarios

### 1. Master Data Setup

#### 1.1 Test FinAccounts CRUD
**Steps:**
1. Navigate to: Finance → Moliya boshqaruvi → Hisoblar
2. Click "Hisob qo'shish"
3. Create a new account:
   - Code: `CASH_UZS_001`
   - Name: `Naqd pul - UZS`
   - Currency: Select UZS
   - Check "Naqd pul"
   - Click "Saqlash"
4. Verify account appears in list
5. Click on account to view Details
6. Click "Tahrirlash" and modify name
7. Click "O'chirish" and verify soft delete (IsActive = false)

**Expected Results:**
- Account created successfully
- Can view, edit, and soft delete
- Code uniqueness validation works
- Currency validation works

#### 1.2 Test FinCategories CRUD
**Steps:**
1. Navigate to: Finance → Moliya boshqaruvi → Kategoriyalar
2. Create parent category:
   - Name: `Sotuv daromadi`
   - Type: `Daromad`
   - Click "Saqlash"
3. Create child category:
   - Name: `Mahsulot sotuvi`
   - Type: `Daromad`
   - Parent: Select "Sotuv daromadi"
   - Click "Saqlash"
4. Verify tree structure displays correctly
5. Test Edit and Delete

**Expected Results:**
- Categories display in tree structure
- Parent-child relationships work
- Circular reference prevention works

#### 1.3 Test FinBalanceCategories
**Steps:**
1. Navigate to: Finance → Moliya boshqaruvi → Balans kategoriyalari
2. Create categories:
   - Aktiv: `Naqd pul va bank`
   - Passiv: `Kreditorlar qarzi`
   - Kapital: `Aksiyador kapitali`
3. Verify tree structure

**Expected Results:**
- Categories created with correct types
- Tree structure displays

### 2. Transaction Management

#### 2.1 Test Manual Transaction Creation
**Steps:**
1. Navigate to: Finance → Moliya boshqaruvi → Tranzaksiyalar
2. Click "Tranzaksiya qo'shish"
3. Create transaction:
   - Date: Today
   - Account: Select cash account created earlier
   - Category: Select income category
   - Direction: `Kirim`
   - Amount: `1000000`
   - Currency: Should auto-fill from account
   - Purpose: `Test transaction`
   - Click "Saqlash"
4. Verify transaction appears in list
5. Test filtering:
   - Filter by date range
   - Filter by account
   - Filter by category
   - Filter by direction
6. Test Excel export

**Expected Results:**
- Transaction created successfully
- Appears in filtered lists
- Excel export works
- Currency matches account currency

#### 2.2 Test Transaction Edit/Delete
**Steps:**
1. Find a manual transaction (SourceDocType = "Manual")
2. Click Edit
3. Modify amount and save
4. Verify changes
5. Click Delete
6. Verify deletion

**Expected Results:**
- Manual transactions can be edited/deleted
- System-posted transactions cannot be edited/deleted

### 3. Automatic Posting Integration

#### 3.1 Test Sales Invoice Posting
**Steps:**
1. Navigate to: CRM → Sales Invoices
2. Create a new Sales Invoice:
   - Customer: Select any customer
   - Add products and lines
   - Status: Set to `FINALIZED` or `APPROVED`
   - Save invoice
3. Navigate to: Finance → Tranzaksiyalar
4. Filter by:
   - SourceDocType: `SalesInvoice`
   - Date: Today
5. Verify transaction created:
   - Direction: `Kirim`
   - Amount: Matches invoice total
   - Account: Receivables account (auto-created if needed)
   - Category: Sales revenue category
   - SourceDocType: `SalesInvoice`
   - SourceDocID: Invoice ID
   - Counterparty: Customer name

**Expected Results:**
- Transaction automatically created when invoice finalized
- Transaction links back to invoice
- Receivables account created if missing
- Sales revenue category created if missing

#### 3.2 Test Purchase Invoice Posting
**Steps:**
1. Navigate to: SRM → PInvoices
2. Create a new Purchase Invoice:
   - Supplier: Select any supplier
   - Add products
   - Save invoice
3. Navigate to: Finance → Tranzaksiyalar
4. Filter by:
   - SourceDocType: `PurchaseInvoice`
   - Date: Today
5. Verify transaction created:
   - Direction: `Chiqim`
   - Amount: Matches invoice total
   - Account: Payables account
   - Category: Purchase expense category
   - Counterparty: Supplier name

**Expected Results:**
- Transaction automatically created when invoice saved
- Payables account created if missing
- Purchase expense category created if missing

#### 3.3 Test Waybill Payment Posting
**Steps:**
1. Navigate to: Logistic → WaybillPayment
2. Create a new Waybill Payment:
   - Select waybill
   - Enter amount
   - Select currency
   - Save
3. Navigate to: Finance → Tranzaksiyalar
4. Filter by:
   - SourceDocType: `WaybillPayment`
   - Date: Today
5. Verify transaction created:
   - Direction: `Chiqim`
   - Category: Transport expenses
   - Counterparty: Forwarder name

**Expected Results:**
- Transaction automatically created
- Transport expense category created if missing

### 4. Shareholder Module

#### 4.1 Test Shareholder CRUD
**Steps:**
1. Navigate to: Finance → Aksiyadorlar → Aksiyadorlar ro'yxati
2. Create shareholder:
   - Full Name: `Test Shareholder`
   - Phone: `+998901234567`
   - Notes: `Test notes`
   - Click "Saqlash"
3. Verify shareholder appears
4. Test Edit and Delete

**Expected Results:**
- Shareholder CRUD works
- Soft delete (IsActive flag)

#### 4.2 Test Share Period Management
**Steps:**
1. Open shareholder Details
2. Click "Ulushlarni boshqarish" icon
3. Add share period:
   - FromDate: `2024-01-01`
   - ToDate: `2024-12-31` (or leave null for open-ended)
   - SharePercent: `50`
   - Save
4. Add another shareholder with 50% for same period
5. Try to add third shareholder with 1% - should fail (exceeds 100%)

**Expected Results:**
- Share periods can be added
- Total share validation works (cannot exceed 100%)
- Open-ended periods work (ToDate = null)

#### 4.3 Test Shareholder Contributions
**Steps:**
1. Open shareholder Details
2. Click "Hissa qo'shish" icon
3. Add cash contribution:
   - Date: Today
   - Amount: `5000000`
   - Currency: UZS
   - Type: `Naqd pul`
   - Description: `Initial contribution`
   - Save
4. Navigate to: Finance → Tranzaksiyalar
5. Verify transaction created:
   - SourceDocType: `ShareholderContribution`
   - CounterpartyType: `Aksiyador`
   - Purpose: `Aksiyador hissasi`

**Expected Results:**
- Contribution recorded
- Financial transaction created for cash contributions
- Transaction links to contribution

### 5. Dashboard Testing

#### 5.1 Test Cashflow Dashboard
**Steps:**
1. Navigate to: Finance → Hisobotlar → Kassa oqimi
2. Set date range: Last month to today
3. Verify:
   - KPI tiles show: Inflow, Outflow, Net cashflow, Current balance
   - Daily cashflow chart displays
   - Filter by account works
4. Verify calculations match transaction totals

**Expected Results:**
- KPIs display correctly
- Chart renders
- Calculations accurate
- Filters work

#### 5.2 Test P&L Report
**Steps:**
1. Navigate to: Finance → Hisobotlar → Foyda va zarar
2. Select Year and Month
3. Verify:
   - Hierarchical P&L structure displays
   - Actual vs Plan comparison
   - Variance calculations
   - Items without plans show 0 plan

**Expected Results:**
- P&L structure displays correctly
- Actual amounts calculated from transactions
- Plan amounts from FIN_PL_PLANS
- Variance = Actual - Plan

#### 5.3 Test Balance Sheet
**Steps:**
1. Navigate to: Finance → Hisobotlar → Balans
2. Select "As of Date"
3. Verify:
   - Balance categories display
   - Amounts shown per category
   - Hierarchical structure

**Expected Results:**
- Balance sheet displays
- Uses FIN_BALANCE_SNAPSHOTS if available
- Categories in correct order

#### 5.4 Test Shareholder Value
**Steps:**
1. Navigate to: Finance → Aksiyadorlar → Kompaniya qiymati
2. Select "As of Date"
3. Verify:
   - Total company value calculated
   - Each shareholder's share percentage
   - Each shareholder's monetary value
   - Values = Company Value × Share Percent / 100

**Expected Results:**
- Company value calculated correctly
- Shareholder values calculated correctly
- Values update based on date

### 6. Integration Testing

#### 6.1 Test Idempotency
**Steps:**
1. Create a Sales Invoice with FINALIZED status
2. Verify transaction created
3. Edit invoice (change status to DRAFT then back to FINALIZED)
4. Verify only ONE transaction exists (not duplicated)

**Expected Results:**
- No duplicate transactions
- Posting service checks for existing transactions

#### 6.2 Test Error Handling
**Steps:**
1. Create invoice without currency
2. Verify system handles gracefully
3. Check logs for error messages

**Expected Results:**
- Errors logged but don't break workflow
- User-friendly error messages

### 7. Performance Testing

#### 7.1 Test Large Datasets
**Steps:**
1. Create 1000+ transactions
2. Test list view pagination
3. Test filtering performance
4. Test export performance

**Expected Results:**
- Pagination works smoothly
- Filters respond quickly
- Export completes in reasonable time

### 8. Security Testing

#### 8.1 Test Permissions
**Steps:**
1. Login as user without Finance permissions
2. Try to access Finance pages
3. Verify redirect to "Not Authorized"

**Expected Results:**
- Unauthorized users cannot access
- Proper redirects

#### 8.2 Test Logging
**Steps:**
1. Perform Create, Edit, Delete operations
2. Navigate to: Admin → Log
3. Verify logs created:
   - ControllerName
   - ActionName
   - DateTime
   - UserID
   - IP and MacAddr

**Expected Results:**
- All actions logged
- Logs contain required information

## Common Issues & Solutions

### Issue: Transactions not posting automatically
**Solution:**
- Check invoice status is FINALIZED or APPROVED
- Check FinancePostingService is called
- Check logs for errors
- Verify accounts/categories exist or can be created

### Issue: Share percentage validation fails
**Solution:**
- Check date ranges don't overlap incorrectly
- Verify total shares calculation
- Check for open-ended periods (ToDate = null)

### Issue: Dashboard shows incorrect amounts
**Solution:**
- Verify transactions are posted correctly
- Check category mappings (FIN_PL_ITEM_CATEGORIES)
- Verify date filters
- Check currency conversions if needed

### Issue: Excel export fails
**Solution:**
- Check EPPlus license context
- Verify file permissions
- Check memory for large exports

## Test Checklist

- [ ] FinAccounts CRUD
- [ ] FinCategories CRUD with tree
- [ ] FinBalanceCategories CRUD
- [ ] FinPlItems CRUD
- [ ] FinPlPlans CRUD and BulkEdit
- [ ] FinTransactions CRUD and filtering
- [ ] Shareholder CRUD
- [ ] Share period management
- [ ] Shareholder contributions
- [ ] Sales Invoice automatic posting
- [ ] Purchase Invoice automatic posting
- [ ] Waybill Payment automatic posting
- [ ] Cashflow dashboard
- [ ] P&L report
- [ ] Balance Sheet
- [ ] Shareholder Value dashboard
- [ ] Excel exports
- [ ] Permissions and security
- [ ] Logging
- [ ] Error handling

## Notes

1. **Default Accounts/Categories**: The posting service creates default accounts and categories if they don't exist. Check database after first posting.

2. **Currency Handling**: All transactions must match account currency. Multi-currency support may need enhancement.

3. **Balance Snapshots**: FIN_BALANCE_SNAPSHOTS may need to be generated separately. Consider creating a snapshot generation service.

4. **Inventory Valuation**: Company value calculation for inventory needs PRODUCT_STOCKS integration.

5. **P&L Mapping**: Ensure FIN_PL_ITEM_CATEGORIES mappings are set up correctly for accurate P&L reporting.

## Next Steps After Testing

1. Fix any issues found
2. Complete remaining views if needed
3. Add any missing business validations
4. Performance optimization if needed
5. User training and documentation
