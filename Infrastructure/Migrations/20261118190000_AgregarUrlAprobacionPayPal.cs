using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class AgregarUrlAprobacionPayPal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "UrlAprobacion", table: "PagosOrden", type: "character varying(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReferenciaCaptura", table: "PagosOrden", type: "character varying(120)", maxLength: 120, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "UrlAprobacion", table: "PagosOrden");
        migrationBuilder.DropColumn(name: "ReferenciaCaptura", table: "PagosOrden");
    }
}
