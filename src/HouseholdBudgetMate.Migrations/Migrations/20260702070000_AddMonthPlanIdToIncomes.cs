using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260702070000_AddMonthPlanIdToIncomes")]
    public partial class AddMonthPlanIdToIncomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Incomes_Year_Month";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Incomes_Year_Month_RegularIncomeDefinitionId";""");
            migrationBuilder.Sql("""ALTER TABLE "Incomes" ADD COLUMN IF NOT EXISTS "MonthPlanId" integer;""");

            migrationBuilder.Sql(
                """
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
                """);

            migrationBuilder.Sql(
                """
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
                """);

            migrationBuilder.Sql("""ALTER TABLE "Incomes" ALTER COLUMN "MonthPlanId" SET NOT NULL;""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_Incomes_MonthPlanId" ON "Incomes" ("MonthPlanId");""");
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Incomes_MonthPlanId_RegularIncomeDefinitionId"
                ON "Incomes" ("MonthPlanId", "RegularIncomeDefinitionId");
                """);
            migrationBuilder.Sql(
                """
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
                """);
            migrationBuilder.Sql("""ALTER TABLE "Incomes" DROP COLUMN IF EXISTS "Year";""");
            migrationBuilder.Sql("""ALTER TABLE "Incomes" DROP COLUMN IF EXISTS "Month";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Incomes" ADD COLUMN IF NOT EXISTS "Month" integer NOT NULL DEFAULT 0;""");
            migrationBuilder.Sql("""ALTER TABLE "Incomes" ADD COLUMN IF NOT EXISTS "Year" integer NOT NULL DEFAULT 0;""");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'Incomes'
                          AND column_name = 'MonthPlanId'
                    ) THEN
                        UPDATE "Incomes"
                        SET "Year" = mp."Year",
                            "Month" = mp."Month"
                        FROM "MonthPlans" mp
                        WHERE mp."Id" = "Incomes"."MonthPlanId";
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""ALTER TABLE "Incomes" DROP CONSTRAINT IF EXISTS "FK_Incomes_MonthPlans_MonthPlanId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Incomes_MonthPlanId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Incomes_MonthPlanId_RegularIncomeDefinitionId";""");
            migrationBuilder.Sql("""ALTER TABLE "Incomes" DROP COLUMN IF EXISTS "MonthPlanId";""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_Incomes_Year_Month" ON "Incomes" ("Year", "Month");""");
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Incomes_Year_Month_RegularIncomeDefinitionId"
                ON "Incomes" ("Year", "Month", "RegularIncomeDefinitionId");
                """);
        }
    }
}
