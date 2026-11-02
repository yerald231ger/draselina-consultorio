using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Pacientes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearPacientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pacientes");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:pacientes.es_ci_ai", "es-u-ks-level1,es-u-ks-level1,icu,False");

            migrationBuilder.CreateTable(
                name: "paciente",
                schema: "pacientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefono = table.Column<string>(type: "text", nullable: false),
                    correo = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nombre_busqueda = table.Column<string>(type: "text", nullable: false),
                    nombre_normalizado = table.Column<string>(type: "text", nullable: false),
                    nombre_completo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paciente", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_paciente_telefono",
                schema: "pacientes",
                table: "paciente",
                column: "telefono");

            // La identidad del Paciente (AD-18, RN-26): nombre_normalizado bajo la collation ICU no determinista, que
            // no distingue mayúsculas ni acentos, y el índice único que la usa. Con SQL explícito porque Npgsql
            // escribe la collation con esquema como un solo identificador ("pacientes.es_ci_ai").
            migrationBuilder.Sql(
                "ALTER TABLE pacientes.paciente ALTER COLUMN nombre_normalizado TYPE text COLLATE pacientes.es_ci_ai;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_paciente_identidad ON pacientes.paciente (telefono, nombre_normalizado);");

            // Para la lista de pacientes: minúsculas y sin acentos, collation determinista.
            migrationBuilder.Sql(
                "CREATE INDEX ix_paciente_nombre_busqueda ON pacientes.paciente (nombre_busqueda);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paciente",
                schema: "pacientes");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:CollationDefinition:pacientes.es_ci_ai", "es-u-ks-level1,es-u-ks-level1,icu,False");
        }
    }
}
