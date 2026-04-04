using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RegularDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegular",
                table: "Incomes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RegularIncomeDefinitionId",
                table: "Incomes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegularIncomeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegularIncomeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegularIncomeDefinitions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_RegularIncomeDefinitionId",
                table: "Incomes",
                column: "RegularIncomeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegularIncomeDefinitions_AccountId",
                table: "RegularIncomeDefinitions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RegularIncomeDefinitions_IsActive",
                table: "RegularIncomeDefinitions",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_RegularIncomeDefinitions_RegularIncomeDefinitionId",
                table: "Incomes",
                column: "RegularIncomeDefinitionId",
                principalTable: "RegularIncomeDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_RegularIncomeDefinitions_RegularIncomeDefinitionId",
                table: "Incomes");

            migrationBuilder.DropTable(
                name: "RegularIncomeDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_RegularIncomeDefinitionId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "IsRegular",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "RegularIncomeDefinitionId",
                table: "Incomes");
        }
    }
}
