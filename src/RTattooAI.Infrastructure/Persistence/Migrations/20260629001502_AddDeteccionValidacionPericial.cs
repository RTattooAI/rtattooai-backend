using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTattooAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeteccionValidacionPericial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoValidacion",
                table: "Detecciones",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaValidacion",
                table: "Detecciones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesPerito",
                table: "Detecciones",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidadoPorId",
                table: "Detecciones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Detecciones_ValidadoPorId",
                table: "Detecciones",
                column: "ValidadoPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Detecciones_Usuarios_ValidadoPorId",
                table: "Detecciones",
                column: "ValidadoPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Detecciones_Usuarios_ValidadoPorId",
                table: "Detecciones");

            migrationBuilder.DropIndex(
                name: "IX_Detecciones_ValidadoPorId",
                table: "Detecciones");

            migrationBuilder.DropColumn(
                name: "EstadoValidacion",
                table: "Detecciones");

            migrationBuilder.DropColumn(
                name: "FechaValidacion",
                table: "Detecciones");

            migrationBuilder.DropColumn(
                name: "ObservacionesPerito",
                table: "Detecciones");

            migrationBuilder.DropColumn(
                name: "ValidadoPorId",
                table: "Detecciones");
        }
    }
}
