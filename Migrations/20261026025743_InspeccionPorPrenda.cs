using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlquilerVestimentasLucy.Migrations
{
    /// <inheritdoc />
    public partial class InspeccionPorPrenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClienteInformado",
                table: "Devoluciones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MedioReembolso",
                table: "Devoluciones",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoReembolso",
                table: "Devoluciones",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoPendiente",
                table: "Devoluciones",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DetallesDevolucion",
                columns: table => new
                {
                    IdDetalleDevolucion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDevolucion = table.Column<int>(type: "int", nullable: false),
                    IdDetalleAlquiler = table.Column<int>(type: "int", nullable: false),
                    EstadoFisicoRecepcion = table.Column<int>(type: "int", nullable: false),
                    CargoDanio = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesDevolucion", x => x.IdDetalleDevolucion);
                    table.ForeignKey(
                        name: "FK_DetallesDevolucion_DetallesAlquiler_IdDetalleAlquiler",
                        column: x => x.IdDetalleAlquiler,
                        principalTable: "DetallesAlquiler",
                        principalColumn: "IdDetalle",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallesDevolucion_Devoluciones_IdDevolucion",
                        column: x => x.IdDevolucion,
                        principalTable: "Devoluciones",
                        principalColumn: "IdDevolucion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDevolucion_IdDetalleAlquiler",
                table: "DetallesDevolucion",
                column: "IdDetalleAlquiler");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDevolucion_IdDevolucion_IdDetalleAlquiler",
                table: "DetallesDevolucion",
                columns: new[] { "IdDevolucion", "IdDetalleAlquiler" },
                unique: true);

            // Compatibilidad con la devolución histórica creada antes de la inspección
            // por prenda. Se replica la condición general en cada línea del contrato.
            migrationBuilder.Sql("""
                INSERT INTO DetallesDevolucion
                    (IdDevolucion, IdDetalleAlquiler, EstadoFisicoRecepcion, CargoDanio, Observaciones)
                SELECT d.IdDevolucion, da.IdDetalle, d.EstadoFisicoRecepcion, 0, d.Observaciones
                FROM Devoluciones d
                INNER JOIN DetallesAlquiler da ON da.IdAlquiler = d.IdAlquiler
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM DetallesDevolucion dd
                    WHERE dd.IdDevolucion = d.IdDevolucion
                );

                UPDATE d
                SET d.MedioReembolso = 'Transferencia',
                    d.MontoReembolso = a.TotalGarantia,
                    d.SaldoPendiente = 0,
                    d.ClienteInformado = 1
                FROM Devoluciones d
                INNER JOIN Alquileres a ON a.IdAlquiler = d.IdAlquiler
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM Penalidades p
                    WHERE p.IdDevolucion = d.IdDevolucion
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesDevolucion");

            migrationBuilder.DropColumn(
                name: "ClienteInformado",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "MedioReembolso",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "MontoReembolso",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "SaldoPendiente",
                table: "Devoluciones");
        }
    }
}
