using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanOperationAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanOperationAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BudgetOwnerUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduleVersionBefore = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScheduleVersionAfter = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OperationPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    RevertedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevertedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RevertsOperationId = table.Column<int>(type: "integer", nullable: true),
                    RevertedByOperationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanOperationAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanOperationAudits_LoanOperationAudits_RevertedByOperation~",
                        column: x => x.RevertedByOperationId,
                        principalTable: "LoanOperationAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOperationAudits_LoanOperationAudits_RevertsOperationId",
                        column: x => x.RevertsOperationId,
                        principalTable: "LoanOperationAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOperationAudits_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOperationAudits_Users_BudgetOwnerUserId",
                        column: x => x.BudgetOwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOperationAudits_Users_RevertedByUserId",
                        column: x => x.RevertedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOperationAudits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_BudgetOwnerUserId",
                table: "LoanOperationAudits",
                column: "BudgetOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_LoanId",
                table: "LoanOperationAudits",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_OccurredAtUtc",
                table: "LoanOperationAudits",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_OperationType",
                table: "LoanOperationAudits",
                column: "OperationType");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_RevertedByOperationId",
                table: "LoanOperationAudits",
                column: "RevertedByOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_RevertedByUserId",
                table: "LoanOperationAudits",
                column: "RevertedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_RevertsOperationId",
                table: "LoanOperationAudits",
                column: "RevertsOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_Status",
                table: "LoanOperationAudits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOperationAudits_UserId",
                table: "LoanOperationAudits",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanOperationAudits");
        }
    }
}
