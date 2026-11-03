using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitacora",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_tipo = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<string>(type: "text", nullable: true),
                    entidad = table.Column<string>(type: "text", nullable: false),
                    entidad_id = table.Column<string>(type: "text", nullable: false),
                    accion = table.Column<string>(type: "text", nullable: false),
                    detalle = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bitacora", x => x.id);
                    table.CheckConstraint("ck_bitacora_accion", "accion IN ('Agendada', 'Reprogramada', 'Cancelada', 'NoSePresento', 'Registrada', 'Cambiada', 'Borrada')");
                    table.CheckConstraint("ck_bitacora_actor_tipo", "actor_tipo IN ('Paciente', 'Administrador', 'Sistema')");
                    table.CheckConstraint("ck_bitacora_entidad", "entidad IN ('Cita', 'AgendaBase', 'AgendaCustom', 'TipoDeVisita', 'ReglasDeReserva')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_bitacora_entidad_entidad_id_ocurrido_en",
                schema: "agenda",
                table: "bitacora",
                columns: new[] { "entidad", "entidad_id", "ocurrido_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitacora",
                schema: "agenda");
        }
    }
}
