using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlanificacionTemporalRutas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DistanciaEstimadaKm",
                table: "Rutas",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EstimacionesActualizadasEn",
                table: "Rutas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeloEstimacion",
                table: "Rutas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OrigenLatitud",
                table: "Rutas",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OrigenLongitud",
                table: "Rutas",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SalidaProgramadaEn",
                table: "Rutas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompromisoFinEn",
                table: "Pedidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompromisoInicioEn",
                table: "Pedidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TiempoServicioMinutos",
                table: "Pedidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizacionEstimadaEn",
                table: "ParadasRuta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InicioAtencionEstimadoEn",
                table: "ParadasRuta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LlegadaEstimadaEn",
                table: "ParadasRuta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TrasladoEstimadoMinutos",
                table: "ParadasRuta",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanciaEstimadaKm",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "EstimacionesActualizadasEn",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "ModeloEstimacion",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "OrigenLatitud",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "OrigenLongitud",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "SalidaProgramadaEn",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "CompromisoFinEn",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "CompromisoInicioEn",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "TiempoServicioMinutos",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "FinalizacionEstimadaEn",
                table: "ParadasRuta");

            migrationBuilder.DropColumn(
                name: "InicioAtencionEstimadoEn",
                table: "ParadasRuta");

            migrationBuilder.DropColumn(
                name: "LlegadaEstimadaEn",
                table: "ParadasRuta");

            migrationBuilder.DropColumn(
                name: "TrasladoEstimadoMinutos",
                table: "ParadasRuta");
        }
    }
}
