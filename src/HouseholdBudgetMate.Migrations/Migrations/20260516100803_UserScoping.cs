using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class UserScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonthPlans_Year_Month",
                table: "MonthPlans");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "RegularIncomeDefinitions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "RegularExpenseDefinitions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MonthSavingsTransferItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MonthPlans",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Incomes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Expenses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ExpenseLineItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Accounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "AccountMonthBalances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default-user");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HouseholdMode = table.Column<int>(type: "integer", nullable: false),
                    BudgetOwnerUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_BudgetOwnerUserId",
                        column: x => x.BudgetOwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id",
                    "Username",
                    "PasswordHash",
                    "HouseholdMode",
                    "BudgetOwnerUserId",
                    "CreatedAtUtc",
                    "UpdatedAtUtc"
                },
                values: new object[]
                {
                    "default-user",
                    "default",
                    string.Empty,
                    1,
                    "default-user",
                    new DateTime(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegularIncomeDefinitions_UserId",
                table: "RegularIncomeDefinitions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RegularExpenseDefinitions_UserId",
                table: "RegularExpenseDefinitions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthSavingsTransferItems_UserId",
                table: "MonthSavingsTransferItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthPlans_UserId",
                table: "MonthPlans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthPlans_UserId_Year_Month",
                table: "MonthPlans",
                columns: new[] { "UserId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_UserId",
                table: "Incomes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UserId",
                table: "Expenses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLineItems_UserId",
                table: "ExpenseLineItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMonthBalances_UserId",
                table: "AccountMonthBalances",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BudgetOwnerUserId",
                table: "Users",
                column: "BudgetOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountMonthBalances_Users_UserId",
                table: "AccountMonthBalances",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_UserId",
                table: "Accounts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseLineItems_Users_UserId",
                table: "ExpenseLineItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_UserId",
                table: "Expenses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_Users_UserId",
                table: "Incomes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthPlans_Users_UserId",
                table: "MonthPlans",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthSavingsTransferItems_Users_UserId",
                table: "MonthSavingsTransferItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegularExpenseDefinitions_Users_UserId",
                table: "RegularExpenseDefinitions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegularIncomeDefinitions_Users_UserId",
                table: "RegularIncomeDefinitions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountMonthBalances_Users_UserId",
                table: "AccountMonthBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_UserId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseLineItems_Users_UserId",
                table: "ExpenseLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_UserId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_Users_UserId",
                table: "Incomes");

            migrationBuilder.DropForeignKey(
                name: "FK_MonthPlans_Users_UserId",
                table: "MonthPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_MonthSavingsTransferItems_Users_UserId",
                table: "MonthSavingsTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RegularExpenseDefinitions_Users_UserId",
                table: "RegularExpenseDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_RegularIncomeDefinitions_Users_UserId",
                table: "RegularIncomeDefinitions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_RegularIncomeDefinitions_UserId",
                table: "RegularIncomeDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_RegularExpenseDefinitions_UserId",
                table: "RegularExpenseDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_MonthSavingsTransferItems_UserId",
                table: "MonthSavingsTransferItems");

            migrationBuilder.DropIndex(
                name: "IX_MonthPlans_UserId",
                table: "MonthPlans");

            migrationBuilder.DropIndex(
                name: "IX_MonthPlans_UserId_Year_Month",
                table: "MonthPlans");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_UserId",
                table: "Incomes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_UserId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseLineItems_UserId",
                table: "ExpenseLineItems");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_AccountMonthBalances_UserId",
                table: "AccountMonthBalances");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RegularIncomeDefinitions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RegularExpenseDefinitions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MonthSavingsTransferItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MonthPlans");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ExpenseLineItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AccountMonthBalances");

            migrationBuilder.CreateIndex(
                name: "IX_MonthPlans_Year_Month",
                table: "MonthPlans",
                columns: new[] { "Year", "Month" },
                unique: true);
        }
    }
}
