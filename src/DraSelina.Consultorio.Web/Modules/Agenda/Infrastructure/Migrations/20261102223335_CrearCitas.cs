using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearCitas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipo_de_visita",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    duracion_min = table.Column<int>(type: "integer", nullable: false),
                    reservable_en_linea = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipo_de_visita", x => x.id);
                    table.CheckConstraint("ck_tipo_de_visita_duracion", "duracion_min > 0");
                });

            migrationBuilder.CreateTable(
                name: "cita",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_de_visita_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    motivo_de_cancelacion = table.Column<string>(type: "text", nullable: true),
                    cancelada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    origen = table.Column<string>(type: "text", nullable: false),
                    nota = table.Column<string>(type: "text", nullable: true),
                    motivo_de_consulta = table.Column<string>(type: "text", nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consentimiento_canal = table.Column<string>(type: "text", nullable: false),
                    consentimiento_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consentimiento_version_aviso = table.Column<string>(type: "text", nullable: false),
                    fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cita", x => x.id);
                    table.CheckConstraint("ck_cita_cancelacion", "(estado = 'Cancelada') = (motivo_de_cancelacion IS NOT NULL) AND (estado = 'Cancelada') = (cancelada_en IS NOT NULL)");
                    table.CheckConstraint("ck_cita_consentimiento_canal", "consentimiento_canal IN ('en_linea', 'externo')");
                    table.CheckConstraint("ck_cita_estado", "estado IN ('Agendada', 'NoSePresento', 'Cancelada')");
                    table.CheckConstraint("ck_cita_motivo_de_cancelacion", "motivo_de_cancelacion IS NULL OR motivo_de_cancelacion IN ('paciente', 'tarde', 'consultorio')");
                    table.CheckConstraint("ck_cita_origen", "origen IN ('en_linea', 'consultorio')");
                    table.CheckConstraint("ck_cita_rango", "fin > inicio");
                    table.ForeignKey(
                        name: "FK_cita_tipo_de_visita_tipo_de_visita_id",
                        column: x => x.tipo_de_visita_id,
                        principalSchema: "agenda",
                        principalTable: "tipo_de_visita",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "agenda",
                table: "tipo_de_visita",
                columns: new[] { "id", "activo", "descripcion", "duracion_min", "nombre", "orden", "reservable_en_linea" },
                values: new object[,]
                {
                    { new Guid("01a19e61-08c5-7c92-a7e1-3b97e6858869"), true, null, 30, "Primera consulta", 1, true },
                    { new Guid("01a19e61-08d2-771e-8fa6-a54172742655"), true, null, 30, "Consulta de seguimiento", 2, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_cita_tipo_de_visita_id",
                schema: "agenda",
                table: "cita",
                column: "tipo_de_visita_id");

            // Sin doble reserva (AD-8, RN-1): dos Citas no canceladas nunca se traslapan en [inicio, fin). Un solo
            // rango con GiST no necesita btree_gist: esa extensión hace falta al combinar el rango con una igualdad
            // sobre una columna escalar (modelo de datos, Observaciones).
            migrationBuilder.Sql(
                "ALTER TABLE agenda.cita ADD CONSTRAINT ex_cita_sin_traslape "
                + "EXCLUDE USING gist (tstzrange(inicio, fin, '[)') WITH &&) WHERE (estado <> 'Cancelada');");

            // Para la agenda del día y la disponibilidad; para el historial y el tope semanal.
            migrationBuilder.Sql("CREATE INDEX ix_cita_inicio ON agenda.cita (inicio);");
            migrationBuilder.Sql("CREATE INDEX ix_cita_paciente_id_inicio ON agenda.cita (paciente_id, inicio);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cita",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "tipo_de_visita",
                schema: "agenda");
        }
    }
}
