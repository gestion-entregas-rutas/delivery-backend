using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IncorporarEcommerce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_RepartidorId",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "NombreUsuario",
                table: "Usuarios",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "NombreNormalizado",
                table: "Usuarios",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Cedula",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Correo",
                table: "Usuarios",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreoNormalizado",
                table: "Usuarios",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreCompleto",
                table: "Usuarios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Usuarios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Carritos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenInvitado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carritos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carritos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OrdenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultadoJson = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ordenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroOrden = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EstadoPago = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TipoEntrega = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CodigoRetiro = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PedidoLogisticoId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RetiroCedula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RetiroNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RetiroTelefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RetiroFecha = table.Column<DateOnly>(type: "date", nullable: true),
                    RetiroFranja = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DestinatarioNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DestinatarioTelefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DireccionTexto = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: true),
                    HoraEntrega = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ObservacionesEntrega = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ordenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ordenes_Pedidos_PedidoLogisticoId",
                        column: x => x.PedidoLogisticoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ordenes_Usuarios_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subcategorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subcategorias_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagosOrden",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metodo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosOrden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosOrden_Ordenes_OrdenId",
                        column: x => x.OrdenId,
                        principalTable: "Ordenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcategoriaId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubcategoriaTexto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Marca = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DescripcionCorta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrecioBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrecioOferta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    ImagenPrincipalUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CalificacionPromedio = table.Column<double>(type: "double precision", nullable: false),
                    TotalResenas = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Productos_Subcategorias_SubcategoriaId",
                        column: x => x.SubcategoriaId,
                        principalTable: "Subcategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CarritoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CarritoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarritoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarritoItems_Carritos_CarritoId",
                        column: x => x.CarritoId,
                        principalTable: "Carritos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarritoItems_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetalleOrdenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreProducto = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleOrdenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetalleOrdenes_Ordenes_OrdenId",
                        column: x => x.OrdenId,
                        principalTable: "Ordenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetalleOrdenes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductoCaracteristicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoCaracteristicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoCaracteristicas_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoFichasTecnicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Clave = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Valor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoFichasTecnicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoFichasTecnicas_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoMedias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoMedias_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resenas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Calificacion = table.Column<int>(type: "integer", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resenas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resenas_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Resenas_Usuarios_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CorreoNormalizado",
                table: "Usuarios",
                column: "CorreoNormalizado",
                unique: true,
                filter: "\"CorreoNormalizado\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RepartidorId",
                table: "Usuarios",
                column: "RepartidorId",
                unique: true,
                filter: "\"RepartidorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CarritoItems_CarritoId_ProductoId",
                table: "CarritoItems",
                columns: new[] { "CarritoId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarritoItems_ProductoId",
                table: "CarritoItems",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Carritos_TokenInvitado",
                table: "Carritos",
                column: "TokenInvitado",
                unique: true,
                filter: "\"TokenInvitado\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Carritos_UsuarioId",
                table: "Carritos",
                column: "UsuarioId",
                unique: true,
                filter: "\"UsuarioId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Activa",
                table: "Categorias",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Numero",
                table: "Categorias",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetalleOrdenes_OrdenId",
                table: "DetalleOrdenes",
                column: "OrdenId");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleOrdenes_ProductoId",
                table: "DetalleOrdenes",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Key_Operacion",
                table: "IdempotencyRecords",
                columns: new[] { "Key", "Operacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_OrdenId",
                table: "IdempotencyRecords",
                column: "OrdenId");

            migrationBuilder.CreateIndex(
                name: "IX_Ordenes_ClienteId_CreadaEn",
                table: "Ordenes",
                columns: new[] { "ClienteId", "CreadaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Ordenes_IdempotencyKey",
                table: "Ordenes",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Ordenes_NumeroOrden",
                table: "Ordenes",
                column: "NumeroOrden",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ordenes_PedidoLogisticoId",
                table: "Ordenes",
                column: "PedidoLogisticoId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosOrden_OrdenId",
                table: "PagosOrden",
                column: "OrdenId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoCaracteristicas_ProductoId_Orden",
                table: "ProductoCaracteristicas",
                columns: new[] { "ProductoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoFichasTecnicas_ProductoId_Orden",
                table: "ProductoFichasTecnicas",
                columns: new[] { "ProductoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoMedias_ProductoId_Orden",
                table: "ProductoMedias",
                columns: new[] { "ProductoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Activo",
                table: "Productos",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_PrecioBase",
                table: "Productos",
                column: "PrecioBase");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_SubcategoriaId",
                table: "Productos",
                column: "SubcategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Resenas_ClienteId",
                table: "Resenas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Resenas_ProductoId_ClienteId",
                table: "Resenas",
                columns: new[] { "ProductoId", "ClienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subcategorias_CategoriaId_Nombre",
                table: "Subcategorias",
                columns: new[] { "CategoriaId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarritoItems");

            migrationBuilder.DropTable(
                name: "DetalleOrdenes");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "PagosOrden");

            migrationBuilder.DropTable(
                name: "ProductoCaracteristicas");

            migrationBuilder.DropTable(
                name: "ProductoFichasTecnicas");

            migrationBuilder.DropTable(
                name: "ProductoMedias");

            migrationBuilder.DropTable(
                name: "Resenas");

            migrationBuilder.DropTable(
                name: "Carritos");

            migrationBuilder.DropTable(
                name: "Ordenes");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Subcategorias");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CorreoNormalizado",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_RepartidorId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Cedula",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Correo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CorreoNormalizado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NombreCompleto",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "NombreUsuario",
                table: "Usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "NombreNormalizado",
                table: "Usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RepartidorId",
                table: "Usuarios",
                column: "RepartidorId",
                unique: true);
        }
    }
}
