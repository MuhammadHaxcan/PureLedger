-- ============================================================================
-- CLEANUP DUPLICATE DELIVERY CHALLAN ENTRIES
-- ============================================================================
-- Purpose: Find and remove duplicate DeliveryChallanEntries where a single
--          JournalEntry has multiple DCEs (should be 1:1 relationship)
--
-- Run scripts 1-3 first to analyze, then run script 4 to delete duplicates.
-- The scripts keep the NEWEST (highest ID) DCE and remove older ones.
-- ============================================================================


-- ============================================================================
-- SCRIPT 1: Find JournalEntries with multiple DCEs
-- ============================================================================
-- Shows which JournalEntryIds have more than one DeliveryChallanEntry

SELECT "JournalEntryId", COUNT(*) as dce_count
FROM "DeliveryChallanEntries"
GROUP BY "JournalEntryId"
HAVING COUNT(*) > 1
ORDER BY dce_count DESC;


-- ============================================================================
-- SCRIPT 2: List all duplicate DCE details
-- ============================================================================
-- Shows full details of all DCEs that belong to JournalEntries with duplicates

SELECT
    dce."DeliveryChallanEntryId",
    dce."JournalEntryId",
    dce."DeliveryChallanVoucherId",
    dce."LocationId",
    dce."EntryDate",
    dce."Remarks",
    dce."OtherReference"
FROM "DeliveryChallanEntries" dce
WHERE dce."JournalEntryId" IN (
    SELECT "JournalEntryId"
    FROM "DeliveryChallanEntries"
    GROUP BY "JournalEntryId"
    HAVING COUNT(*) > 1
)
ORDER BY dce."JournalEntryId", dce."DeliveryChallanEntryId";


-- ============================================================================
-- SCRIPT 3: Identify DCEs to DELETE (preview before deletion)
-- ============================================================================
-- Shows which DCEs will be deleted (keeps highest/newest ID per JournalEntry)

SELECT
    dce."DeliveryChallanEntryId" as "TO_DELETE",
    dce."JournalEntryId",
    dce."DeliveryChallanVoucherId",
    dce."LocationId",
    dce."EntryDate"
FROM "DeliveryChallanEntries" dce
WHERE dce."DeliveryChallanEntryId" NOT IN (
    SELECT MAX("DeliveryChallanEntryId")
    FROM "DeliveryChallanEntries"
    GROUP BY "JournalEntryId"
)
AND dce."JournalEntryId" IN (
    SELECT "JournalEntryId"
    FROM "DeliveryChallanEntries"
    GROUP BY "JournalEntryId"
    HAVING COUNT(*) > 1
)
ORDER BY dce."JournalEntryId";


-- ============================================================================
-- SCRIPT 4: DELETE the duplicate DCEs
-- ============================================================================
-- CAUTION: This permanently deletes duplicate entries!
-- Keeps the NEWEST (highest ID) DCE, deletes older ones.
-- Run scripts 1-3 first to verify what will be deleted.

DELETE FROM "DeliveryChallanEntries"
WHERE "DeliveryChallanEntryId" IN (
    SELECT dce."DeliveryChallanEntryId"
    FROM "DeliveryChallanEntries" dce
    WHERE dce."DeliveryChallanEntryId" NOT IN (
        SELECT MAX("DeliveryChallanEntryId")
        FROM "DeliveryChallanEntries"
        GROUP BY "JournalEntryId"
    )
    AND dce."JournalEntryId" IN (
        SELECT "JournalEntryId"
        FROM "DeliveryChallanEntries"
        GROUP BY "JournalEntryId"
        HAVING COUNT(*) > 1
    )
);


-- ============================================================================
-- SCRIPT 5: Verify cleanup (run after Script 4)
-- ============================================================================
-- Should return 0 rows if all duplicates were removed
 
