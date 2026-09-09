using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTattooAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionesModeloYMetadatosCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "TatuajesCatalogo",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "TatuajesCatalogo",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UbicacionCorporal",
                table: "TatuajesCatalogo",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VersionesModelo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identificador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Detector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Embedder = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Cabeza = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetricaMap50 = table.Column<double>(type: "float", nullable: true),
                    MetricaTop1 = table.Column<double>(type: "float", nullable: true),
                    MetricaTop5 = table.Column<double>(type: "float", nullable: true),
                    Umbral = table.Column<double>(type: "float", nullable: true),
                    Margen = table.Column<double>(type: "float", nullable: true),
                    Fuente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EsActiva = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesModelo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VersionesModelo_Identificador",
                table: "VersionesModelo",
                column: "Identificador",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VersionesModelo");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "TatuajesCatalogo");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "TatuajesCatalogo");

            migrationBuilder.DropColumn(
                name: "UbicacionCorporal",
                table: "TatuajesCatalogo");
        }
    }
}
