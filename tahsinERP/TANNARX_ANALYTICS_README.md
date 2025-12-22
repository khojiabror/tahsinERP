# TANNARX Analytics Tizimi

## Umumiy Ma'lumot

TANNARX Analytics tizimi Planned (Rejalashtirilgan) va Actual (Haqiqiy) narxlarni solishtirish uchun yaratilgan. Bu tizim tahsinERP ning eng muhim qismi bo'lib, barcha hisob-kitoblar to'g'ri va tez ishlashi kerak.

## Tizim Komponentlari

### 1. Database Layer

#### SQL Views
- **V_TANNARX_ACTUAL_MATERIAL_COSTS**: NORM_INPUT_LINES jadvalidan actual material narxini hisoblaydi
- **V_TANNARX_ACTUAL_MATERIAL_TRANSPORT**: F_WAYBILLS dan material tashuv narxini (bir maxsulot soniga bo'lingan)
- **V_TANNARX_ACTUAL_LABOR_COSTS**: HR_PIECE_RATE_WORK_ENTRIES dan actual ishbay narxini
- **V_TANNARX_ACTUAL_ADMIN_EXPENSES**: HR_PAYROLL_LINES dan soatbaya (administrativ) harajatlar
- **V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT**: F_WAYBILLS dan tayyor maxsulot tashuv narxini

#### Stored Procedure
- **SP_TANNARX_UPDATE_ACTUAL_COSTS**: Barcha actual costs'larni TANNARX jadvaliga yangilaydi

### 2. Service Layer

**TannarxAnalyticsService** - Barcha analytics hisob-kitoblari:
- `UpdateActualCostsAsync()` - Actual costs'larni yangilash
- `GetDashboardDataAsync()` - Dashboard ma'lumotlarini olish
- `GetAnalyticsReportAsync()` - Batafsil hisobot ma'lumotlarini olish

### 3. Controller Layer

**TannarxAnalyticsController**:
- `Dashboard` - Analytics dashboard sahifasi
- `Report` - Batafsil hisobot sahifasi
- `ProductDetails` - Maxsulot tafsilotlari
- `UpdateActualCosts` - Actual costs'larni yangilash (POST)

### 4. View Layer

- **Dashboard.cshtml** - Asosiy analytics dashboard (metrikalar, grafiklar)
- **Report.cshtml** - Batafsil hisobot (product-wise, month-wise)
- **ProductDetails.cshtml** - Maxsulot tafsilotlari

## O'rnatish Qadamlar

### 1. SQL Script'ni ishga tushirish

```sql
-- Scripts/Create_TANNARX_Analytics_Views.sql faylini ishga tushiring
```

### 2. Actual Costs'larni yangilash

Dashboard sahifasida "Actual Costs Yangilash" tugmasini bosing yoki:

```sql
EXEC SP_TANNARX_UPDATE_ACTUAL_COSTS @Year = 2025, @Month = 9
-- Yoki barcha oylar uchun:
EXEC SP_TANNARX_UPDATE_ACTUAL_COSTS
```

## Hisob-kitoblar

### Actual Material Price
- **Manba**: NORM_INPUT_LINES jadvali
- **Hisoblash**: `SUM(TotalAmount)` yoki `SUM(Price * Consumption)`
- **Guruhlash**: ProductID, Year, Month

### Actual Material Transport Cost
- **Manba**: F_WAYBILLS (Direction = 0, PurchaseOrderID orqali)
- **Hisoblash**: Waybill cost / Total products quantity * Product quantity
- **Guruhlash**: ProductID, Year, Month

### Actual Labor Cost
- **Manba**: HR_PIECE_RATE_WORK_ENTRIES
- **Hisoblash**: `SUM(Quantity * RatePerUnit)` (HR_PIECE_RATE_TARIFFS dan)
- **Guruhlash**: ProductID, Year, Month

### Actual Administrative Expenses
- **Manba**: HR_PAYROLL_LINES (IsHourly = 1 bo'lgan xodimlar)
- **Hisoblash**: Total admin payroll / Total products produced
- **Guruhlash**: ProductID, Year, Month

### Actual Finished Product Transport Cost
- **Manba**: F_WAYBILLS (Direction = 1, SalesOrderID orqali)
- **Hisoblash**: Waybill cost / Total products quantity * Product quantity
- **Guruhlash**: ProductID, Year, Month

## Performance Optimizatsiyalar

1. **SQL Views** - Barcha hisob-kitoblar database level'da
2. **Indexes** - TANNARX jadvalida ProductID, Year, Month bo'yicha indexlar
3. **Stored Procedure** - Bulk update uchun
4. **Caching** - Service layer'da caching qo'shish mumkin (keyinchalik)

## Foydalanish

1. **Dashboard**: `/TannarxAnalytics/Dashboard?year=2025&month=9`
2. **Report**: `/TannarxAnalytics/Report?year=2025&month=9`
3. **Product Details**: `/TannarxAnalytics/ProductDetails?productID=123&year=2025&month=9`

## Eslatmalar

- Actual costs'lar avtomatik yangilanmaydi - "Actual Costs Yangilash" tugmasini bosing
- Material transport va Finished product transport - waybill'dagi barcha maxsulotlarga proporsional taqsimlanadi
- Administrative expenses - oy davomida ishlab chiqarilgan barcha maxsulotlarga teng taqsimlanadi
