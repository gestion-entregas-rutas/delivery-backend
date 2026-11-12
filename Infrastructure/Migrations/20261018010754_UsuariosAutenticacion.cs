using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsuariosAutenticacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreUsuario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClaveHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    DebeCambiarCredenciales = table.Column<bool>(type: "boolean", nullable: false),
                    SelloSeguridad = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreNormalizado",
                table: "Usuarios",
                column: "NombreNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RepartidorId",
                table: "Usuarios",
                column: "RepartidorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
