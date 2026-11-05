using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlquilerVestimentasLucy.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConcurrenciaPrenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Prendas",
                type: "rowversion",
                rowVersion: true,
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Prendas");
        }
    }
}
