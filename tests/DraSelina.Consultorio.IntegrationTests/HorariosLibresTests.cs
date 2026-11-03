using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="HorariosLibres"/> por su handler contra PostgreSQL real: la agenda de atención y las reglas sembradas, y
/// las Citas guardadas. Cada prueba usa su propia base migrada y el reloj en el lunes 2 de noviembre de 2026 a las
/// 10:00 de Ciudad de México.
/// </summary>
public sealed class HorariosLibresTests(Arnes arnes)
{
    /// <summary>Lunes 2 de noviembre de 2026, 10:00 en Ciudad de México (UTC-6).</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 16, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Lunes = new(2026, 11, 2);

    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly TimeOnly[] TardeCompleta =
        [new(15, 30), new(16, 0), new(16, 30), new(17, 0), new(17, 30)];

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Con_la_base_sembrada_el_martes_ofrece_la_tarde_y_no_aparecen_hoy_ni_el_fin_de_semana()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.ConsultarAsync(TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var dias = resultado.Valor;
        Assert.Equal(Martes, dias[0].Fecha);
        Assert.Equal(TardeCompleta, dias[0].Horas);
        Assert.DoesNotContain(dias, d => d.Fecha == Lunes);
        Assert.DoesNotContain(dias, d => d.Fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        // La ventana de 2 meses: hasta el sábado 2 de enero de 2027, así que el último día hábil es el viernes 1.
        Assert.Equal(new DateOnly(2027, 1, 1), dias[^1].Fecha);
        Assert.All(dias, d => Assert.Equal(TardeCompleta, d.Horas));
    }

    [Fact]
    public async Task Una_Cita_guardada_quita_su_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Martes, new TimeOnly(16, 0))).EsExito);

        var resultado = await escenario.ConsultarAsync(TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var martes = Assert.Single(resultado.Valor, d => d.Fecha == Martes);
        Assert.Equal([new(15, 30), new(16, 30), new(17, 0), new(17, 30)], martes.Horas);

        // Las Citas son de Agenda, no del tipo: la otra consulta tampoco ofrece ese horario.
        var seguimiento = await escenario.ConsultarAsync(TiposDeVisitaSembrados.ConsultaDeSeguimiento);
        Assert.DoesNotContain(new TimeOnly(16, 0), Assert.Single(seguimiento.Valor, d => d.Fecha == Martes).Horas);
    }

    [Fact]
    public async Task Una_Cita_Cancelada_no_quita_su_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Martes, new TimeOnly(16, 0))).EsExito);
        await escenario.EjecutarAsync(
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()");

        var resultado = await escenario.ConsultarAsync(TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.Equal(TardeCompleta, Assert.Single(resultado.Valor, d => d.Fecha == Martes).Horas);
    }

    [Fact]
    public async Task Un_dia_lleno_aparece_con_la_lista_vacia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        foreach (var hora in TardeCompleta)
        {
            Assert.True((await escenario.AgendarAsync(Martes, hora)).EsExito);
        }

        var resultado = await escenario.ConsultarAsync(TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.Empty(Assert.Single(resultado.Valor, d => d.Fecha == Martes).Horas);
    }

    [Fact]
    public async Task Una_fecha_sin_ningun_horario_posible_para_el_tipo_no_viene()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync(
            $"update agenda.tipo_de_visita set duracion_min = 45 where id = '{TiposDeVisitaSembrados.ConsultaDeSeguimiento}'");
        await escenario.EjecutarAsync(
            "insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('2026-11-03', 'tarde corta', now(), now())");
        await escenario.EjecutarAsync(
            "insert into agenda.agenda_custom_tramo (id, fecha, inicio, fin) values (gen_random_uuid(), '2026-11-03', '15:30', '16:00')");

        var seguimiento = await escenario.ConsultarAsync(TiposDeVisitaSembrados.ConsultaDeSeguimiento);
        var primera = await escenario.ConsultarAsync(TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.DoesNotContain(seguimiento.Valor, d => d.Fecha == Martes);
        Assert.Equal([new TimeOnly(15, 30)], Assert.Single(primera.Valor, d => d.Fecha == Martes).Horas);
    }

    [Fact]
    public async Task Un_tipo_inexistente_vuelve_como_tipo_no_reservable()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.ConsultarAsync(TipoDeVisitaId.Nuevo());

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.TipoNoReservable, resultado.Codigo);
    }

    [Theory]
    [InlineData("reservable_en_linea")]
    [InlineData("activo")]
    public async Task Un_tipo_que_no_se_agenda_en_linea_vuelve_como_tipo_no_reservable(string columna)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync(
            $"update agenda.tipo_de_visita set {columna} = false where id = '{TiposDeVisitaSembrados.PrimeraConsulta}'");

        var resultado = await escenario.ConsultarAsync(TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.TipoNoReservable, resultado.Codigo);
    }

    /// <summary>Una base migrada propia y la web en memoria sobre ella, con el reloj en <see cref="Ahora"/>.</summary>
    private sealed class Escenario : IAsyncDisposable
    {
        private readonly string _cadena;

        private readonly WebApplicationFactory<Program> _web;

        private Escenario(string cadena, WebApplicationFactory<Program> web)
        {
            _cadena = cadena;
            _web = web;
        }

        public static async Task<Escenario> CrearAsync(Arnes arnes)
        {
            var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
            return new Escenario(cadena, arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba(Ahora)));
        }

        /// <summary>El handler se pide al contenedor por su interfaz, en un ámbito propio, como lo hará la página.</summary>
        public async Task<Resultado<IReadOnlyList<DiaConHorarios>>> ConsultarAsync(TipoDeVisitaId tipo)
        {
            await using var ambito = _web.Services.CreateAsyncScope();
            return await ambito.ServiceProvider
                .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
                .HandleAsync(new HorariosLibres(tipo), Cancelacion);
        }

        /// <summary>Una Cita en línea de Primera consulta guardada por su propio handler, sin sesión.</summary>
        public async Task<Resultado<CitaAgendadaEnLinea>> AgendarAsync(DateOnly fecha, TimeOnly hora)
        {
            var comando = ReservaEnLineaTests.Comando(fecha, hora);
            await using var ambito = _web.Services.CreateAsyncScope();
            ReservaEnLineaTests.SinSesion(ambito);
            return await ambito.ServiceProvider
                .GetRequiredService<ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>>()
                .HandleAsync(comando, Cancelacion);
        }

        public async Task EjecutarAsync(string sql)
        {
            await using var conexion = new NpgsqlConnection(_cadena);
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand(sql, conexion);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        public ValueTask DisposeAsync() => _web.DisposeAsync();
    }
}
