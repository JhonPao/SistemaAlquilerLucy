using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlquilerVestimentasLucy.Migrations
{
    /// <inheritdoc />
    public partial class CamposEntregaAlquiler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MedioPago",
                table: "Alquileres",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Transferencia");

            migrationBuilder.AddColumn<string>(
                name: "Observaciones",
                table: "Alquileres",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaPago",
                table: "Alquileres",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MedioPago",
                table: "Alquileres");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                table: "Alquileres");

            migrationBuilder.DropColumn(
                name: "ReferenciaPago",
                table: "Alquileres");
        }
    }
}
