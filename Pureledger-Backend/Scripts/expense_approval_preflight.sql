-- Read-only deployment checks for Expense approval.
-- Structural/error result sets should be empty. Product-role inventory is
-- informational and may contain rows; the final AccountGroup row confirms ID availability.

-- ID 3 must be unused or already named Expense.
SELECT "AccountGroupId", "AccountGroupName"
FROM "AccountGroups"
WHERE "AccountGroupId" = 3 AND "AccountGroupName" <> 'Expense';

-- Duplicate DC containers are unsupported for approval graph conversion.
SELECT "JournalVoucherId", COUNT(*) AS "ContainerCount"
FROM "DeliveryChallanVouchers"
GROUP BY "JournalVoucherId"
HAVING COUNT(*) > 1;

-- Duplicate or malformed FOR/cartage references.
SELECT "MainVoucherId", "ReferencedVoucherId", "ReferenceType", COUNT(*) AS "ReferenceCount"
FROM "JournalVoucherReferences"
WHERE "ReferenceType" IN ('ForCharges', 'Cartage')
GROUP BY "MainVoucherId", "ReferencedVoucherId", "ReferenceType"
HAVING COUNT(*) > 1;

-- Orphan checks (normally prevented by FKs, retained as rollout verification).
SELECT dce."DeliveryChallanEntryId"
FROM "DeliveryChallanEntries" dce
LEFT JOIN "JournalEntries" je ON je."JournalEntryId" = dce."JournalEntryId"
LEFT JOIN "DeliveryChallanVouchers" dcv
  ON dcv."DeliveryChallanVoucherId" = dce."DeliveryChallanVoucherId"
WHERE je."JournalEntryId" IS NULL OR dcv."DeliveryChallanVoucherId" IS NULL;

-- Current FOR/cartage category contents. Product rows are deliberately reported,
-- not migrated: this feature preserves the existing category-driven behavior.
SELECT pcs."ProjectId", 'FOR' AS "Role", a."AccountId", a."AccountName", a."AccountGroupId"
FROM "ProjectCartageSettings" pcs
JOIN "Accounts" a ON a."CategoryId" = pcs."ForExpenseCategoryId"
WHERE a."AccountGroupId" = 2
UNION ALL
SELECT pcs."ProjectId", 'Cartage', a."AccountId", a."AccountName", a."AccountGroupId"
FROM "ProjectCartageSettings" pcs
JOIN "Accounts" a ON a."CategoryId" = pcs."CartageTransporterCategoryId"
WHERE a."AccountGroupId" = 2;

SELECT CASE
  WHEN NOT EXISTS (SELECT 1 FROM "AccountGroups" WHERE "AccountGroupId" = 3)
    THEN 'AVAILABLE'
  WHEN EXISTS (
    SELECT 1 FROM "AccountGroups"
    WHERE "AccountGroupId" = 3 AND "AccountGroupName" = 'Expense'
  ) THEN 'ALREADY_EXPENSE'
  ELSE 'CONFLICT'
END AS "AccountGroup3Status";
