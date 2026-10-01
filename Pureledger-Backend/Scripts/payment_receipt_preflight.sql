-- PRE-DEPLOYMENT audit. Safe before the Payment & Receipt migration exists.
-- It detects a partial/manual schema installation and validates the existing
-- Expense approval state without referencing any new P&R column directly.

DO $$
DECLARE
    migration_applied boolean;
    reserved_objects integer;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20260731010000_AddPaymentReceiptVouchers'
    ) INTO migration_applied;

    SELECT
        (CASE WHEN to_regclass('"JournalEntryInstrumentDetails"') IS NULL THEN 0 ELSE 1 END) +
        (SELECT count(*) FROM information_schema.columns
         WHERE table_schema = current_schema()
           AND table_name IN ('ApprovalVouchers', 'ApprovalVoucherEntries')
           AND column_name IN (
               'VoucherType', 'RequestedEntryDate', 'SnapshotVersion',
               'HadDuplicateWarningsAtSubmission', 'HadDuplicateWarningsAtDecision',
               'InstrumentType', 'ReferenceNumber', 'NormalizedReferenceNumber', 'InstrumentDate'))
    INTO reserved_objects;

    IF NOT migration_applied AND reserved_objects > 0 THEN
        RAISE EXCEPTION 'Partial Payment & Receipt schema detected; reconcile it before applying the EF migration';
    END IF;

    IF EXISTS (
        SELECT 1 FROM "ApprovalVouchers"
        WHERE "Status" NOT IN ('Pending', 'Approved', 'Rejected', 'PostedNormally')
           OR "RequestKind" NOT IN ('New', 'Revision')
    ) THEN
        RAISE EXCEPTION 'Existing approval rows contain unsupported status or request-kind values';
    END IF;
END $$;

SELECT 'Payment & Receipt preflight passed' AS result;
