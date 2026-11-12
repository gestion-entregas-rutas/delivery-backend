using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AmpliacionPedidosYUbicaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Detalles_DestinatarioCorreo",
                table: "Pedidos",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Detalles_Fragil",
                table: "Pedidos",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalles_Observaciones",
                table: "Pedidos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Detalles_PesoKg",
                table: "Pedidos",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalles_Prioridad",
                table: "Pedidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalles_RemitenteCorreo",
                table: "Pedidos",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalles_RemitenteNombre",
                table: "Pedidos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalles_RemitenteTelefono",
                table: "Pedidos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalles_TipoCarga",
                table: "Pedidos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UbicacionesRepartidores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Punto = table.Column<Point>(type: "geometry (point, 4326)", nullable: false),
                    PrecisionMetros = table.Column<double>(type: "double precision", nullable: false),
                    CapturadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecibidaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UbicacionesRepartidores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UbicacionesRepartidores_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UbicacionesRepartidores_Rutas_RutaId",
                        column: x => x.RutaId,
                        principalTable: "Rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesRepartidores_RepartidorId",
                table: "UbicacionesRepartidores",
                column: "RepartidorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesRepartidores_RutaId",
                table: "UbicacionesRepartidores",
                column: "RutaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UbicacionesRepartidores");

            migrationBuilder.DropColumn(
                name: "Detalles_DestinatarioCorreo",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_Fragil",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_Observaciones",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_PesoKg",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_Prioridad",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_RemitenteCorreo",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_RemitenteNombre",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_RemitenteTelefono",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Detalles_TipoCarga",
                table: "Pedidos");
        }
    }
}
