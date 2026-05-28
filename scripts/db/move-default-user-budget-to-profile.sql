-- PostgreSQL one-time migration: move the existing technical-owner budget
-- scope from default-user to the selected interactive profile.
--
-- Target profile: aab0486067634e24a8a283a2c0a60800
--
-- Important:
-- 1. S-01 normally keeps shared household data owned by default-user.
--    Running this script intentionally changes that model to a separate
--    budget owned by the target profile.
-- 2. "AuditLogs"."UserId" is audit actor history, not budget ownership.
--    It is changed below because this script explicitly requests replacing
--    UserId in every table. Remove that UPDATE if actor history must remain
--    unchanged.
-- 3. This script aborts if another visible profile still points at
--    default-user, because moving the rows would make its shared budget empty.
-- 4. Take a database backup before executing this script.
-- 5. HeidiSQL: execute the whole script with F9 / Run batch. The DELIMITER
--    directives keep semicolons inside the PostgreSQL DO block together.

BEGIN;

DELIMITER //
DO $migration$
DECLARE
    source_user_id constant text := 'default-user';
    target_user_id constant text := 'aab0486067634e24a8a283a2c0a60800';
    conflicting_month_plans bigint;
    dependent_profiles bigint;
    affected_rows bigint;
BEGIN
    IF target_user_id = source_user_id THEN
        RAISE EXCEPTION 'Source and target user IDs must differ.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = source_user_id) THEN
        RAISE EXCEPTION 'Source user % does not exist.', source_user_id;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = target_user_id) THEN
        RAISE EXCEPTION 'Target user % does not exist.', target_user_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM "Users"
        WHERE "Id" = target_user_id
          AND "PasswordHash" LIKE 'PBKDF2-SHA256:%'
    ) THEN
        RAISE EXCEPTION 'Target user % is not a PIN-protected interactive profile.', target_user_id;
    END IF;

    SELECT COUNT(*)
    INTO dependent_profiles
    FROM "Users"
    WHERE "Id" NOT IN (source_user_id, target_user_id)
      AND "BudgetOwnerUserId" = source_user_id;

    IF dependent_profiles > 0 THEN
        RAISE EXCEPTION
            '% other profile(s) still share budget owner %. Decide how they should be rebound before migrating.',
            dependent_profiles,
            source_user_id;
    END IF;

    SELECT COUNT(*)
    INTO conflicting_month_plans
    FROM "MonthPlans" source_plan
    JOIN "MonthPlans" target_plan
      ON target_plan."UserId" = target_user_id
     AND source_plan."UserId" = source_user_id
     AND target_plan."Year" = source_plan."Year"
     AND target_plan."Month" = source_plan."Month";

    IF conflicting_month_plans > 0 THEN
        RAISE EXCEPTION
            'Target profile already has % monthly plan(s) colliding with default-user.',
            conflicting_month_plans;
    END IF;

    UPDATE "Accounts"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'Accounts: % row(s) moved.', affected_rows;

    UPDATE "AccountMonthBalances"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'AccountMonthBalances: % row(s) moved.', affected_rows;

    UPDATE "Expenses"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'Expenses: % row(s) moved.', affected_rows;

    UPDATE "ExpenseLineItems"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'ExpenseLineItems: % row(s) moved.', affected_rows;

    UPDATE "Incomes"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'Incomes: % row(s) moved.', affected_rows;

    UPDATE "Loans"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'Loans: % row(s) moved.', affected_rows;

    UPDATE "MonthPlans"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'MonthPlans: % row(s) moved.', affected_rows;

    UPDATE "MonthSavingsTransferItems"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'MonthSavingsTransferItems: % row(s) moved.', affected_rows;

    UPDATE "RegularExpenseDefinitions"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'RegularExpenseDefinitions: % row(s) moved.', affected_rows;

    UPDATE "RegularIncomeDefinitions"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'RegularIncomeDefinitions: % row(s) moved.', affected_rows;

    UPDATE "AuditLogs"
    SET "BudgetOwnerUserId" = target_user_id
    WHERE "BudgetOwnerUserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'AuditLogs budget ownership: % row(s) moved.', affected_rows;

    UPDATE "AuditLogs"
    SET "UserId" = target_user_id
    WHERE "UserId" = source_user_id;
    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RAISE NOTICE 'AuditLogs actor UserId: % row(s) rewritten.', affected_rows;

    UPDATE "Users"
    SET "HouseholdMode" = 2,
        "BudgetOwnerUserId" = target_user_id,
        "UpdatedAtUtc" = CURRENT_TIMESTAMP
    WHERE "Id" = target_user_id;

    RAISE NOTICE 'Target profile % now owns its separate budget scope.', target_user_id;
END;
$migration$//
DELIMITER ;

COMMIT;
