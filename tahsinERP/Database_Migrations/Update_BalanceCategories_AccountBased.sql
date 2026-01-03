-- =============================================
-- Script: Update Balance Categories to Account-Based Calculation
-- Description: 
--   - Updates "Taminotchilardan qarzlar" (Accounts Payable) to use Account-Based calculation (CalculationType = 1)
--   - Updates "Xaridorlardan haqlarimiz" (Accounts Receivable) to use Account-Based calculation (CalculationType = 1)
--   - Links Payables accounts (PAYABLES_*) to "Taminotchilardan qarzlar" balance category
--   - Links Receivables accounts (RECEIVABLES_*) to "Xaridorlardan haqlarimiz" balance category
--   - This follows ERP standards (SAP, Oracle, Microsoft Dynamics)
-- Date: 2026-01-02
-- =============================================

USE [DBTHSN]
GO

PRINT '========================================'
PRINT 'Updating Balance Categories to Account-Based Calculation'
PRINT '========================================'
PRINT ''

-- ============================================
-- PART 1: "Taminotchilardan qarzlar" (Accounts Payable)
-- ============================================
PRINT '========================================'
PRINT 'PART 1: "Taminotchilardan qarzlar" (Accounts Payable)'
PRINT '========================================'
PRINT ''

-- Step 1: Check if "Taminotchilardan qarzlar" category exists
PRINT 'Step 1: Checking if "Taminotchilardan qarzlar" category exists...'
DECLARE @PayablesCategoryID INT
DECLARE @PayablesCurrentCalculationType TINYINT

SELECT @PayablesCategoryID = ID, @PayablesCurrentCalculationType = CalculationType
FROM FIN_BALANCE_CATEGORIES
WHERE Name = N'Taminotchilardan qarzlar' OR Name = N'Ta''minotchilardan qarzlar'

IF @PayablesCategoryID IS NULL
BEGIN
    PRINT 'WARNING: "Taminotchilardan qarzlar" category not found!'
    PRINT 'Skipping this category. Please create it first in FinBalanceCategories if needed.'
    PRINT ''
END
ELSE
BEGIN
    PRINT 'Found category ID: ' + CAST(@PayablesCategoryID AS VARCHAR(10))
    PRINT 'Current CalculationType: ' + CAST(@PayablesCurrentCalculationType AS VARCHAR(10))
    PRINT ''

    -- Step 2: Update CalculationType to 1 (Account-Based)
    PRINT 'Step 2: Updating CalculationType to 1 (Account-Based)...'
    UPDATE FIN_BALANCE_CATEGORIES
    SET CalculationType = 1
    WHERE ID = @PayablesCategoryID

    IF @@ROWCOUNT > 0
    BEGIN
        PRINT 'SUCCESS: CalculationType updated to 1 (Account-Based)'
    END
    ELSE
    BEGIN
        PRINT 'WARNING: No rows updated. CalculationType may already be 1.'
    END
    PRINT ''

    -- Step 3: Find and link Payables accounts
    PRINT 'Step 3: Finding and linking Payables accounts...'
    DECLARE @PayablesAccountCount INT

    -- Update all Payables accounts (PAYABLES_* or containing "Kreditorlar")
    UPDATE FIN_ACCOUNTS
    SET BalanceCategoryID = @PayablesCategoryID
    WHERE (Code LIKE 'PAYABLES_%' OR Name LIKE '%Kreditorlar%' OR Name LIKE '%Creditor%')
      AND IsActive = 1
      AND (BalanceCategoryID IS NULL OR BalanceCategoryID != @PayablesCategoryID)

    SET @PayablesAccountCount = @@ROWCOUNT

    IF @PayablesAccountCount > 0
    BEGIN
        PRINT 'SUCCESS: Linked ' + CAST(@PayablesAccountCount AS VARCHAR(10)) + ' Payables account(s) to "Taminotchilardan qarzlar"'
        
        -- Show which accounts were linked
        PRINT ''
        PRINT 'Linked Payables accounts:'
        SELECT 
            ID,
            Code,
            Name,
            BalanceCategoryID
        FROM FIN_ACCOUNTS
        WHERE BalanceCategoryID = @PayablesCategoryID
          AND (Code LIKE 'PAYABLES_%' OR Name LIKE '%Kreditorlar%' OR Name LIKE '%Creditor%')
          AND IsActive = 1
    END
    ELSE
    BEGIN
        PRINT 'INFO: No Payables accounts found or already linked.'
        PRINT 'Checking existing Payables accounts...'
        
        SELECT 
            ID,
            Code,
            Name,
            BalanceCategoryID,
            IsActive
        FROM FIN_ACCOUNTS
        WHERE (Code LIKE 'PAYABLES_%' OR Name LIKE '%Kreditorlar%' OR Name LIKE '%Creditor%')
    END
    PRINT ''
END

-- ============================================
-- PART 2: "Xaridorlardan haqlarimiz" (Accounts Receivable)
-- ============================================
PRINT '========================================'
PRINT 'PART 2: "Xaridorlardan haqlarimiz" (Accounts Receivable)'
PRINT '========================================'
PRINT ''

-- Step 1: Check if "Xaridorlardan haqlarimiz" category exists
PRINT 'Step 1: Checking if "Xaridorlardan haqlarimiz" category exists...'
DECLARE @ReceivablesCategoryID INT
DECLARE @ReceivablesCurrentCalculationType TINYINT

SELECT @ReceivablesCategoryID = ID, @ReceivablesCurrentCalculationType = CalculationType
FROM FIN_BALANCE_CATEGORIES
WHERE Name = N'Xaridorlardan haqlarimiz' OR Name LIKE N'%Xaridor%haqlarimiz%'

IF @ReceivablesCategoryID IS NULL
BEGIN
    PRINT 'WARNING: "Xaridorlardan haqlarimiz" category not found!'
    PRINT 'Skipping this category. Please create it first in FinBalanceCategories if needed.'
    PRINT ''
END
ELSE
BEGIN
    PRINT 'Found category ID: ' + CAST(@ReceivablesCategoryID AS VARCHAR(10))
    PRINT 'Current CalculationType: ' + CAST(@ReceivablesCurrentCalculationType AS VARCHAR(10))
    PRINT ''

    -- Step 2: Update CalculationType to 1 (Account-Based)
    PRINT 'Step 2: Updating CalculationType to 1 (Account-Based)...'
    UPDATE FIN_BALANCE_CATEGORIES
    SET CalculationType = 1
    WHERE ID = @ReceivablesCategoryID

    IF @@ROWCOUNT > 0
    BEGIN
        PRINT 'SUCCESS: CalculationType updated to 1 (Account-Based)'
    END
    ELSE
    BEGIN
        PRINT 'WARNING: No rows updated. CalculationType may already be 1.'
    END
    PRINT ''

    -- Step 3: Find and link Receivables accounts
    PRINT 'Step 3: Finding and linking Receivables accounts...'
    DECLARE @ReceivablesAccountCount INT

    -- Update all Receivables accounts (RECEIVABLES_* or containing "Debitorlar")
    UPDATE FIN_ACCOUNTS
    SET BalanceCategoryID = @ReceivablesCategoryID
    WHERE (Code LIKE 'RECEIVABLES_%' OR Name LIKE '%Debitorlar%' OR Name LIKE '%Receivable%')
      AND IsActive = 1
      AND (BalanceCategoryID IS NULL OR BalanceCategoryID != @ReceivablesCategoryID)

    SET @ReceivablesAccountCount = @@ROWCOUNT

    IF @ReceivablesAccountCount > 0
    BEGIN
        PRINT 'SUCCESS: Linked ' + CAST(@ReceivablesAccountCount AS VARCHAR(10)) + ' Receivables account(s) to "Xaridorlardan haqlarimiz"'
        
        -- Show which accounts were linked
        PRINT ''
        PRINT 'Linked Receivables accounts:'
        SELECT 
            ID,
            Code,
            Name,
            BalanceCategoryID
        FROM FIN_ACCOUNTS
        WHERE BalanceCategoryID = @ReceivablesCategoryID
          AND (Code LIKE 'RECEIVABLES_%' OR Name LIKE '%Debitorlar%' OR Name LIKE '%Receivable%')
          AND IsActive = 1
    END
    ELSE
    BEGIN
        PRINT 'INFO: No Receivables accounts found or already linked.'
        PRINT 'Checking existing Receivables accounts...'
        
        SELECT 
            ID,
            Code,
            Name,
            BalanceCategoryID,
            IsActive
        FROM FIN_ACCOUNTS
        WHERE (Code LIKE 'RECEIVABLES_%' OR Name LIKE '%Debitorlar%' OR Name LIKE '%Receivable%')
    END
    PRINT ''
END

-- ============================================
-- FINAL VERIFICATION
-- ============================================
PRINT '========================================'
PRINT 'FINAL VERIFICATION'
PRINT '========================================'
PRINT ''

-- Check both category settings
PRINT 'Updated Balance Categories:'
SELECT 
    ID,
    Name,
    CalculationType,
    CASE CalculationType
        WHEN 1 THEN 'Account-Based'
        WHEN 2 THEN 'Transaction-Based'
        WHEN 3 THEN 'Document-Based'
        ELSE 'Unknown'
    END AS CalculationTypeName,
    IsActive
FROM FIN_BALANCE_CATEGORIES
WHERE (ID = @PayablesCategoryID OR ID = @ReceivablesCategoryID)
  AND (@PayablesCategoryID IS NOT NULL OR @ReceivablesCategoryID IS NOT NULL)

PRINT ''
PRINT '========================================'
PRINT 'Update completed successfully!'
PRINT '========================================'
PRINT ''
PRINT 'Summary:'
IF @PayablesCategoryID IS NOT NULL
BEGIN
    PRINT '- "Taminotchilardan qarzlar" updated to Account-Based calculation'
END
ELSE
BEGIN
    PRINT '- "Taminotchilardan qarzlar" category not found (skipped)'
END

IF @ReceivablesCategoryID IS NOT NULL
BEGIN
    PRINT '- "Xaridorlardan haqlarimiz" updated to Account-Based calculation'
END
ELSE
BEGIN
    PRINT '- "Xaridorlardan haqlarimiz" category not found (skipped)'
END

PRINT ''
PRINT 'Next steps:'
PRINT '1. Verify that Payables and Receivables accounts are correctly linked'
PRINT '2. Test Balance Sheet report to ensure both categories show correct amounts'
PRINT '3. The amounts should now match the respective account balances'
PRINT '   - "Taminotchilardan qarzlar" = Payables account balance (e.g., Payables_5)'
PRINT '   - "Xaridorlardan haqlarimiz" = Receivables account balance (e.g., Receivables_5)'
PRINT ''

GO
