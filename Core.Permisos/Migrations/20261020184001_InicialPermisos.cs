using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Permisos.Migrations
{
    /// <inheritdoc />
    public partial class InicialPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermisosOtorgados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    Permiso = table.Column<string>(type: "TEXT", nullable: false),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaOtorgado = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosOtorgados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Solicitudes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitanteId = table.Column<int>(type: "INTEGER", nullable: false),
                    PermisoSolicitado = table.Column<string>(type: "TEXT", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    AprobadorId = table.Column<int>(type: "INTEGER", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitudes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermisosOtorgados");

            migrationBuilder.DropTable(
                name: "Solicitudes");
        }
    }
}
