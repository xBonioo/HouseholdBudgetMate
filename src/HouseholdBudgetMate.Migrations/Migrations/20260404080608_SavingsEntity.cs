using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SavingsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SavingsTransferAmount",
                table: "MonthPlans");

            migrationBuilder.DropColumn(
                name: "SavingsTransferDate",
                table: "MonthPlans");

            migrationBuilder.CreateTable(
                name: "MonthSavingsTransferItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MonthPlanId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthSavingsTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthSavingsTransferItems_MonthPlans_MonthPlanId",
                        column: x => x.MonthPlanId,
                        principalTable: "MonthPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthSavingsTransferItems_MonthPlanId",
                table: "MonthSavingsTransferItems",
                column: "MonthPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthSavingsTransferItems_TransferDate",
                table: "MonthSavingsTransferItems",
                column: "TransferDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthSavingsTransferItems");

            migrationBuilder.AddColumn<decimal>(
                name: "SavingsTransferAmount",
                table: "MonthPlans",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SavingsTransferDate",
                table: "MonthPlans",
                type: "date",
                nullable: true);
        }
    }
}
