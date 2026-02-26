using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stat.Migrations
{
    public partial class v01 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           
            migrationBuilder.CreateTable(
                name: "CategorieIndicateurs",
                columns: table => new
                {
                    IdCategIn = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IntituleCategIn = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorieIndicateurs", x => x.IdCategIn);
                });

            migrationBuilder.CreateTable(
                name: "DCs",
                columns: table => new
                {
                    CodeDC = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    LibelleDC = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCs", x => x.CodeDC);
                });

            migrationBuilder.CreateTable(
                name: "DIWs",
                columns: table => new
                {
                    CodeDIW = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    LibelleDIW = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CodeDRI = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DIWs", x => x.CodeDIW);
                });

            migrationBuilder.CreateTable(
                name: "DRIs",
                columns: table => new
                {
                    CodeDRI = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    LibelleDRI = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DRIs", x => x.CodeDRI);
                });

            migrationBuilder.CreateTable(
                name: "Indicateurs",
                columns: table => new
                {
                    IdIn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntituleIn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdCategIn = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indicateurs", x => x.IdIn);
                });

            migrationBuilder.CreateTable(
                name: "Situations",
                columns: table => new
                {
                    IDSituation = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DIW = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Month = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Situations", x => x.IDSituation);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    ID_User = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KeepLoggedIn = table.Column<bool>(type: "bit", nullable: false),
                    FirstNmUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastNmUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MailUser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TelUser = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CodeDIW = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastCnx = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateDeCreation = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Date_deb_Affect = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Date_Fin_Affect = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.ID_User);
                });

            migrationBuilder.CreateTable(
                name: "ValeurCibles",
                columns: table => new
                {
                    IdValeurCible = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mois = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    Valeur = table.Column<float>(type: "real", nullable: false),
                    ValeurCumulee = table.Column<float>(type: "real", nullable: false),
                    ValeurCibleMensuelle = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IdIn = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValeurCibles", x => x.IdValeurCible);
                });

            migrationBuilder.CreateTable(
                name: "ValeurRealisés",
                columns: table => new
                {
                    Idvalreal = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IdIn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DIW = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MoisIn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnneeIn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Valeur = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValeurRealisés", x => x.Idvalreal);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategorieIndicateurs");

            migrationBuilder.DropTable(
                name: "DCs");

            migrationBuilder.DropTable(
                name: "DIWs");

            migrationBuilder.DropTable(
                name: "DRIs");

            migrationBuilder.DropTable(
                name: "Indicateurs");

            migrationBuilder.DropTable(
                name: "Situations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ValeurCibles");

            migrationBuilder.DropTable(
                name: "ValeurRealisés");
          
        }

    }

}
