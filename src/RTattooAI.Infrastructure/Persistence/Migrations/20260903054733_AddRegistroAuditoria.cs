using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTattooAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistroAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistroAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UsuarioRol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Recurso = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DireccionIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistroAuditoria_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_Fecha",
                table: "RegistroAuditoria",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_UsuarioId",
                table: "RegistroAuditoria",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistroAuditoria");
        }
    }
}
