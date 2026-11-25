using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.Web.Identidad;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Lo que el <c>MigrationService</c> deja en la base (AD-2, AD-25): los esquemas de los módulos y el del host, y los
/// datos iniciales de Selina, comprobados sobre una base migrada sin la agenda de prueba del arnés.
/// </summary>
public sealed class BaseMigradaTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AgendaDbContext.Esquema)]
    [InlineData(PacientesDbContext.Esquema)]
    [InlineData(NotificacionesDbContext.Esquema)]
    [InlineData(IdentidadDbContext.Esquema)]
    public async Task Cada_modulo_lleva_su_historial_de_migraciones_en_su_esquema(string esquema)
    {
        var tablas = await Consultas.TablasAsync(arnes.CadenaDeConexion, esquema, Cancelacion);

        Assert.Contains(Consultas.Historial, tablas);
        Assert.True(await Consultas.MigracionesAplicadasAsync(arnes.CadenaDeConexion, esquema, Cancelacion) >= 1);
    }

    [Fact]
    public async Task No_hay_historial_de_migraciones_fuera_de_los_esquemas_de_los_modulos()
    {
        var esquemas = await Consultas.EsquemasConTablaAsync(arnes.CadenaDeConexion, Consultas.Historial, Cancelacion);

        Assert.DoesNotContain("public", esquemas);
        Assert.Equal(
            [AgendaDbContext.Esquema, IdentidadDbContext.Esquema, NotificacionesDbContext.Esquema, PacientesDbContext.Esquema],
            esquemas);
    }

    [Fact]
    public async Task La_base_migrada_no_tiene_el_esquema_wolverine()
    {
        // El MigrationService ya no crea ningún almacenamiento de mensajes: ese esquema no existe.
        Assert.False(await Consultas.ExisteEsquemaAsync(arnes.CadenaDeConexion, "wolverine", Cancelacion));
    }

    // La semilla real, la de Selina (Gerardo, 2026-11-24), sobre una base migrada sin la agenda de prueba del arnés.
    // El reloj, en el lunes 2 de noviembre de 2026 a las 10:00 de Ciudad de México (UTC-6).
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 16, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    [Fact]
    public async Task La_migracion_siembra_la_agenda_base_de_lunes_a_viernes_de_3_a_7_pm()
    {
        var cadena = await BaseConLaSemillaRealAsync();

        var tramos = await FilasAsync(
            cadena, "select dia_de_la_semana, inicio, fin from agenda.agenda_base_tramo order by dia_de_la_semana",
            l => (Dia: l.GetInt16(0), Inicio: l.GetFieldValue<TimeOnly>(1), Fin: l.GetFieldValue<TimeOnly>(2)));

        Assert.Equal(
            [(1, new(15, 0), new(19, 0)), (2, new(15, 0), new(19, 0)), (3, new(15, 0), new(19, 0)), (4, new(15, 0), new(19, 0)), (5, new(15, 0), new(19, 0))],
            tramos.Select(t => ((int)t.Dia, t.Inicio, t.Fin)).ToArray());
    }

    [Fact]
    public async Task La_migracion_siembra_los_dos_tipos_de_visita_de_60_minutos_y_reservables_en_linea()
    {
        var cadena = await BaseConLaSemillaRealAsync();

        var tipos = await FilasAsync(
            cadena, "select id, duracion_min, reservable_en_linea from agenda.tipo_de_visita order by orden",
            l => (Id: l.GetGuid(0), Duracion: l.GetInt32(1), Reservable: l.GetBoolean(2)));

        Assert.Equal(
            [(TiposDeVisitaSembrados.PrimeraConsulta.Valor, 60, true), (TiposDeVisitaSembrados.ConsultaDeSeguimiento.Valor, 60, true)],
            tipos.ToArray());
    }

    [Fact]
    public async Task La_migracion_siembra_la_Agenda_del_dia_a_las_8_y_el_Recordatorio_a_las_9()
    {
        var cadena = await BaseConLaSemillaRealAsync();

        var fila = Assert.Single(await FilasAsync(
            cadena, "select hora_recordatorio, hora_agenda_del_dia from notificaciones.configuracion_de_envio",
            l => (Recordatorio: l.GetFieldValue<TimeOnly>(0), AgendaDelDia: l.GetFieldValue<TimeOnly>(1))));

        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(8, 0)), fila);
    }

    [Fact]
    public async Task Con_la_semilla_real_un_martes_ofrece_3_4_5_y_6_pm_y_el_sabado_nada()
    {
        var cadena = await BaseConLaSemillaRealAsync();
        await using var web = arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba(Ahora));

        var dias = await HorariosAsync(web);

        var martes = Assert.Single(dias, d => d.Fecha == Martes);
        Assert.Equal([new(15, 0), new(16, 0), new(17, 0), new(18, 0)], martes.Horas);
        Assert.DoesNotContain(dias, d => d.Fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    [Fact]
    public async Task Con_la_semilla_real_un_sabado_abierto_con_Agenda_custom_ofrece_sus_horas_de_una_hora()
    {
        var cadena = await BaseConLaSemillaRealAsync();
        await using var web = arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba(Ahora));

        await using (var ambito = web.Services.CreateAsyncScope())
        {
            Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "cuenta-del-administrador"), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")));
            var guardada = await ambito.ServiceProvider
                .GetRequiredService<ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>>()
                .HandleAsync(
                    new GuardarAgendaCustom
                    {
                        Fecha = Sabado,
                        Motivo = "sábado abierto",
                        Tramos = [new TramoAGuardar { Inicio = new TimeOnly(10, 0), Fin = new TimeOnly(13, 0) }],
                    },
                    Cancelacion);
            Assert.True(guardada.EsExito, $"{guardada.Forma} {guardada.Codigo}");
        }

        var sabado = Assert.Single(await HorariosAsync(web), d => d.Fecha == Sabado);
        Assert.Equal([new(10, 0), new(11, 0), new(12, 0)], sabado.Horas);
    }

    [Fact]
    public async Task Con_la_semilla_real_la_Cita_del_martes_a_las_3_pm_termina_a_las_4_pm()
    {
        var cadena = await BaseConLaSemillaRealAsync();
        await using var web = arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba(Ahora));

        Resultado<CitaAgendadaEnLinea> agendada;
        await using (var ambito = web.Services.CreateAsyncScope())
        {
            ReservaEnLineaTests.SinSesion(ambito);
            agendada = await ambito.ServiceProvider
                .GetRequiredService<ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>>()
                .HandleAsync(ReservaEnLineaTests.Comando(Martes, new TimeOnly(15, 0)), Cancelacion);
        }

        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        var rango = Assert.Single(await FilasAsync(
            cadena, $"select inicio, fin from agenda.cita where id = '{agendada.Valor.CitaId.Valor}'",
            l => (Inicio: l.GetFieldValue<DateTimeOffset>(0), Fin: l.GetFieldValue<DateTimeOffset>(1))));

        // Ciudad de México, UTC-6: de 15:00 a 16:00 local.
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.FromHours(-6)), rango.Inicio);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 16, 0, 0, TimeSpan.FromHours(-6)), rango.Fin);
    }

    // Una base nueva migrada por el MigrationService, sin la agenda de prueba que el arnés pone en las suyas.
    private async Task<string> BaseConLaSemillaRealAsync()
    {
        var cadena = await arnes.CrearBaseVaciaAsync(Cancelacion);
        await MigrationService.EjecutarAsync(cadena, Cancelacion);
        return cadena;
    }

    private static async Task<IReadOnlyList<DiaConHorarios>> HorariosAsync(WebApplicationFactory<Program> web)
    {
        await using var ambito = web.Services.CreateAsyncScope();
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta, null), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<List<T>> FilasAsync<T>(string cadena, string sql, Func<NpgsqlDataReader, T> leer)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(Cancelacion);
        var filas = new List<T>();
        while (await lector.ReadAsync(Cancelacion))
        {
            filas.Add(leer(lector));
        }

        return filas;
    }
}
