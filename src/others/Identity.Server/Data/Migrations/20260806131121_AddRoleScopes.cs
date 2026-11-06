using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleScopes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleScopes_RoleId_Scope",
                table: "RoleScopes",
                columns: new[] { "RoleId", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleScopes");
        }
    }
}
