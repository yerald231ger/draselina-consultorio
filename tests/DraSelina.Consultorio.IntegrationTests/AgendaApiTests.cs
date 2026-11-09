using DraSelina.Consultorio.Agenda.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="IAgendaApi.AgendaDelDiaAsync"/> contra PostgreSQL real (entrada 4.6): los Tramos vigentes de la fecha
/// (base o custom, INV-V1) y sus Citas Agendadas de cualquier origen, en orden. Con la agenda base sembrada (lunes a
/// viernes, 15:30 a 18:00) y el reloj de <see cref="ReservaEnLineaTests"/>.
/// </summary>
public sealed class AgendaApiTests(Arnes arnes)
{
    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Los_Tramos_salen_de_la_base_o_de_la_custom_de_la_fecha()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var jueves = new DateOnly(2026, 11, 5);
        await escenario.InsertarCustomAsync(jueves, "cirugía");
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(12, 0)));

        Assert.True((await AgendaDelDiaAsync(escenario, Martes)).TieneTramos);
        Assert.False((await AgendaDelDiaAsync(escenario, jueves)).TieneTramos);
        Assert.True((await AgendaDelDiaAsync(escenario, Sabado)).TieneTramos);
        Assert.False((await AgendaDelDiaAsync(escenario, Sabado.AddDays(1))).TieneTramos);
        Assert.Empty((await AgendaDelDiaAsync(escenario, Martes)).Citas);
    }

    [Fact]
    public async Task Trae_las_Agendadas_del_dia_local_de_cualquier_origen_en_orden_sin_las_canceladas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var resultado = await escenario.AgendarAsync(Comando(Martes, new TimeOnly(16, 0)));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var ana = await escenario.PacienteDeAnaAsync();

        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(17, 0), new TimeOnly(17, 30), ana);
        // A las 23:30 local ya es miércoles en UTC: cuenta el día local.
        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(23, 30), new TimeOnly(23, 50), ana);
        await escenario.InsertarCitaDelConsultorioAsync(Martes.AddDays(-1), new TimeOnly(23, 30), new TimeOnly(23, 50), ana);
        await escenario.InsertarCitaDelConsultorioAsync(Martes.AddDays(1), new TimeOnly(0, 0), new TimeOnly(0, 30), ana);
        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(15, 30), new TimeOnly(16, 0), ana);
        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now() "
            + $"where inicio = '{Agenda.Domain.HoraLocal.AUtc(Martes, new TimeOnly(15, 30)):O}'");

        var agenda = await AgendaDelDiaAsync(escenario, Martes);

        Assert.True(agenda.TieneTramos);
        Assert.Equal(
            [
                (new TimeOnly(16, 0), ana, (string?)"Dolor de rodilla derecha"),
                (new TimeOnly(17, 0), ana, null),
                (new TimeOnly(23, 30), ana, null),
            ],
            agenda.Citas.Select(c => (c.Hora, c.PatientId.Valor, c.MotivoDeConsulta)));
        Assert.DoesNotContain("rodilla", agenda.ToString(), StringComparison.Ordinal);
    }

    private static async Task<AgendaDelDia> AgendaDelDiaAsync(Escenario escenario, DateOnly fecha)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IAgendaApi>().AgendaDelDiaAsync(fecha, Cancelacion);
    }
}
