using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Notificaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearRecordatorioProgramado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recordatorio_programado",
                schema: "notificaciones",
                columns: table => new
                {
                    cita_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    programado_para = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recordatorio_programado", x => x.cita_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recordatorio_programado_programado_para",
                schema: "notificaciones",
                table: "recordatorio_programado",
                column: "programado_para");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recordatorio_programado",
                schema: "notificaciones");
        }
    }
}
