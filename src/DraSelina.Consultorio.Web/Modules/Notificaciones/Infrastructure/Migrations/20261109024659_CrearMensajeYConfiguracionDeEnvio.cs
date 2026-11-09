using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Notificaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearMensajeYConfiguracionDeEnvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notificaciones");

            migrationBuilder.CreateTable(
                name: "configuracion_de_envio",
                schema: "notificaciones",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    hora_recordatorio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_agenda_del_dia = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    correo_doctora = table.Column<string>(type: "text", nullable: false),
                    correo_administrador = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_de_envio", x => x.id);
                    table.CheckConstraint("ck_configuracion_de_envio_id", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "mensaje",
                schema: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cita_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    canal = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    registrado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensaje", x => x.id);
                    table.CheckConstraint("ck_mensaje_canal", "canal IN ('correo')");
                    table.CheckConstraint("ck_mensaje_estado", "estado IN ('Enviado', 'Fallido', 'Omitido')");
                    table.CheckConstraint("ck_mensaje_tipo", "tipo IN ('Confirmacion', 'Recordatorio', 'Reprogramada', 'Cancelada', 'CanceladaPorConsultorio', 'AlertaTardia', 'AgendaDelDia')");
                });

            migrationBuilder.InsertData(
                schema: "notificaciones",
                table: "configuracion_de_envio",
                columns: new[] { "id", "correo_administrador", "correo_doctora", "hora_agenda_del_dia", "hora_recordatorio" },
                values: new object[] { (short)1, "administrador@consultorio.local", "doctora@consultorio.local", new TimeOnly(14, 0, 0), new TimeOnly(9, 0, 0) });

            migrationBuilder.CreateIndex(
                name: "ux_mensaje_evento_id_tipo",
                schema: "notificaciones",
                table: "mensaje",
                columns: new[] { "evento_id", "tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_de_envio",
                schema: "notificaciones");

            migrationBuilder.DropTable(
                name: "mensaje",
                schema: "notificaciones");
        }
    }
}
