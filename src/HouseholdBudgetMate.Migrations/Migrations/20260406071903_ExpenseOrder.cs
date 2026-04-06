using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_MonthPlanId",
                table: "Expenses");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_MonthPlanId_Order",
                table: "Expenses",
                columns: new[] { "MonthPlanId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_MonthPlanId_Order",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_MonthPlanId",
                table: "Expenses",
                column: "MonthPlanId");
        }
    }
}
