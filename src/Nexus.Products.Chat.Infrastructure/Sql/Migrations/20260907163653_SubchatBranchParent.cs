using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Products.Chat.Infrastructure.Sql.Migrations
{
    /// <inheritdoc />
    public partial class SubchatBranchParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentBranchId",
                schema: "session",
                table: "Branch",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branch_ParentBranchId",
                schema: "session",
                table: "Branch",
                column: "ParentBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Branch_Branch_ParentBranchId",
                schema: "session",
                table: "Branch",
                column: "ParentBranchId",
                principalSchema: "session",
                principalTable: "Branch",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branch_Branch_ParentBranchId",
                schema: "session",
                table: "Branch");

            migrationBuilder.DropIndex(
                name: "IX_Branch_ParentBranchId",
                schema: "session",
                table: "Branch");

            migrationBuilder.DropColumn(
                name: "ParentBranchId",
                schema: "session",
                table: "Branch");
        }
    }
}
