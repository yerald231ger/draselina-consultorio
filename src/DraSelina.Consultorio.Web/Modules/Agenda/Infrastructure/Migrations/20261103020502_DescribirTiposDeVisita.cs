using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DescribirTiposDeVisita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "agenda",
                table: "tipo_de_visita",
                keyColumn: "id",
                keyValue: new Guid("01a19e61-08c5-7c92-a7e1-3b97e6858869"),
                column: "descripcion",
                value: "Si es tu primera vez con la doctora.");

            migrationBuilder.UpdateData(
                schema: "agenda",
                table: "tipo_de_visita",
                keyColumn: "id",
                keyValue: new Guid("01a19e61-08d2-771e-8fa6-a54172742655"),
                column: "descripcion",
                value: "Si ya te atendió y vienes a revisión.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "agenda",
                table: "tipo_de_visita",
                keyColumn: "id",
                keyValue: new Guid("01a19e61-08c5-7c92-a7e1-3b97e6858869"),
                column: "descripcion",
                value: null);

            migrationBuilder.UpdateData(
                schema: "agenda",
                table: "tipo_de_visita",
                keyColumn: "id",
                keyValue: new Guid("01a19e61-08d2-771e-8fa6-a54172742655"),
                column: "descripcion",
                value: null);
        }
    }
}
