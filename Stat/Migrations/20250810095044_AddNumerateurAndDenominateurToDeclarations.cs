using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stat.Migrations
{
    public partial class AddNumerateurAndDenominateurToDeclarations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Denominateur",
                table: "Declarations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Numerateur",
                table: "Declarations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Denominateur",
                table: "Declarations");

            migrationBuilder.DropColumn(
                name: "Numerateur",
                table: "Declarations");
        }
    }
}
