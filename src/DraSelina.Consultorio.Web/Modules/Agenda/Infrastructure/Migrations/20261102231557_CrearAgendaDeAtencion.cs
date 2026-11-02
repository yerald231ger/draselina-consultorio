using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearAgendaDeAtencion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agenda_base_tramo",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_de_la_semana = table.Column<short>(type: "smallint", nullable: false),
                    inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_base_tramo", x => x.id);
                    table.CheckConstraint("ck_agenda_base_tramo_dia", "dia_de_la_semana BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_agenda_base_tramo_rango", "fin > inicio");
                });

            migrationBuilder.CreateTable(
                name: "agenda_custom",
                schema: "agenda",
                columns: table => new
                {
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_custom", x => x.fecha);
                });

            migrationBuilder.CreateTable(
                name: "reglas_de_reserva",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    anticipacion_minima_dias = table.Column<short>(type: "smallint", nullable: false),
                    ventana_meses = table.Column<short>(type: "smallint", nullable: false),
                    umbral_cancelacion_tardia_min = table.Column<short>(type: "smallint", nullable: false),
                    tope_semanal_por_telefono = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_de_reserva", x => x.id);
                    table.CheckConstraint("ck_reglas_de_reserva_id", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "agenda_custom_tramo",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_custom_tramo", x => x.id);
                    table.CheckConstraint("ck_agenda_custom_tramo_rango", "fin > inicio");
                    table.ForeignKey(
                        name: "FK_agenda_custom_tramo_agenda_custom_fecha",
                        column: x => x.fecha,
                        principalSchema: "agenda",
                        principalTable: "agenda_custom",
                        principalColumn: "fecha",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "agenda",
                table: "agenda_base_tramo",
                columns: new[] { "id", "dia_de_la_semana", "fin", "inicio" },
                values: new object[,]
                {
                    { new Guid("01a19e61-3a10-7a01-8b10-6f2a3c4d5e01"), (short)1, new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) },
                    { new Guid("01a19e61-3a10-7a02-8b10-6f2a3c4d5e02"), (short)2, new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) },
                    { new Guid("01a19e61-3a10-7a03-8b10-6f2a3c4d5e03"), (short)3, new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) },
                    { new Guid("01a19e61-3a10-7a04-8b10-6f2a3c4d5e04"), (short)4, new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) },
                    { new Guid("01a19e61-3a10-7a05-8b10-6f2a3c4d5e05"), (short)5, new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) }
                });

            migrationBuilder.InsertData(
                schema: "agenda",
                table: "reglas_de_reserva",
                columns: new[] { "id", "anticipacion_minima_dias", "tope_semanal_por_telefono", "umbral_cancelacion_tardia_min", "ventana_meses" },
                values: new object[] { (short)1, (short)1, (short)3, (short)120, (short)2 });

            migrationBuilder.CreateIndex(
                name: "ux_agenda_base_tramo_dia_inicio",
                schema: "agenda",
                table: "agenda_base_tramo",
                columns: new[] { "dia_de_la_semana", "inicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_agenda_custom_tramo_fecha_inicio",
                schema: "agenda",
                table: "agenda_custom_tramo",
                columns: new[] { "fecha", "inicio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenda_base_tramo",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "agenda_custom_tramo",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "reglas_de_reserva",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "agenda_custom",
                schema: "agenda");
        }
    }
}
