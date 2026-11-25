using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations;

/// <inheritdoc />
public partial class _20261125013626_AgregarConsentimientoDelMotivoACita : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Las Citas que ya existen quedan con el motivo incluido: su casilla única ya nombraba los datos de salud
        // (Gerardo, 2026-11-24). El valor por omisión solo sirve para llenarlas: se quita enseguida, para que ninguna
        // inserción que omita la columna registre un consentimiento expreso que no se dio.
        migrationBuilder.AddColumn<bool>(
            name: "consentimiento_incluye_motivo",
            schema: "agenda",
            table: "cita",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AlterColumn<bool>(
            name: "consentimiento_incluye_motivo",
            schema: "agenda",
            table: "cita",
            type: "boolean",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "consentimiento_incluye_motivo",
            schema: "agenda",
            table: "cita");
    }
}
