using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTattooAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogBandaNullableAndConsultaPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BandaId",
                table: "TatuajesCatalogo",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsultaOrigenId",
                table: "TatuajesCatalogo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonaDocumento",
                table: "Consultas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonaNombre",
                table: "Consultas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TatuajesCatalogo_ConsultaOrigenId",
                table: "TatuajesCatalogo",
                column: "ConsultaOrigenId");

            migrationBuilder.AddForeignKey(
                name: "FK_TatuajesCatalogo_Consultas_ConsultaOrigenId",
                table: "TatuajesCatalogo",
                column: "ConsultaOrigenId",
                principalTable: "Consultas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TatuajesCatalogo_Consultas_ConsultaOrigenId",
                table: "TatuajesCatalogo");

            migrationBuilder.DropIndex(
                name: "IX_TatuajesCatalogo_ConsultaOrigenId",
                table: "TatuajesCatalogo");

            migrationBuilder.DropColumn(
                name: "ConsultaOrigenId",
                table: "TatuajesCatalogo");

            migrationBuilder.DropColumn(
                name: "PersonaDocumento",
                table: "Consultas");

            migrationBuilder.DropColumn(
                name: "PersonaNombre",
                table: "Consultas");

            migrationBuilder.AlterColumn<Guid>(
                name: "BandaId",
                table: "TatuajesCatalogo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
