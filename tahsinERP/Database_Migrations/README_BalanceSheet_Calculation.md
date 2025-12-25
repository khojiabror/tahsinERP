# Balans Hisoboti - Calculation Type Yechimi

## Umumiy ma'lumot

Bu yechim mashhur ERP tizimlarida (SAP, Oracle, Microsoft Dynamics) qo'llaniladigan standart yondashuvga asoslangan.

## Database O'zgarishlari

### 1. FIN_BALANCE_CATEGORIES jadvaliga qo'shilgan maydon:
- **CalculationType** (TINYINT, NOT NULL, DEFAULT 1)
  - `1` = Account-Based (Hisob asosida)
  - `2` = Transaction-Based (Tranzaksiya asosida)
  - `3` = Document-Based (Hujjat asosida)

### 2. FIN_ACCOUNTS jadvaliga qo'shilgan maydon:
- **BalanceCategoryID** (INT, NULL)
  - Foreign Key: FIN_BALANCE_CATEGORIES.ID
  - Account-Based kategoriyalar uchun hisobni balans kategoriyasiga bog'laydi

## Calculation Type Turlari

### 1. Account-Based (Hisob asosida)
**Qachon ishlatiladi:**
- Naqd pul
- Bank hisobi
- Boshqa doimiy hisoblar

**Qanday ishlaydi:**
- FIN_ACCOUNTS jadvalidan BalanceCategoryID ga qarab hisoblar tanlanadi
- Har bir hisobning qoldig'i hisoblanadi (FIN_TRANSACTIONS orqali)
- Barcha hisoblarning qoldiqlari yig'iladi

**Misol:**
```
"Naqd pul" kategoriyasi:
- Hisob 9000 (Naqd pul) → BalanceCategoryID = 1
- Hisob 9003 (Naqd pul - Kassa) → BalanceCategoryID = 1
- Jami = 9000 qoldig'i + 9003 qoldig'i
```

### 2. Transaction-Based (Tranzaksiya asosida)
**Qachon ishlatiladi:**
- Ustav kapitali (A'zo hissasi kategoriyasi orqali)
- Boshqa tranzaksiya kategoriyalari orqali hisoblanishi kerak bo'lgan pozitsiyalar

**Qanday ishlaydi:**
- FIN_BALANCE_CATEGORIES → FIN_CATEGORIES mapping ishlatiladi
- Mapping qilingan kategoriyalardagi barcha tranzaksiyalar yig'iladi
- Kirim/chiqim yo'nalishiga qarab hisoblanadi

**Misol:**
```
"Ustav kapitali" kategoriyasi:
- "A'zo hissasi" kategoriyasi bilan bog'langan
- Barcha "A'zo hissasi" kategoriyasidagi tranzaksiyalar yig'iladi
- Jami = Kirimlar - Chiqimlar
```

### 3. Document-Based (Hujjat asosida)
**Qachon ishlatiladi:**
- Ta'minotchilardan qarzlar (P_INVOICES)
- Xaridorlardan haqlarimiz (SALES_INVOICES)
- Boshqa hujjatlar asosida hisoblanishi kerak bo'lgan pozitsiyalar

**Qanday ishlaydi:**
- To'g'ridan-to'g'ri hujjatlar jadvalidan (P_INVOICES, SALES_INVOICES)
- To'lovlar FIN_TRANSACTIONS orqali tekshiriladi
- To'lanmagan qism hisoblanadi

**Misol:**
```
"Ta'minotchilardan qarzlar":
- Barcha P_INVOICES olinadi
- Har bir invoys uchun FIN_TRANSACTIONS dan to'lovlar hisoblanadi
- Jami = Barcha invoyslar summasi - To'lovlar summasi
```

## Migration Qadamlari

1. **SQL Migration faylini ishga tushiring:**
   ```sql
   -- Database_Migrations/BalanceSheet_CalculationType.sql
   ```

2. **Entity Framework modelini yangilang:**
   - Visual Studio'da: Model → Update Model from Database
   - Yoki qo'lda: Models/FIN_BALANCE_CATEGORIES.cs va FIN_ACCOUNTS.cs fayllarini yangilang

3. **Mavjud ma'lumotlarni yangilang:**
   - FIN_ACCOUNTS jadvalidagi hisoblarni tegishli BalanceCategoryID ga bog'lang
   - FIN_BALANCE_CATEGORIES jadvalidagi kategoriyalarni CalculationType ga moslang

## Qo'shimcha Sozlamalar

### FinAccountsController'da BalanceCategoryID qo'shish

Create va Edit metodlariga BalanceCategoryID dropdown qo'shish kerak:

```csharp
ViewBag.BalanceCategoryID = new SelectList(
    db.FIN_BALANCE_CATEGORIES
        .Where(c => c.IsActive && c.CalculationType == 1) // Faqat Account-Based
        .OrderBy(c => c.Name)
        .ToList(),
    "ID", "Name", account.BalanceCategoryID);
```

### FinBalanceCategoriesController'da CalculationType qo'shish

Create va Edit metodlariga CalculationType dropdown qo'shish kerak:

```csharp
ViewBag.CalculationTypeList = new SelectList(new[] {
    new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
    new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
    new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
}, "Value", "Text", category.CalculationType);
```

## Afzalliklari

1. **Moslashuvchanlik:** Har bir kategoriya uchun mos hisoblash usuli
2. **Tezlik:** Document-Based hisoblash to'g'ridan-to'g'ri hujjatlardan
3. **Aniqlik:** Account-Based hisoblash aniq hisoblar qoldiqlari
4. **Standart:** Mashhur ERP tizimlarida qo'llaniladigan yondashuv

## Eslatmalar

- Snapshots hali ham eng yuqori prioritetga ega (agar mavjud bo'lsa)
- Document-Based hisoblashda to'lovlar FIN_TRANSACTIONS orqali tekshiriladi
- Transaction-Based hisoblashda FIN_BALANCE_CATEGORIES → FIN_CATEGORIES mapping kerak
