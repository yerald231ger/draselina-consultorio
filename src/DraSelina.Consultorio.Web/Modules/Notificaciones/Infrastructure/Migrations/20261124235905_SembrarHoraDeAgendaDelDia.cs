using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Notificaciones.Infrastructure.Migrations;

/// <inheritdoc />
public partial class _20261124235905_SembrarHoraDeAgendaDelDia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            schema: "notificaciones",
            table: "configuracion_de_envio",
            keyColumn: "id",
            keyValue: (short)1,
            column: "hora_agenda_del_dia",
            value: new TimeOnly(8, 0, 0));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            schema: "notificaciones",
            table: "configuracion_de_envio",
            keyColumn: "id",
            keyValue: (short)1,
            column: "hora_agenda_del_dia",
            value: new TimeOnly(14, 0, 0));
    }
}
