# Database schema: Lot tracking va DLOC (Direct Location) taklifi

Hozirgi loyihada FIFO/lot tracking `EXPENSE_PRODUCTS.Comment` orqali `IncomeID:123` formatida saqlanadi. Keyinchalik quyidagi o'zgarishlar taklif etiladi.

---

## 1. Lot/Batch tracking (FIFO uchun)

### 1.1 Yangi jadvallar

#### `INCOME_LOTS` (yoki `LOTS`)
| Ustun | Turi | Izoh |
|-------|------|------|
| ID | int, PK | |
| LotNo | nvarchar(50) | Partiya/lot raqami (masalan: LOT-2025-001) |
| IncomeID | int, FK → INCOMES | Qaysi kirim hujjatiga tegishli |
| ProductID | int, FK | |
| WarehouseID | int, FK | |
| Quantity | decimal(18,4) | Partiyadagi miqdor |
| ReceivedDate | datetime | Kirim sanasi |
| ExpiryDate | datetime, NULL | Muddati (FEFO uchun) |
| IsDeleted | bit | |

#### `EXPENSE_PRODUCTS` o'zgarishi
| Yangi ustun | Turi | Izoh |
|-------------|------|------|
| LotID | int, FK → INCOME_LOTS, NULL | Qaysi lotdan chiqim (hozirgi `Comment` o'rniga) |

`Comment` ustuni saqlanadi — foydalanuvchi izohi uchun.

### 1.2 PRODUCT_STOCKS o'zgarishi

Hozir: `PRODUCT_STOCKS (WHID, ProductID, Amount)` — umumiy qoldiq.

**Variant A – Lot darajasida qoldiq:**
`PRODUCT_STOCK_LOTS`:
| Ustun | Turi |
|-------|------|
| ID | int, PK |
| WarehouseID | int, FK |
| ProductID | int, FK |
| LotID | int, FK |
| Amount | decimal(18,4) |

Umumiy qoldiq: `SUM(Amount) GROUP BY WarehouseID, ProductID`.

**Variant B – PRODUCT_STOCKS ga LotID qo'shish:**
`PRODUCT_STOCKS (WHID, ProductID, LotID, Amount)` — LotID NULL bo'lsa, lot bo'yicha hisoblanmagan (eskiga mos).

---

## 2. DLOC – Direct Location (ombor yacheykalari)

### 2.1 Yangi jadvallar

#### `WAREHOUSE_LOCATIONS` (DLOC)
| Ustun | Turi | Izoh |
|-------|------|------|
| ID | int, PK | |
| WarehouseID | int, FK → WAREHOUSES | |
| LocationCode | nvarchar(20) | Yacheyka kodi (A-01-02) |
| LocationName | nvarchar(100) | Nomi |
| ZoneID | int, FK, NULL | Zona (ixtiyoriy) |
| IsActive | bit | |
| IsDeleted | bit | |

#### `WAREHOUSE_ZONES` (ixtiyoriy)
| Ustun | Turi |
|-------|------|
| ID | int, PK |
| WarehouseID | int, FK |
| ZoneCode | nvarchar(20) |
| ZoneName | nvarchar(100) |

### 2.2 Lot + Location bog'lash

#### `INCOME_LOTS` ga ustunlar:
| Ustun | Turi | Izoh |
|-------|------|------|
| LocationID | int, FK → WAREHOUSE_LOCATIONS, NULL | Qaysi yacheykaga kirim qilingan |

Yoki alohida:

#### `LOT_LOCATIONS`
| Ustun | Turi |
|-------|------|
| LotID | int, FK |
| LocationID | int, FK |
| Quantity | decimal(18,4) |

Bitta lot bir nechta yacheykada bo'lishi mumkin.

### 2.3 INCOME / INCOME_PRODUCTS o'zgarishlari

- `INCOME` yoki `INCOME_PRODUCTS`: `LocationID` (int, NULL) — kirim qilingan yacheyka.

---

## 3. Amaliy migratsiya ketma-ketligi

1. **Birinchi bosqich (hozirgi):** `EXPENSE_PRODUCTS.Comment` da `IncomeID:X` — DB o'zgarishsiz.
2. **Ikkinchi bosqich:** `EXPENSE_PRODUCTS` ga `SourceIncomeID` (int, FK, NULL) qo'shish — to'g'ri bog'lanish.
3. **Uchinchi bosqich:** `INCOME_LOTS` va lot darajasida qoldiq (PRODUCT_STOCK_LOTS yoki PRODUCT_STOCKS.LotID).
4. **To'rtinchi bosqich:** `WAREHOUSE_LOCATIONS`, `INCOME.LocationID` yoki `INCOME_PRODUCTS.LocationID`.

---

## 4. Xomashyo va tayyor mahsulot

Taklif bir xil:

- **Xomashyo (Xomashyo ombori → Ishlab chiqarish):** Lot + DLOC qo'llanadi.
- **Tayyor mahsulot (Ishlab chiqarish → Sotuv):** Lot + DLOC qo'llanadi.

`INCOME` va `EXPENS` uchun `WarehouseID` bor; `ReceivingWHID` / `SenderWHID` orqali ombor aniqlanadi. Lot va Location xuddi shu model ustiga qo'shiladi.
