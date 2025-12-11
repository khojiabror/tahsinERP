# HR Module - Next Steps After SQL Script Execution

## ✅ Completed
- SQL script executed - All 5 new HR tables created in database

## 🔧 Next Steps

### 1. Update Entity Framework Model (EDMX) - **CRITICAL**

Since this project uses **Database First** approach, you need to update the EDMX model:

1. **Open Visual Studio**
2. **Open `Models/DBTHSNModel.edmx`** in the designer
3. **Right-click on the designer surface** → Select **"Update Model from Database..."**
4. **In the Update Wizard:**
   - Go to the **"Refresh"** tab
   - Expand **"Tables"**
   - Check the following new tables:
     - ✅ `HR_EMPLOYEES`
     - ✅ `HR_TIMESHEET_HEADERS`
     - ✅ `HR_TIMESHEET_LINES`
     - ✅ `HR_PAYROLL_HEADERS`
     - ✅ `HR_PAYROLL_LINES`
   - Click **"Finish"**
5. **Save the EDMX file**
6. **Let Visual Studio regenerate** the model classes

**Important:** After updating EDMX:
- The partial classes in `Models/HR_*.cs` will be replaced by auto-generated classes
- The `Models/DBTHSNModel.Context.HR.cs` file can be deleted (DbSets will be auto-generated)
- Verify that `Models/DBTHSNModel.Context.cs` now includes:
  ```csharp
  public virtual DbSet<HR_EMPLOYEES> HR_EMPLOYEES { get; set; }
  public virtual DbSet<HR_TIMESHEET_HEADERS> HR_TIMESHEET_HEADERS { get; set; }
  public virtual DbSet<HR_TIMESHEET_LINES> HR_TIMESHEET_LINES { get; set; }
  public virtual DbSet<HR_PAYROLL_HEADERS> HR_PAYROLL_HEADERS { get; set; }
  public virtual DbSet<HR_PAYROLL_LINES> HR_PAYROLL_LINES { get; set; }
  ```

### 2. Add HR Permissions to Database

Add the following permissions to the `PERMISSIONS` table:

```sql
-- Add HR Module Permissions
INSERT INTO PERMISSIONS (ModuleName, PermissionName, Description, IsActive, CreatedDT)
VALUES 
('HR', 'HrDashboard', 'HR Dashboard - View', 1, GETDATE()),
('HR', 'HrOrgUnits', 'Organizational Structure - View/Edit', 1, GETDATE()),
('HR', 'HrEmployees', 'Employees - View/Edit', 1, GETDATE()),
('HR', 'HrSalaryGrades', 'Salary Grades - View/Edit', 1, GETDATE()),
('HR', 'HrStaffingPlan', 'Staffing Plan - View/Edit', 1, GETDATE()),
('HR', 'HrTimesheets', 'Timesheets - View/Edit', 1, GETDATE()),
('HR', 'HrPayroll', 'Payroll - View/Edit', 1, GETDATE());
```

**Or** add them through the Admin Panel if your system has a UI for permissions.

### 3. Assign Permissions to Roles

Assign the HR permissions to appropriate roles through:
- Admin Panel → Permissions
- Or directly in database via `ROLE_PERMISSIONS` table

### 4. Build and Test

1. **Build the solution** (should compile without errors now)
2. **Test each module:**
   - Navigate to HR menu items
   - Test organizational structure tree
   - Create test employees
   - Create test timesheet
   - Generate test payroll
   - Test Excel import/export

### 5. Configure Default Values (Optional)

Set up default values for:
- **Payroll Posting**: Default AccountID and CategoryID for payroll transactions
- **Currency**: Default currency for salary calculations
- **Working Hours**: Standard working hours per day (default: 8)

## 🔍 Verification Checklist

After EDMX update, verify:

- [ ] All 5 new tables appear in `DBTHSNModel.edmx` designer
- [ ] DbContext includes all 5 new DbSets
- [ ] Project compiles without errors
- [ ] HR menu items appear in navigation (if permissions are set)
- [ ] Can access HR Dashboard
- [ ] Can create organizational units
- [ ] Can create employees
- [ ] Can create timesheets
- [ ] Can generate payroll
- [ ] Excel import/export works

## 🐛 Troubleshooting

### If EDMX update fails:
- Check that all foreign key constraints are properly created
- Verify table names match exactly (case-sensitive)
- Check SQL script executed completely without errors

### If controllers can't find entities:
- Rebuild solution after EDMX update
- Check that namespace matches: `tahsinERP.Models`
- Verify DbSets are in `DBTHSNModel.Context.cs`

### If views show errors:
- Ensure ViewModels are in `ViewModels/HR/` folder
- Check that controllers have `using tahsinERP.ViewModels.HR;`
- Rebuild solution

## 📝 Notes

- The partial model classes (`Models/HR_*.cs`) are temporary and will be replaced by EDMX-generated classes
- After EDMX update, you can delete `Models/DBTHSNModel.Context.HR.cs` if DbSets are auto-generated
- All views and controllers are ready - just need EDMX update to work

