# Logistics Module - Deployment Guide

## ✅ Step 1: Database Migration - COMPLETED

All database extensions have been successfully applied:
- ✅ 24 new columns added to F_WAYBILLS
- ✅ PaymentDate added to F_WAYBILL_PAYMENTS
- ✅ F_CONTRACT_TARIFFS table created
- ✅ F_WAYBILL_PAYMENT_SPLITS table created
- ✅ All foreign key constraints added
- ✅ Performance indexes created

## 📋 Step 2: Update Entity Framework Model

### Option A: Using Visual Studio (Recommended)
1. Open your solution in Visual Studio
2. Navigate to `Models/DBTHSNModel.edmx`
3. Right-click on the design surface → **"Update Model from Database"**
4. In the wizard:
   - Select **"Refresh"** tab
   - Check all tables: `F_WAYBILLS`, `F_WAYBILL_PAYMENTS`, `F_CONTRACT_TARIFFS`, `F_WAYBILL_PAYMENT_SPLITS`
   - Click **"Finish"**
5. **Save** the .edmx file
6. **Rebuild** the solution (Build → Rebuild Solution)

### Option B: Using Package Manager Console
```powershell
# If using Code First Migrations (if applicable)
Update-Database
```

### Verify Model Update
After updating, verify that:
- `F_WAYBILLS` model has all new properties
- `F_WAYBILL_PAYMENTS` has `PaymentDate` property
- Navigation properties are correctly set

## 📋 Step 3: Register Permissions

Run the permissions seed script:

```sql
-- Execute: Scripts/Logistics_Permissions_Seed.sql
```

This will register all Logistics module permissions in PERMISSIONMODULES table.

**Verify Permissions:**
1. Go to Admin Panel → Permissions
2. Check that all Logistics controllers are listed:
   - Forwarder (Index, Create, Edit, Delete, Details)
   - FTransportType (Index, Create, Edit, Delete, Details)
   - FContract (Index, Create, Edit, Delete, Details)
   - Waybill (Index, Create, Edit, Delete, Details)
   - WaybillPayment (Index, Create, Edit, Delete, Details)
   - LogisticsDashboard (Index, LanePerformance, Reconciliation, ForwarderComparison)

## 📋 Step 4: Assign Permissions to Roles

1. Go to **Admin Panel → Roles**
2. Select a role (e.g., Admin, Logistics Manager)
3. Go to **Permissions** tab
4. Enable appropriate permissions for Logistics module:
   - **ViewPermit**: Allow viewing
   - **ChangePermit**: Allow create/edit
   - **DeletePermit**: Allow delete (if needed)

## 🧪 Step 5: Testing Checklist

### Test 1: Forwarder Management
- [ ] Navigate to `/Forwarder/Index`
- [ ] Create a new forwarder
- [ ] View forwarder details (check statistics appear)
- [ ] Edit forwarder
- [ ] Verify statistics calculate correctly

### Test 2: Waybill Management
- [ ] Navigate to `/Waybill/Index`
- [ ] Test filters (direction, forwarder, date range)
- [ ] Create a new waybill with all fields:
  - [ ] Select direction (Inbound/Outbound/Internal)
  - [ ] Enter FromLocation and ToLocation
  - [ ] Enter DistanceKm
  - [ ] Select FreightBasis and Rate
  - [ ] Verify Amount auto-calculates
  - [ ] Enter vehicle and driver information
- [ ] View waybill details
- [ ] Edit waybill
- [ ] Test payment status display

### Test 3: Waybill Payments
- [ ] Navigate to `/WaybillPayment/Index`
- [ ] Create a payment for a waybill:
  - [ ] Select waybill
  - [ ] Enter PaymentDate
  - [ ] Enter Payment_Amount
  - [ ] Select Account (for finance posting)
- [ ] Verify payment appears in waybill details
- [ ] Check FIN_TRANSACTIONS table for posted transaction

### Test 4: Finance Integration
- [ ] Create a waybill payment
- [ ] Verify FIN_TRANSACTIONS record created:
  ```sql
  SELECT * FROM FIN_TRANSACTIONS 
  WHERE SourceDocType = 'WaybillPayment' 
  ORDER BY CreatedDT DESC
  ```
- [ ] Check:
  - [ ] Direction = 2 (Outgoing)
  - [ ] CounterpartyType = 4 (Forwarder)
  - [ ] Purpose contains "Logistics / freight payment"
  - [ ] AccountID is correct

### Test 5: Dashboard
- [ ] Navigate to `/LogisticsDashboard/Index`
- [ ] Verify KPIs display correctly:
  - [ ] Total Cost
  - [ ] Total Trips
  - [ ] Cost per Ton
  - [ ] Unpaid Amount
- [ ] Test date range filter
- [ ] Verify forwarder statistics table
- [ ] Verify lane performance table

### Test 6: Permissions
- [ ] Test with user without ViewPermit (should redirect to ViewNotAuthorized)
- [ ] Test with user without ChangePermit (should not see Create/Edit buttons)
- [ ] Test with user with full permissions (should see all features)

## 🔧 Troubleshooting

### Issue: Entity Framework model not updating
**Solution:**
1. Close Visual Studio
2. Delete `bin` and `obj` folders
3. Reopen Visual Studio
4. Update model again
5. Rebuild solution

### Issue: Properties not appearing in model
**Solution:**
1. Check that columns were actually created in database:
   ```sql
   SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
   WHERE TABLE_NAME = 'F_WAYBILLS'
   ```
2. If columns exist, try deleting and re-adding the table in EF model

### Issue: Foreign key errors
**Solution:**
1. Verify foreign key constraints exist:
   ```sql
   SELECT * FROM sys.foreign_keys 
   WHERE name LIKE 'FK_F_WAYBILLS%'
   ```
2. Check that referenced tables have data (if needed)

### Issue: Permissions not working
**Solution:**
1. Verify permissions script ran successfully
2. Check PERMISSIONMODULES table:
   ```sql
   SELECT * FROM PERMISSIONMODULES 
   WHERE Module = 'Logistics'
   ```
3. Clear cache (RoleHelper uses caching):
   - Restart application
   - Or clear cache programmatically

### Issue: Finance posting not working
**Solution:**
1. Verify FIN_ACCOUNTS has cash/bank accounts:
   ```sql
   SELECT * FROM FIN_ACCOUNTS 
   WHERE (IsCash = 1 OR IsBank = 1) AND IsActive = 1
   ```
2. Verify FIN_CATEGORIES has transport category:
   ```sql
   SELECT * FROM FIN_CATEGORIES 
   WHERE Name LIKE '%Transport%' AND Type = 2
   ```
3. Check application logs for errors

## 📊 Verification Queries

### Check Database Structure
```sql
-- Verify F_WAYBILLS columns
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'F_WAYBILLS'
ORDER BY ORDINAL_POSITION;

-- Verify indexes
SELECT name, type_desc 
FROM sys.indexes 
WHERE object_id = OBJECT_ID('F_WAYBILLS');

-- Verify foreign keys
SELECT 
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) = 'F_WAYBILLS';
```

### Check Permissions
```sql
-- Verify permissions registered
SELECT * FROM PERMISSIONMODULES 
WHERE Module = 'Logistics'
ORDER BY Controller, Action;

-- Check role permissions
SELECT 
    r.RName,
    pm.Controller,
    pm.Action,
    p.ViewPermit,
    p.ChangePermit,
    p.DeletePermit
FROM PERMISSIONS p
INNER JOIN PERMISSIONMODULES pm ON p.PermissionModuleID = pm.ID
INNER JOIN ROLES r ON p.RoleID = r.ID
WHERE pm.Module = 'Logistics'
ORDER BY r.RName, pm.Controller, pm.Action;
```

## ✅ Success Criteria

The module is successfully deployed when:
- ✅ All database columns and tables exist
- ✅ Entity Framework model is updated
- ✅ Permissions are registered and assigned
- ✅ All CRUD operations work
- ✅ Finance integration posts transactions correctly
- ✅ Dashboard displays KPIs correctly
- ✅ Filters and search work properly

## 🎯 Next Steps After Deployment

1. **User Training:**
   - Train users on waybill creation
   - Explain filtering and search features
   - Show dashboard and analytics

2. **Data Migration (if needed):**
   - Import existing waybill data from Excel
   - Map old data to new structure

3. **Optional Enhancements:**
   - Add tariff management to contracts
   - Implement document upload
   - Create additional reports

---

**Status:** Database migration complete. Proceed with EF model update and testing.
