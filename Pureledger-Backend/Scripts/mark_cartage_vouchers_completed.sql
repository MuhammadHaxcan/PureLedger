-- ============================================================================
-- MARK CARTAGE REFERENCED VOUCHERS AS COMPLETED
-- ============================================================================
-- Purpose: Find all JournalVouchers that are referenced as "Cartage" type
--          and mark them as IsCompleted = true
--
-- Run scripts 1-2 first to analyze, then run script 3 to update.
-- ============================================================================


-- ============================================================================
-- SCRIPT 1: Find all Cartage referenced voucher IDs
-- ============================================================================
-- Shows all ReferencedVoucherIds where ReferenceType = 'Cartage'

SELECT DISTINCT
    jvr."ReferencedVoucherId",
    jv."JournalVoucherId",
    jv."EntryDate",
    jv."IsCompleted",
    jv."ProjectId"
FROM "JournalVoucherReferences" jvr
INNER JOIN "JournalVouchers" jv ON jv."JournalVoucherId" = jvr."ReferencedVoucherId"
WHERE jvr."ReferenceType" = 'Cartage'
ORDER BY jvr."ReferencedVoucherId";


-- ============================================================================
-- SCRIPT 2: Preview vouchers that will be updated (currently NOT completed)
-- ============================================================================
-- Shows only Cartage vouchers where IsCompleted is currently false

SELECT DISTINCT
    jvr."ReferencedVoucherId",
    jv."JournalVoucherId",
    jv."EntryDate",
    jv."IsCompleted" AS "CurrentStatus",
    jv."ProjectId"
FROM "JournalVoucherReferences" jvr
INNER JOIN "JournalVouchers" jv ON jv."JournalVoucherId" = jvr."ReferencedVoucherId"
WHERE jvr."ReferenceType" = 'Cartage'
  AND jv."IsCompleted" = false
ORDER BY jvr."ReferencedVoucherId";


-- ============================================================================
-- SCRIPT 3: UPDATE - Mark all Cartage referenced vouchers as completed
-- ============================================================================
-- CAUTION: This permanently updates IsCompleted to true!
-- Run scripts 1-2 first to verify what will be updated.

UPDATE "JournalVouchers"
SET "IsCompleted" = true
WHERE "JournalVoucherId" IN (
    SELECT DISTINCT jvr."ReferencedVoucherId"
    FROM "JournalVoucherReferences" jvr
    WHERE jvr."ReferenceType" = 'Cartage'
);


-- ============================================================================
-- SCRIPT 4: Verify update (run after Script 3)
-- ============================================================================
-- Should return 0 rows if all Cartage vouchers are now completed

SELECT DISTINCT
    jvr."ReferencedVoucherId",
    jv."IsCompleted"
FROM "JournalVoucherReferences" jvr
INNER JOIN "JournalVouchers" jv ON jv."JournalVoucherId" = jvr."ReferencedVoucherId"
WHERE jvr."ReferenceType" = 'Cartage'
  AND jv."IsCompleted" = false;
