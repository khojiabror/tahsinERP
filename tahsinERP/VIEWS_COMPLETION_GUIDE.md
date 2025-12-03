# Views Completion Guide

## Completed Views ✅

1. **Customer**
   - ✅ Index.cshtml (with pagination/filtering)
   - ✅ Details.cshtml (with navigation links)

2. **SOrder**
   - ✅ Index.cshtml (with pagination/filtering)
   - ✅ Details.cshtml (with delivery schedule summary)
   - ✅ DeliverySchedule.cshtml (full delivery schedule management)
   - ✅ Create.cshtml (already exists)
   - ✅ Edit.cshtml (needs update to match new structure)

3. **SalesInvoices**
   - ✅ Index.cshtml (with pagination/filtering)

## Views Still Needed

### SalesInvoices (High Priority)
- [ ] Details.cshtml - Show invoice details, lines, navigation to SF/TTNK
- [ ] CreateFromOrder.cshtml - Create invoice from order with pre-filled lines
- [ ] Create.cshtml - Standalone invoice creation
- [ ] Edit.cshtml - Edit invoice (if status allows)

### SalesSF (High Priority)
- [ ] Index.cshtml - List SF with filtering
- [ ] Details.cshtml - Show SF details, lines, navigation
- [ ] GenerateFromInvoice.cshtml - Generate SF from invoice
- [ ] Edit.cshtml - Edit SF (if status allows)

### SalesTTNK (High Priority)
- [ ] Index.cshtml - List TTNK with filtering
- [ ] Details.cshtml - Show TTNK details, logistics info, navigation
- [ ] CreateFromInvoices.cshtml - Create TTNK from one or multiple invoices
- [ ] Edit.cshtml - Edit TTNK logistics fields

### SContract (Medium Priority)
- [ ] Index.cshtml - Update with pagination/filtering (controller ready)
- [ ] Details.cshtml - Update with order linking and totals (controller ready)
- [ ] ManageOrders.cshtml - Link/unlink orders to contract (NEW)

### SOrder (Low Priority - mostly done)
- [ ] Edit.cshtml - May need minor updates

## View Templates Pattern

All views should follow this structure:

```razor
@model [EntityType]
@{
    ViewBag.Title = "[Title]";
    Layout = "~/Views/Shared/_Layout.cshtml";
}

@if (tahsinERP.Models.RoleHelper.IsViewPermitted(...))
{
    bool changePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(...);
    
    <div class="app-content content">
        <!-- Breadcrumbs -->
        <!-- Content with navigation links -->
        <!-- Master-detail sections -->
        <!-- Action buttons -->
    </div>
}
```

## Key Features for Each View Type

### Details Views Should Include:
1. Master record information
2. Related detail records (products, lines, etc.)
3. Navigation links to related documents
4. Action buttons (Create SF, Create TTNK, etc.)
5. Summary/totals section

### Index Views Should Include:
1. Search and filter controls
2. Pagination
3. Sorting
4. Action column (Edit, Delete)
5. Links to Details

### Create/Edit Views Should Include:
1. Form with validation
2. Master fields
3. Detail lines management (add/edit/delete)
4. Business rule validation messages
5. Navigation back to source document

## Navigation Links Pattern

All Details views should have navigation section:

```razor
<div class="row mt-3">
    <div class="col-12">
        <h5 class="text-primary mb-3">Bog'liq hujjatlar</h5>
        <div class="btn-group" role="group">
            <a href="..." class="btn btn-outline-primary">
                <i class="bx bx-[icon]"></i> [Label] (@count)
            </a>
        </div>
    </div>
</div>
```

## Next Steps

1. Create SalesInvoices/Details.cshtml
2. Create SalesInvoices/CreateFromOrder.cshtml
3. Create SalesSF/Index.cshtml and Details.cshtml
4. Create SalesSF/GenerateFromInvoice.cshtml
5. Create SalesTTNK/Index.cshtml and Details.cshtml
6. Create SalesTTNK/CreateFromInvoices.cshtml
7. Update SContract views
8. Test all navigation flows

