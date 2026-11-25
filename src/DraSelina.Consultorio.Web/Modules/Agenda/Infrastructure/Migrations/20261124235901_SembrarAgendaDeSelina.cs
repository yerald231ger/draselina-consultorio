using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations;

/// <inheritdoc />
public partial class _20261124235901_SembrarAgendaDeSelina : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a01-8b10-6f2a3c4d5e01"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(19, 0, 0), new TimeOnly(15, 0, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a02-8b10-6f2a3c4d5e02"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(19, 0, 0), new TimeOnly(15, 0, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a03-8b10-6f2a3c4d5e03"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(19, 0, 0), new TimeOnly(15, 0, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a04-8b10-6f2a3c4d5e04"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(19, 0, 0), new TimeOnly(15, 0, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a05-8b10-6f2a3c4d5e05"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(19, 0, 0), new TimeOnly(15, 0, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "tipo_de_visita",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-08c5-7c92-a7e1-3b97e6858869"),
            column: "duracion_min",
            value: 60);

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "tipo_de_visita",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-08d2-771e-8fa6-a54172742655"),
            column: "duracion_min",
            value: 60);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a01-8b10-6f2a3c4d5e01"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a02-8b10-6f2a3c4d5e02"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a03-8b10-6f2a3c4d5e03"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a04-8b10-6f2a3c4d5e04"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "agenda_base_tramo",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-3a10-7a05-8b10-6f2a3c4d5e05"),
            columns: new[] { "fin", "inicio" },
            values: new object[] { new TimeOnly(18, 0, 0), new TimeOnly(15, 30, 0) });

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "tipo_de_visita",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-08c5-7c92-a7e1-3b97e6858869"),
            column: "duracion_min",
            value: 30);

        migrationBuilder.UpdateData(
            schema: "agenda",
            table: "tipo_de_visita",
            keyColumn: "id",
            keyValue: new Guid("01a19e61-08d2-771e-8fa6-a54172742655"),
            column: "duracion_min",
            value: 30);
    }
}
