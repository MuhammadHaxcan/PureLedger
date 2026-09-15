-- POST-DEPLOYMENT integrity audit. Run Scripts/payment_receipt_preflight.sql first.
-- Every query below must return zero rows.

-- Orphaned detail rows (the FK should also prevent this).
SELECT d.*
FROM "JournalEntryInstrumentDetails" d
LEFT JOIN "JournalEntries" e ON e."JournalEntryId" = d."JournalEntryId"
WHERE e."JournalEntryId" IS NULL;

-- Multiple details per journal entry.
SELECT e."JournalVoucherId"
FROM "JournalEntries" e
JOIN "JournalEntryInstrumentDetails" d ON d."JournalEntryId" = e."JournalEntryId"
GROUP BY e."JournalVoucherId"
HAVING count(*) <> count(DISTINCT e."JournalEntryId");

-- Any partial-detail voucher, including corrupt unapproved graphs.
SELECT e."JournalVoucherId"
FROM "JournalEntries" e
LEFT JOIN "JournalEntryInstrumentDetails" d ON d."JournalEntryId" = e."JournalEntryId"
GROUP BY e."JournalVoucherId"
HAVING count(d.*) > 0 AND count(d.*) <> count(e.*);

-- Partial-detail and zero-entry approved P&R vouchers.
SELECT a."ApprovalVoucherId", a."JournalVoucherId"
FROM "ApprovalVouchers" a
LEFT JOIN "JournalEntries" e ON e."JournalVoucherId" = a."JournalVoucherId"
LEFT JOIN "JournalEntryInstrumentDetails" d ON d."JournalEntryId" = e."JournalEntryId"
WHERE a."VoucherType" = 'PaymentReceipt' AND a."Status" = 'Approved'
GROUP BY a."ApprovalVoucherId", a."JournalVoucherId"
HAVING count(e.*) = 0 OR count(e.*) <> count(d.*);

-- P&R approvals must retain at least two complete snapshot rows and no line numbers.
SELECT a."ApprovalVoucherId"
FROM "ApprovalVouchers" a
LEFT JOIN "ApprovalVoucherEntries" e ON e."ApprovalVoucherId" = a."ApprovalVoucherId"
WHERE a."VoucherType" = 'PaymentReceipt'
GROUP BY a."ApprovalVoucherId"
HAVING count(e.*) < 2
    OR count(*) FILTER (WHERE e."LineNumber" IS NOT NULL) > 0
    OR count(*) FILTER (WHERE e."InstrumentType" IS NULL) > 0;

-- Incompatible base/stock/DC/reference graphs.
SELECT DISTINCT v."JournalVoucherId"
FROM "JournalVouchers" v
JOIN "JournalEntries" e ON e."JournalVoucherId" = v."JournalVoucherId"
JOIN "JournalEntryInstrumentDetails" d ON d."JournalEntryId" = e."JournalEntryId"
WHERE v."IsBaseVoucher" OR v."IsStockTransfer" OR v."PendingForCharges"
   OR EXISTS (SELECT 1 FROM "DeliveryChallanVouchers" dc WHERE dc."JournalVoucherId" = v."JournalVoucherId")
   OR EXISTS (SELECT 1 FROM "DeliveryChallanEntries" de WHERE de."JournalEntryId" = e."JournalEntryId")
   OR EXISTS (SELECT 1 FROM "JournalVoucherReferences" r
              WHERE r."MainVoucherId" = v."JournalVoucherId" OR r."ReferencedVoucherId" = v."JournalVoucherId");

-- Completed without approval provenance / incomplete without one pending revision.
SELECT v."JournalVoucherId"
FROM "JournalVouchers" v
WHERE EXISTS (SELECT 1 FROM "JournalEntries" e JOIN "JournalEntryInstrumentDetails" d
              ON d."JournalEntryId" = e."JournalEntryId" WHERE e."JournalVoucherId" = v."JournalVoucherId")
  AND ((v."IsCompleted" AND NOT EXISTS (SELECT 1 FROM "ApprovalVouchers" a
          WHERE a."JournalVoucherId" = v."JournalVoucherId" AND a."VoucherType" = 'PaymentReceipt' AND a."Status" = 'Approved'))
    OR (NOT v."IsCompleted" AND 1 <> (SELECT count(*) FROM "ApprovalVouchers" a
          WHERE a."JournalVoucherId" = v."JournalVoucherId" AND a."VoucherType" = 'PaymentReceipt'
            AND a."Status" = 'Pending' AND a."RequestKind" = 'Revision')));

-- Invalid pending links/snapshots and duplicate pending revisions.
SELECT a."ApprovalVoucherId"
FROM "ApprovalVouchers" a
WHERE a."VoucherType" = 'PaymentReceipt' AND (
      a."RequestedEntryDate" IS NULL OR a."SnapshotVersion" IS NULL
   OR NOT EXISTS (SELECT 1 FROM "ApprovalVoucherEntries" e WHERE e."ApprovalVoucherId" = a."ApprovalVoucherId")
   OR (a."Status" = 'Pending' AND a."RequestKind" = 'New' AND a."JournalVoucherId" IS NOT NULL)
   OR (a."RequestKind" = 'Revision' AND (a."JournalVoucherId" IS NULL OR NOT EXISTS (
          SELECT 1 FROM "JournalVouchers" v WHERE v."JournalVoucherId" = a."JournalVoucherId"))));

SELECT "JournalVoucherId", count(*)
FROM "ApprovalVouchers"
WHERE "VoucherType" = 'PaymentReceipt' AND "Status" = 'Pending' AND "JournalVoucherId" IS NOT NULL
GROUP BY "JournalVoucherId"
HAVING count(*) > 1;
