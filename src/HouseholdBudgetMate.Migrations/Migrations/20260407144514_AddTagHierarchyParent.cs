using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTagHierarchyParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_CategoryId",
                table: "Tags");

            migrationBuilder.AddColumn<int>(
                name: "ParentTagId",
                table: "Tags",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_CategoryId_ParentTagId",
                table: "Tags",
                columns: new[] { "CategoryId", "ParentTagId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_ParentTagId",
                table: "Tags",
                column: "ParentTagId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Tags_ParentTagId",
                table: "Tags",
                column: "ParentTagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Tags_ParentTagId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_CategoryId_ParentTagId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_ParentTagId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "ParentTagId",
                table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_CategoryId",
                table: "Tags",
                column: "CategoryId");
        }
    }
}
