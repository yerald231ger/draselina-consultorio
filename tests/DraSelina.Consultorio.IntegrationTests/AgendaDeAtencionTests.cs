using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La agenda de atención y las reglas de reserva contra PostgreSQL real: lo que siembra la migración (AD-25) y cómo
/// <see cref="IAgendaAtencion"/> arma y guarda el agregado desde sus tres tablas. Cada prueba usa su propia base
/// migrada y el reloj de <see cref="ReservaEnLineaTests.Ahora"/> (lunes 2 de noviembre de 2026, 12:00 local).
/// </summary>
public sealed class AgendaDeAtencionTests(Arnes arnes)
{
    private static readonly TimeSpan Media = TimeSpan.FromMinutes(30);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static Tramo T(int hi, int mi, int hf, int mf) => Tramo.Crear(new TimeOnly(hi, mi), new TimeOnly(hf, mf));

    [Fact]
    public async Task La_agenda_sembrada_atiende_de_lunes_a_viernes_de_1530_a_1800_y_no_los_fines_de_semana()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);

        var agenda = await ObtenerAsync(escenario, Hoy(escenario));

        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            Assert.Equal([T(15, 30, 18, 0)], agenda.TramosBase(dia));
        }

        Assert.Empty(agenda.TramosBase(DayOfWeek.Saturday));
        Assert.Empty(agenda.TramosBase(DayOfWeek.Sunday));
        Assert.Empty(agenda.FechasConCustom());

        var martes = agenda.TramosVigentes(new DateOnly(2026, 11, 3));
        Assert.Equal([T(15, 30, 18, 0)], martes.Tramos);
        Assert.Equal(OrigenDeTramos.Base, martes.Origen);
        Assert.Empty(agenda.TramosVigentes(new DateOnly(2026, 11, 7)).Tramos);
    }

    [Fact]
    public async Task La_base_guarda_el_dia_de_la_semana_en_numeracion_ISO()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);

        var dias = await escenario.FilasAsync(
            "select dia_de_la_semana, to_char(inicio, 'HH24:MI'), to_char(fin, 'HH24:MI') from agenda.agenda_base_tramo order by dia_de_la_semana",
            l => (Dia: (int)l.GetInt16(0), Inicio: l.GetString(1), Fin: l.GetString(2)));

        Assert.Equal([(1, "15:30", "18:00"), (2, "15:30", "18:00"), (3, "15:30", "18:00"), (4, "15:30", "18:00"), (5, "15:30", "18:00")], dias);
    }

    [Fact]
    public async Task Las_reglas_sembradas_son_1_dia_2_meses_120_minutos_y_tope_3()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();

        var reglas = await ambito.ServiceProvider.GetRequiredService<IReglasDeReserva>().ObtenerAsync(Cancelacion);

        Assert.Equal(1, reglas.AnticipacionMinimaDias);
        Assert.Equal(2, reglas.VentanaMeses);
        Assert.Equal(120, reglas.UmbralCancelacionTardiaMin);
        Assert.Equal(3, reglas.TopeSemanalPorTelefono);
    }

    [Fact]
    public async Task Las_reglas_cambiadas_se_guardan()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            var puerto = ambito.ServiceProvider.GetRequiredService<IReglasDeReserva>();
            var reglas = await puerto.ObtenerAsync(Cancelacion);
            Assert.Null(reglas.Cambiar(2, 3, 60, 4));
            await puerto.GuardarAsync(reglas, Cancelacion);
        }

        var guardadas = await escenario.FilasAsync(
            "select anticipacion_minima_dias, ventana_meses, umbral_cancelacion_tardia_min, tope_semanal_por_telefono "
            + "from agenda.reglas_de_reserva",
            l => (l.GetInt16(0), l.GetInt16(1), l.GetInt16(2), l.GetInt16(3)));
        Assert.Equal(((short)2, (short)3, (short)60, (short)4), Assert.Single(guardadas));
    }

    [Fact]
    public async Task Una_custom_guardada_se_vuelve_a_leer_con_sus_tramos_y_su_motivo()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var hoy = Hoy(escenario);
        var martes = new DateOnly(2026, 11, 3);
        var sabado = new DateOnly(2026, 11, 7);

        await CambiarAsync(escenario, hoy, agenda =>
        {
            Assert.Null(agenda.RegistrarCustom(martes, [T(17, 0, 18, 0), T(15, 30, 16, 30)], "cirugía", Media));
            Assert.Null(agenda.RegistrarCustom(sabado, [], "vacaciones", Media));
        });

        var leida = await ObtenerAsync(escenario, hoy);
        Assert.Equal([martes, sabado], leida.FechasConCustom());

        var vigentes = leida.TramosVigentes(martes);
        Assert.Equal([T(15, 30, 16, 30), T(17, 0, 18, 0)], vigentes.Tramos);
        Assert.Equal(OrigenDeTramos.Custom, vigentes.Origen);
        Assert.Equal("cirugía", leida.Customs.Single(c => c.Fecha == martes).Motivo);
        Assert.Empty(leida.TramosVigentes(sabado).Tramos);
        Assert.Equal(OrigenDeTramos.Custom, leida.TramosVigentes(sabado).Origen);

        // La custom sin tramos es solo su fila: "ese día no se atiende".
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom where fecha = '2026-11-07'"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom_tramo where fecha = '2026-11-07'"));
    }

    [Fact]
    public async Task Cambiar_y_borrar_una_custom_sincroniza_sus_filas()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var hoy = Hoy(escenario);
        var martes = new DateOnly(2026, 11, 3);
        var miercoles = new DateOnly(2026, 11, 4);

        await CambiarAsync(escenario, hoy, agenda =>
        {
            Assert.Null(agenda.RegistrarCustom(martes, [T(15, 30, 16, 30), T(17, 0, 18, 0)], "cirugía", Media));
            Assert.Null(agenda.RegistrarCustom(miercoles, [], "vacaciones", Media));
        });

        await CambiarAsync(escenario, hoy, agenda =>
        {
            Assert.Null(agenda.CambiarCustom(martes, [T(15, 30, 17, 0)], "congreso", Media));
            Assert.Null(agenda.BorrarCustom(miercoles));
        });

        var leida = await ObtenerAsync(escenario, hoy);
        var custom = Assert.Single(leida.Customs);
        Assert.Equal(martes, custom.Fecha);
        Assert.Equal("congreso", custom.Motivo);
        Assert.Equal([T(15, 30, 17, 0)], custom.Tramos);
        Assert.Equal(OrigenDeTramos.Base, leida.TramosVigentes(miercoles).Origen);
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom_tramo"));
    }

    [Fact]
    public async Task Cambiar_la_base_se_guarda()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var hoy = Hoy(escenario);

        await CambiarAsync(escenario, hoy, agenda =>
        {
            Assert.Null(agenda.CambiarBase(DayOfWeek.Monday, [T(15, 30, 17, 0), T(17, 0, 19, 0)], Media));
            Assert.Null(agenda.CambiarBase(DayOfWeek.Friday, [], Media));
            Assert.Null(agenda.CambiarBase(DayOfWeek.Saturday, [T(10, 0, 13, 0)], Media));
        });

        var leida = await ObtenerAsync(escenario, hoy);
        Assert.Equal([T(15, 30, 17, 0), T(17, 0, 19, 0)], leida.TramosBase(DayOfWeek.Monday));
        Assert.Empty(leida.TramosBase(DayOfWeek.Friday));
        Assert.Equal([T(10, 0, 13, 0)], leida.TramosBase(DayOfWeek.Saturday));
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_base_tramo where dia_de_la_semana = 6"));
        Assert.Equal([T(15, 30, 18, 0)], leida.TramosBase(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Una_custom_de_ayer_no_se_carga_y_guardar_no_la_toca()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var hoy = Hoy(escenario);
        var ayer = hoy.AddDays(-1);
        var manana = hoy.AddDays(1);
        await InsertarCustomAsync(escenario, ayer, "vacaciones");
        await CambiarAsync(escenario, hoy, agenda => Assert.Null(agenda.RegistrarCustom(manana, [], "vacaciones", Media)));

        await CambiarAsync(escenario, hoy, agenda =>
        {
            Assert.Equal([manana], agenda.FechasConCustom());
            Assert.Equal(OrigenDeTramos.Base, agenda.TramosVigentes(ayer).Origen);
        });

        // La de ayer sigue en la base aunque el agregado no la cargó.
        Assert.Equal(2L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom"));
    }

    [Fact]
    public async Task Registrar_una_custom_de_ayer_en_la_agenda_de_hoy_falla_y_no_toca_la_que_ya_habia()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var hoy = Hoy(escenario);
        var ayer = hoy.AddDays(-1);
        await InsertarCustomAsync(escenario, ayer, "vacaciones");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CambiarAsync(escenario, hoy, agenda =>
        {
            // El agregado no cargó la de ayer, así que no la ve (INV-A1 no la alcanza): el puerto es quien se niega.
            Assert.Null(agenda.RegistrarCustom(ayer, [T(16, 0, 17, 0)], "cirugía", Media));
        }));

        Assert.Equal("vacaciones", await escenario.EscalarAsync<string>($"select motivo from agenda.agenda_custom where fecha = '{ayer:yyyy-MM-dd}'"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom_tramo"));
    }

    [Fact]
    public async Task Guardar_una_agenda_recien_creada_no_borra_las_custom_pasadas()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var ayer = Hoy(escenario).AddDays(-1);
        await InsertarCustomAsync(escenario, ayer, "vacaciones");

        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            await ambito.ServiceProvider.GetRequiredService<IAgendaAtencion>().GuardarAsync(AgendaAtencion.Crear(), Cancelacion);
        }

        Assert.Equal("vacaciones", await escenario.EscalarAsync<string>($"select motivo from agenda.agenda_custom where fecha = '{ayer:yyyy-MM-dd}'"));
    }

    [Fact]
    public async Task Unas_reglas_fuera_del_rango_de_smallint_no_se_guardan_truncadas()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            var puerto = ambito.ServiceProvider.GetRequiredService<IReglasDeReserva>();
            var reglas = await puerto.ObtenerAsync(Cancelacion);
            Assert.Null(reglas.Cambiar(1, 2, 40000, 3));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => puerto.GuardarAsync(reglas, Cancelacion));
            Assert.IsType<OverflowException>(error.InnerException);
        }

        Assert.Equal((short)120, await escenario.EscalarAsync<short>("select umbral_cancelacion_tardia_min from agenda.reglas_de_reserva"));
    }

    private static Task InsertarCustomAsync(ReservaEnLineaTests.Escenario escenario, DateOnly fecha, string motivo) =>
        escenario.EjecutarAsync(
            $"insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('{fecha:yyyy-MM-dd}', '{motivo}', now(), now())");

    private static DateOnly Hoy(ReservaEnLineaTests.Escenario escenario) =>
        HoraLocal.DesdeUtc(escenario.Web.Services.GetRequiredService<IClock>().UtcNow).Fecha;

    private static async Task<AgendaAtencion> ObtenerAsync(ReservaEnLineaTests.Escenario escenario, DateOnly desde)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IAgendaAtencion>().ObtenerAsync(desde, Cancelacion);
    }

    // Obtener, cambiar y guardar en un ámbito propio, como lo hará el handler.
    private static async Task CambiarAsync(ReservaEnLineaTests.Escenario escenario, DateOnly desde, Action<AgendaAtencion> cambiar)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var puerto = ambito.ServiceProvider.GetRequiredService<IAgendaAtencion>();
        var agenda = await puerto.ObtenerAsync(desde, Cancelacion);
        cambiar(agenda);
        await puerto.GuardarAsync(agenda, Cancelacion);
    }
}
