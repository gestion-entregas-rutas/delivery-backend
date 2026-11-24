using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Etapa 5: intervalo elegido en checkout (reserva temporal mientras el pago está pendiente).
    /// Solo agrega columnas nullable e índice; no modifica ni rellena órdenes existentes.
    /// </summary>
    public partial class IntervalosEntregaReservas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompromisoFinEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompromisoInicioEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ordenes_FechaEntrega_Estado",
                table: "Ordenes",
                columns: new[] { "FechaEntrega", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ordenes_FechaEntrega_Estado",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CompromisoFinEn",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CompromisoInicioEn",
                table: "Ordenes");
        }
    }
}
