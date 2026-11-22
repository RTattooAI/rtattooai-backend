using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTattooAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultaImagenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se crea nullable a proposito: primero se rellena a partir de los datos
            // existentes y solo despues se exige. Asi ninguna consulta previa se pierde.
            migrationBuilder.AddColumn<Guid>(
                name: "ConsultaImagenId",
                table: "Detecciones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConsultaImagenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsultaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ImagenUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ImagenMiniaturaUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImagenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TiempoProcMs = table.Column<int>(type: "int", nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultaImagenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultaImagenes_Consultas_ConsultaId",
                        column: x => x.ConsultaId,
                        principalTable: "Consultas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Cada consulta existente pasa a tener exactamente una imagen (la que ya tenia),
            // conservando su ruta, su hash, sus dimensiones y su estado.
            migrationBuilder.Sql(@"
                INSERT INTO ConsultaImagenes
                    (Id, ConsultaId, Orden, ImagenUrl, ImagenMiniaturaUrl, ImagenHash,
                     Width, Height, Estado, ModelVersion, TiempoProcMs, Origen)
                SELECT NEWID(), c.Id, 0, c.ImagenOrigUrl, c.ImagenMiniaturaUrl, ISNULL(c.ImagenOrigHash, ''),
                       c.ImageWidth, c.ImageHeight, c.Estado, c.ModelVersion, c.TiempoProcMs, 'archivo'
                FROM Consultas c;");

            migrationBuilder.Sql(@"
                UPDATE d
                SET d.ConsultaImagenId = i.Id
                FROM Detecciones d
                INNER JOIN ConsultaImagenes i ON i.ConsultaId = d.ConsultaId AND i.Orden = 0;");

            // Detecciones huerfanas (sin consulta) no deberian existir; si las hubiera,
            // se eliminan aqui en vez de bloquear la migracion con la restriccion NOT NULL.
            migrationBuilder.Sql("DELETE FROM Detecciones WHERE ConsultaImagenId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConsultaImagenId",
                table: "Detecciones",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Detecciones_ConsultaImagenId",
                table: "Detecciones",
                column: "ConsultaImagenId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultaImagenes_ConsultaId_Orden",
                table: "ConsultaImagenes",
                columns: new[] { "ConsultaId", "Orden" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Detecciones_ConsultaImagenes_ConsultaImagenId",
                table: "Detecciones",
                column: "ConsultaImagenId",
                principalTable: "ConsultaImagenes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Detecciones_ConsultaImagenes_ConsultaImagenId",
                table: "Detecciones");

            migrationBuilder.DropTable(
                name: "ConsultaImagenes");

            migrationBuilder.DropIndex(
                name: "IX_Detecciones_ConsultaImagenId",
                table: "Detecciones");

            migrationBuilder.DropColumn(
                name: "ConsultaImagenId",
                table: "Detecciones");
        }
    }
}
