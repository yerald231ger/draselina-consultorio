using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Notificaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFechaAMensaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha",
                schema: "notificaciones",
                table: "mensaje",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_mensaje_tipo_fecha",
                schema: "notificaciones",
                table: "mensaje",
                columns: new[] { "tipo", "fecha" },
                unique: true,
                filter: "fecha IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_mensaje_tipo_fecha",
                schema: "notificaciones",
                table: "mensaje");

            migrationBuilder.DropColumn(
                name: "fecha",
                schema: "notificaciones",
                table: "mensaje");
        }
    }
}
