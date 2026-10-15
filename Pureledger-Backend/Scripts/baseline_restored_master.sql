-- Use only after restoring a master database and checking its schema against
-- VpsMasterBaseline. This registers existing tables; it does not recreate them.
BEGIN;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
               WHERE "MigrationId" = '20261003102217_VpsMasterBaseline') THEN
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
                   WHERE "MigrationId" = '20260420153759_AddLegacyDatabaseSetting')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
                      WHERE table_schema = 'public'
                        AND table_name = 'ProjectCartageSettings'
                        AND column_name = 'ProjectImage')
       OR EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = 'JournalVouchers'
                    AND column_name = 'IsStockTransfer')
       OR to_regclass('public."ApprovalVouchers"') IS NOT NULL
       OR to_regclass('public."ProjectImg"') IS NOT NULL THEN
        RAISE EXCEPTION 'Database does not match the expected restored master baseline. Review the schema before proceeding.';
    END IF;

    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261003102217_VpsMasterBaseline', '9.0.6');
END $$;
COMMIT;
