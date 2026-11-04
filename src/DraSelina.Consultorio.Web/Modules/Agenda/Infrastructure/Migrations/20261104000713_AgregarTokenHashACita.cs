using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTokenHashACita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sin valor por defecto: el hash sale de la clave del host, que la migración no tiene (AD-19). Hasta esta
            // migración ninguna base con Citas salió de local, donde cada arranque parte de una base vacía.
            migrationBuilder.AddColumn<byte[]>(
                name: "token_hash",
                schema: "agenda",
                table: "cita",
                type: "bytea",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ux_cita_token_hash",
                schema: "agenda",
                table: "cita",
                column: "token_hash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_cita_token_hash",
                schema: "agenda",
                table: "cita",
                sql: "octet_length(token_hash) = 32");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cita_token_hash",
                schema: "agenda",
                table: "cita");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cita_token_hash",
                schema: "agenda",
                table: "cita");

            migrationBuilder.DropColumn(
                name: "token_hash",
                schema: "agenda",
                table: "cita");
        }
    }
}
