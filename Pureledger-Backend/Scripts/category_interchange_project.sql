-- ============================================================================
-- CATEGORY INTERCHANGE BETWEEN PROJECTS
-- ============================================================================
-- Purpose: Safely MOVE categories from one project to another.
--
-- SCENARIO: Source project has categories but NO journal entries.
--           Target project has existing categories, accounts, and entries.
--
-- KEY RELATIONSHIPS:
--   Project (1) -> Categories (Many)
--       Category (1) -> Accounts (Many)
--       Category (1) -> JournalEntries (Many) [via CategoryId FK]
--
-- BLOCKING CONDITIONS (Script will NOT proceed if):
--   1. Category has journal entries (data integrity violation)
--   2. Target project already has category with same name (unique constraint)
--   3. Category already belongs to target project
--
-- Run Scripts 1-5 to ANALYZE (safe SELECT queries)
-- Review Script 5 output - must show "OK TO PROCEED"
-- Run Script 6 to EXECUTE (UPDATE with transaction)
-- Run Script 7 to VERIFY
-- ============================================================================


-- ============================================================================
-- PARAMETERS - REPLACE THESE VALUES BEFORE RUNNING
-- ============================================================================
-- Replace the following placeholders with actual values:
--
--   @CATEGORY_IDS    = ARRAY of CategoryIds to move, e.g.: ARRAY[123, 456, 789]
--   @TARGET_PROJECT  = Target ProjectId, e.g.: 2
--
-- Example usage:
--   SELECT * FROM "Categories" WHERE "CategoryId" = ANY(ARRAY[123, 456]);
--   Then update categories to ProjectId = 2
-- ============================================================================


-- ============================================================================
-- SCRIPT 1: Parameter Validation - Verify inputs exist
-- ============================================================================
-- Validates that the specified categories and target project exist.
-- Returns the categories to move and their current project assignment.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds
-- REPLACE: 999 with your target ProjectId

SELECT
    'CATEGORIES TO MOVE' as "Check",
    c."CategoryId",
    c."CategoryName",
    c."ProjectId" as "CurrentProjectId",
    p."ProjectName" as "CurrentProject",
    c."AccountTypeId"
FROM "Categories" c
JOIN "Projects" p ON c."ProjectId" = p."ProjectId"
WHERE c."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds to move
ORDER BY c."CategoryId";

-- Verify target project exists
SELECT
    'TARGET PROJECT' as "Check",
    "ProjectId",
    "ProjectName"
FROM "Projects"
WHERE "ProjectId" = 1;  -- <-- REPLACE with target ProjectId


-- ============================================================================
-- SCRIPT 2: Unique Constraint Check - Name conflicts in target project
-- ============================================================================
-- BLOCKING CHECK: If any rows returned, the move will FAIL due to
-- UNIQUE(CategoryName, ProjectId) constraint on Categories table.
--
-- Returns categories that would violate unique constraint.
-- MUST return 0 rows to proceed.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds
-- REPLACE: 999 with your target ProjectId

SELECT
    'NAME CONFLICT - BLOCKING' as "Status",
    source."CategoryId" as "SourceCategoryId",
    source."CategoryName",
    source."ProjectId" as "SourceProjectId",
    existing."CategoryId" as "ConflictingCategoryId",
    existing."ProjectId" as "TargetProjectId"
FROM "Categories" source
JOIN "Categories" existing ON source."CategoryName" = existing."CategoryName"
WHERE source."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds
  AND existing."ProjectId" = 1               -- <-- REPLACE with target ProjectId
  AND source."CategoryId" != existing."CategoryId";


-- ============================================================================
-- SCRIPT 3: Journal Entries Check - CRITICAL DATA INTEGRITY
-- ============================================================================
-- BLOCKING CHECK: If any rows returned, the move will VIOLATE data integrity.
-- JournalEntry.CategoryId would point to a category in a DIFFERENT project
-- than the JournalVoucher.ProjectId - this breaks the data model.
--
-- Returns categories that have journal entries.
-- MUST return 0 rows to proceed.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds

SELECT
    'HAS ENTRIES - BLOCKING' as "Status",
    c."CategoryId",
    c."CategoryName",
    c."ProjectId" as "CategoryProjectId",
    COUNT(je."JournalEntryId") as "EntryCount",
    MIN(jv."EntryDate") as "FirstEntryDate",
    MAX(jv."EntryDate") as "LastEntryDate"
FROM "Categories" c
JOIN "JournalEntries" je ON c."CategoryId" = je."CategoryId"
JOIN "JournalVouchers" jv ON je."JournalVoucherId" = jv."JournalVoucherId"
WHERE c."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds
GROUP BY c."CategoryId", c."CategoryName", c."ProjectId"
HAVING COUNT(je."JournalEntryId") > 0
ORDER BY c."CategoryId";


-- ============================================================================
-- SCRIPT 4: Dependent Data Impact Analysis
-- ============================================================================
-- Shows all related data that will be affected by the move.
-- These are informational - the move will still work, but you should
-- review if these references make sense after the move.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds

-- 4A: Accounts under these categories (will move WITH the category)
SELECT
    'ACCOUNTS (will move with category)' as "Impact",
    c."CategoryId",
    c."CategoryName",
    a."AccountId",
    a."AccountName",
    a."IsEnabled"
FROM "Categories" c
JOIN "Accounts" a ON c."CategoryId" = a."CategoryId"
WHERE c."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds
ORDER BY c."CategoryId", a."AccountName";

-- 4B: UserCategories - User permissions linked to these categories
SELECT
    'USER CATEGORIES (review permissions)' as "Impact",
    uc."UserCategoryId",
    uc."UserId",
    u."FullName" as "UserName",
    c."CategoryId",
    c."CategoryName"
FROM "UserCategories" uc
JOIN "Categories" c ON uc."CategoryId" = c."CategoryId"
JOIN "Users" u ON uc."UserId" = u."UserId"
WHERE c."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds
ORDER BY c."CategoryId", u."FullName";

delete from "UserCategories"
where "UserCategoryId" = 2;


-- 4C: ProjectCartageSettings - Categories used in cartage settings
SELECT
    'CARTAGE SETTINGS (review after move)' as "Impact",
    pcs."Id" as "SettingsId",
    pcs."ProjectId",
    p."ProjectName",
    'ForExpense' as "SettingType",
    pcs."ForExpenseCategoryId" as "CategoryId",
    c."CategoryName"
FROM "ProjectCartageSettings" pcs
JOIN "Categories" c ON pcs."ForExpenseCategoryId" = c."CategoryId"
JOIN "Projects" p ON pcs."ProjectId" = p."ProjectId"
WHERE c."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds

UNION ALL

SELECT
    'CARTAGE SETTINGS (review after move)' as "Impact",
    pcs."Id" as "SettingsId",
    pcs."ProjectId",
    p."ProjectName",
    'CartageTransporter' as "SettingType",
    pcs."CartageTransporterCategoryId" as "CategoryId",
    c."CategoryName"
FROM "ProjectCartageSettings" pcs
JOIN "Categories" c ON pcs."CartageTransporterCategoryId" = c."CategoryId"
JOIN "Projects" p ON pcs."ProjectId" = p."ProjectId"
WHERE c."CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds
ORDER BY "SettingsId";

-- 4D: DashboardSettings - Category IDs stored in JSON arrays
-- Note: This checks if any CategoryId appears in the JSON text
SELECT
    'DASHBOARD SETTINGS (review JSON arrays)' as "Impact",
    ds."DashboardSettingsId",
    ds."ProjectId",
    p."ProjectName",
    ds."RevenueCategoryIds",
    ds."PurchaseCategoryIds",
    ds."ExpenseCategoryIds"
FROM "DashboardSettings" ds
JOIN "Projects" p ON ds."ProjectId" = p."ProjectId"
WHERE EXISTS (
    SELECT 1 FROM unnest(ARRAY[1,2,3]) AS cat_id  -- <-- REPLACE with CategoryIds
    WHERE ds."RevenueCategoryIds" LIKE '%' || cat_id || '%'
       OR ds."PurchaseCategoryIds" LIKE '%' || cat_id || '%'
       OR ds."ExpenseCategoryIds" LIKE '%' || cat_id || '%'
);

-- 4E: RecipeItems - Categories used in recipes
SELECT
    'RECIPE ITEMS (review after move)' as "Impact",
    ri."RecipeItemId",
    ri."RecipeId",
    r."RecipeName",
    c."CategoryId",
    c."CategoryName",
    a."AccountName"
FROM "RecipeItems" ri
JOIN "Categories" c ON ri."CategoryId" = c."CategoryId"
JOIN "Accounts" a ON ri."AccountId" = a."AccountId"
JOIN "Recipes" r ON ri."RecipeId" = r."RecipeId"
WHERE c."CategoryId" = ANY(ARRAY[1,2,3])  -- <-- REPLACE with CategoryIds
ORDER BY r."RecipeName", c."CategoryName";


-- ============================================================================
-- SCRIPT 5: Pre-Move Summary - GO/NO-GO Decision
-- ============================================================================
-- Consolidated check that determines if the move is safe to execute.
-- Look for "OK TO PROCEED" in the Status column.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds
-- REPLACE: 999 with your target ProjectId

WITH params AS (
    SELECT
        ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75] as category_ids,  -- <-- REPLACE with CategoryIds
        1 as target_project_id       -- <-- REPLACE with target ProjectId
),
categories_exist AS (
    SELECT COUNT(*) as cnt
    FROM "Categories" c, params p
    WHERE c."CategoryId" = ANY(p.category_ids)
),
target_exists AS (
    SELECT COUNT(*) as cnt
    FROM "Projects" p, params pm
    WHERE p."ProjectId" = pm.target_project_id
),
name_conflicts AS (
    SELECT COUNT(*) as cnt
    FROM "Categories" source, "Categories" existing, params p
    WHERE source."CategoryId" = ANY(p.category_ids)
      AND existing."ProjectId" = p.target_project_id
      AND source."CategoryName" = existing."CategoryName"
      AND source."CategoryId" != existing."CategoryId"
),
has_entries AS (
    SELECT COUNT(DISTINCT c."CategoryId") as cnt
    FROM "Categories" c
    JOIN "JournalEntries" je ON c."CategoryId" = je."CategoryId"
    , params p
    WHERE c."CategoryId" = ANY(p.category_ids)
),
already_in_target AS (
    SELECT COUNT(*) as cnt
    FROM "Categories" c, params p
    WHERE c."CategoryId" = ANY(p.category_ids)
      AND c."ProjectId" = p.target_project_id
)
SELECT
    CASE
        WHEN ce.cnt = 0 THEN 'BLOCKED - No categories found with specified IDs'
        WHEN te.cnt = 0 THEN 'BLOCKED - Target project does not exist'
        WHEN nc.cnt > 0 THEN 'BLOCKED - Name conflict(s) in target project (' || nc.cnt || ' conflicts)'
        WHEN he.cnt > 0 THEN 'BLOCKED - Category/ies have journal entries (' || he.cnt || ' categories)'
        WHEN ait.cnt > 0 THEN 'BLOCKED - Category/ies already in target project (' || ait.cnt || ' categories)'
        ELSE 'OK TO PROCEED'
    END as "Status",
    ce.cnt as "CategoriesFound",
    te.cnt as "TargetProjectExists",
    nc.cnt as "NameConflicts",
    he.cnt as "CategoriesWithEntries",
    ait.cnt as "AlreadyInTarget",
    (SELECT array_length(category_ids, 1) FROM params) as "RequestedCount"
FROM categories_exist ce, target_exists te, name_conflicts nc, has_entries he, already_in_target ait;


-- ============================================================================
-- SCRIPT 6: EXECUTE THE MOVE
-- ============================================================================
-- CAUTION: This permanently moves categories to the target project!
-- Run Scripts 1-5 first and verify Script 5 shows "OK TO PROCEED".
--
-- The UPDATE includes embedded safety checks in the WHERE clause:
--   - Category must not have any journal entries
--   - Category name must not exist in target project
--   - Category must not already be in target project
--
-- If 0 rows updated, one of the blocking conditions was true.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds
-- REPLACE: 999 with your target ProjectId

BEGIN;

UPDATE "Categories"
SET "ProjectId" = 1  -- <-- REPLACE with target ProjectId
WHERE "CategoryId" = ANY(ARRAY[85,77,86,76,79,80,84,74,78,81,82,83,75])  -- <-- REPLACE with CategoryIds
  -- Safety: Category must not have journal entries
  AND NOT EXISTS (
      SELECT 1 FROM "JournalEntries" je
      WHERE je."CategoryId" = "Categories"."CategoryId"
  )
  -- Safety: Category name must not exist in target project
  AND NOT EXISTS (
      SELECT 1 FROM "Categories" existing
      WHERE existing."ProjectId" = 1  -- <-- REPLACE with target ProjectId
        AND existing."CategoryName" = "Categories"."CategoryName"
        AND existing."CategoryId" != "Categories"."CategoryId"
  )
  -- Safety: Category must not already be in target project
  AND "ProjectId" != 1;  -- <-- REPLACE with target ProjectId

-- Show how many rows were updated
-- If 0 rows: one of the safety conditions blocked the update
-- If < expected: some categories were blocked

COMMIT;


-- ============================================================================
-- SCRIPT 7: Post-Move Verification
-- ============================================================================
-- Run after Script 6 to verify the move was successful.
--
-- REPLACE: ARRAY[1,2,3] with your CategoryIds
-- REPLACE: 999 with your target ProjectId

-- 7A: Verify categories are now in target project
SELECT
    'MOVED CATEGORIES' as "Verification",
    c."CategoryId",
    c."CategoryName",
    c."ProjectId",
    p."ProjectName",
    c."AccountTypeId"
FROM "Categories" c
JOIN "Projects" p ON c."ProjectId" = p."ProjectId"
WHERE c."CategoryId" = ANY(ARRAY[1,2,3])  -- <-- REPLACE with CategoryIds
ORDER BY c."CategoryId";

-- 7B: Verify accounts are accessible under new project
SELECT
    'ACCOUNTS UNDER MOVED CATEGORIES' as "Verification",
    c."CategoryId",
    c."CategoryName",
    c."ProjectId" as "NewProjectId",
    a."AccountId",
    a."AccountName",
    a."IsEnabled"
FROM "Categories" c
JOIN "Accounts" a ON c."CategoryId" = a."CategoryId"
WHERE c."CategoryId" = ANY(ARRAY[1,2,3])  -- <-- REPLACE with CategoryIds
ORDER BY c."CategoryId", a."AccountName";

-- 7C: Confirm no orphaned entries (should return 0 rows)
-- This checks for any journal entries whose category is in a different project
-- than their voucher - would indicate data integrity issue
SELECT
    'ORPHANED ENTRIES (should be 0 rows)' as "Warning",
    je."JournalEntryId",
    je."CategoryId",
    c."ProjectId" as "CategoryProjectId",
    jv."JournalVoucherId",
    jv."ProjectId" as "VoucherProjectId"
FROM "JournalEntries" je
JOIN "Categories" c ON je."CategoryId" = c."CategoryId"
JOIN "JournalVouchers" jv ON je."JournalVoucherId" = jv."JournalVoucherId"
WHERE c."CategoryId" = ANY(ARRAY[1,2,3])  -- <-- REPLACE with CategoryIds
  AND c."ProjectId" != jv."ProjectId";


-- ============================================================================
-- ROLLBACK GUIDANCE
-- ============================================================================
-- If you need to undo the move, run:
--
-- UPDATE "Categories"
-- SET "ProjectId" = @ORIGINAL_PROJECT_ID
-- WHERE "CategoryId" = ANY(ARRAY[...CategoryIds...]);
--
-- NOTE: This assumes no data was modified after the move.
-- If journal entries were created in the target project using these
-- categories, rolling back would cause data integrity issues.
-- ============================================================================


-- ============================================================================
-- CLEANUP RECOMMENDATIONS (Post-Move)
-- ============================================================================
-- After moving categories, consider reviewing:
--
-- 1. UserCategories - Users may need permissions adjusted for new project
--    DELETE FROM "UserCategories" WHERE "CategoryId" = ANY(ARRAY[...]);
--
-- 2. ProjectCartageSettings - May reference moved categories
--    UPDATE "ProjectCartageSettings" SET "ForExpenseCategoryId" = NULL
--    WHERE "ForExpenseCategoryId" = ANY(ARRAY[...]);
--
-- 3. DashboardSettings - JSON arrays may contain moved category IDs
--    Manually update the JSON arrays in the dashboard settings
--
-- 4. RecipeItems - May reference moved categories
--    Review recipes that use these categories
-- ============================================================================
