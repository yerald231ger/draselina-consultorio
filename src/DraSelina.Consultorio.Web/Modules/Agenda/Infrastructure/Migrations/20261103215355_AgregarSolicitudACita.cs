using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSolicitudACita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "solicitud_id",
                schema: "agenda",
                table: "cita",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_cita_solicitud_id",
                schema: "agenda",
                table: "cita",
                column: "solicitud_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cita_solicitud_id",
                schema: "agenda",
                table: "cita");

            migrationBuilder.DropColumn(
                name: "solicitud_id",
                schema: "agenda",
                table: "cita");
        }
    }
}
