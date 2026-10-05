using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.ControlAcceso.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSesion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SesionesValidasDesde",
                table: "Usuarios",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "TokensInvalidados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Jti = table.Column<string>(type: "TEXT", nullable: false),
                    FechaExpiracionToken = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensInvalidados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokensInvalidados_Jti",
                table: "TokensInvalidados",
                column: "Jti",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokensInvalidados");

            migrationBuilder.DropColumn(
                name: "SesionesValidasDesde",
                table: "Usuarios");
        }
    }
}
