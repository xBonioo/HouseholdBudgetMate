using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanPercentageChargeAndOriginalInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GracePeriodMonths",
                table: "Loans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrincipal",
                table: "Loans",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPercentageBased",
                table: "LoanCharges",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GracePeriodMonths",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "OriginalPrincipal",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "IsPercentageBased",
                table: "LoanCharges");
        }
    }
}
