START TRANSACTION;
DROP INDEX IF EXISTS "IX_Incomes_Year_Month";

DROP INDEX IF EXISTS "IX_Incomes_Year_Month_RegularIncomeDefinitionId";

ALTER TABLE "Incomes" ADD COLUMN IF NOT EXISTS "MonthPlanId" integer;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'Incomes'
          AND column_name = 'Year'
    )
    AND EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'Incomes'
          AND column_name = 'Month'
    ) THEN
INSERT INTO "MonthPlans" ("UserId", "Year", "Month", "IsClosed", "CreatedAtUtc", "UpdatedAtUtc")
SELECT DISTINCT i."UserId", i."Year", i."Month", FALSE, now(), now()
FROM "Incomes" i
WHERE NOT EXISTS (
    SELECT 1
    FROM "MonthPlans" mp
    WHERE mp."UserId" = i."UserId"
      AND mp."Year" = i."Year"
      AND mp."Month" = i."Month"
);
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'Incomes'
          AND column_name = 'Year'
    )
    AND EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'Incomes'
          AND column_name = 'Month'
    ) THEN
UPDATE "Incomes"
SET "MonthPlanId" = (
    SELECT mp."Id"
    FROM "MonthPlans" mp
    WHERE mp."UserId" = "Incomes"."UserId"
      AND mp."Year" = "Incomes"."Year"
      AND mp."Month" = "Incomes"."Month"
    LIMIT 1
)
WHERE "MonthPlanId" IS NULL;
    END IF;
END $$;

ALTER TABLE "Incomes" ALTER COLUMN "MonthPlanId" SET NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_Incomes_MonthPlanId" ON "Incomes" ("MonthPlanId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Incomes_MonthPlanId_RegularIncomeDefinitionId"
ON "Incomes" ("MonthPlanId", "RegularIncomeDefinitionId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Incomes_MonthPlans_MonthPlanId'
    ) THEN
        ALTER TABLE "Incomes"
        ADD CONSTRAINT "FK_Incomes_MonthPlans_MonthPlanId"
        FOREIGN KEY ("MonthPlanId")
        REFERENCES "MonthPlans" ("Id")
        ON DELETE RESTRICT;
    END IF;
END $$;

ALTER TABLE "Incomes" DROP COLUMN IF EXISTS "Year";

ALTER TABLE "Incomes" DROP COLUMN IF EXISTS "Month";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260702070000_AddMonthPlanIdToIncomes', '10.0.7');

COMMIT;

