using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Crear, cambiar y desactivar Tipos de visita contra PostgreSQL real (CAP-16, entrada 6.7): lo que ven el sitio y
/// Agendar cita, la duración nueva en la siguiente reserva, las Citas que conserva un tipo desactivado, el último activo
/// y la bitácora. Con el escenario de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local;
/// "Primera consulta" y "Consulta de seguimiento" de 30 minutos, reservables en línea.
/// </summary>
public sealed class TiposDeVisitaEnConfiguracionTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crear_uno_solo_del_consultorio_lo_ofrece_en_Agendar_cita_y_no_en_el_sitio()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await LlamarAsync(escenario, (ICommandHandler<CrearTipoDeVisita, TipoDeVisitaGuardado> h) => h.HandleAsync(
            new CrearTipoDeVisita { Nombre = "Infiltración", DuracionMin = 45, ReservableEnLinea = false, Orden = 3 }, Cancelacion));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var activos = await LlamarAsync(escenario, (IQueryHandler<TiposDeVisitaActivos, IReadOnlyList<TipoDeVisitaActivo>> h) =>
            h.HandleAsync(new TiposDeVisitaActivos(), Cancelacion));
        Assert.Contains(activos.Valor, t => t.Id == resultado.Valor.Id && t.Nombre == "Infiltración" && t.DuracionMin == 45);
        Assert.DoesNotContain(await ReservablesAsync(escenario), t => t.Id == resultado.Valor.Id);

        var entrada = Assert.Single(await BitacoraAsync(escenario));
        Assert.Equal(("Administrador", IdDelAdministrador, resultado.Valor.Id.Valor.ToString(), "Registrada"),
            (entrada.Actor, entrada.Id, entrada.EntidadId, entrada.Accion));
    }

    [Fact]
    public async Task Cambiar_la_duracion_cambia_la_rejilla_de_la_siguiente_reserva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await CambiarAsync(escenario, Cambio(TiposDeVisitaSembrados.PrimeraConsulta, "Primera consulta", 45, orden: 1));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var martes = Assert.Single(await HorariosAsync(escenario), d => d.Fecha == Manana);
        Assert.Equal([new TimeOnly(15, 30), new TimeOnly(16, 15), new TimeOnly(17, 0)], martes.Horas);

        using var detalle = JsonDocument.Parse(Assert.Single(await BitacoraAsync(escenario)).Detalle);
        Assert.Equal(30, detalle.RootElement.GetProperty("antes").GetProperty("duracionMin").GetInt32());
        Assert.Equal(45, detalle.RootElement.GetProperty("despues").GetProperty("duracionMin").GetInt32());
    }

    [Fact]
    public async Task Desactivar_lo_quita_del_sitio_y_de_Agendar_cita_y_sus_Citas_siguen_en_el_historial()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando(Manana, new TimeOnly(16, 0));
        comando.TipoDeVisitaId = TiposDeVisitaSembrados.ConsultaDeSeguimiento;
        Assert.True((await escenario.AgendarAsync(comando)).EsExito);
        var ana = await escenario.PacienteDeAnaAsync();

        var resultado = await DesactivarAsync(escenario, TiposDeVisitaSembrados.ConsultaDeSeguimiento);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.DoesNotContain(await ReservablesAsync(escenario), t => t.Id == TiposDeVisitaSembrados.ConsultaDeSeguimiento);
        var activos = await LlamarAsync(escenario, (IQueryHandler<TiposDeVisitaActivos, IReadOnlyList<TipoDeVisitaActivo>> h) =>
            h.HandleAsync(new TiposDeVisitaActivos(), Cancelacion));
        Assert.DoesNotContain(activos.Valor, t => t.Id == TiposDeVisitaSembrados.ConsultaDeSeguimiento);
        var historial = await LlamarAsync(escenario, (IQueryHandler<HistorialDelPaciente, PacienteConHistorial> h) =>
            h.HandleAsync(new HistorialDelPaciente(new PatientId(ana)), Cancelacion));
        Assert.Single(historial.Valor.Citas);
        Assert.Equal("Agendada", Assert.Single(await escenario.CitasAsync()).Estado);

        using var detalle = JsonDocument.Parse(Assert.Single(await BitacoraAsync(escenario)).Detalle);
        Assert.True(detalle.RootElement.GetProperty("antes").GetProperty("activo").GetBoolean());
        Assert.False(detalle.RootElement.GetProperty("despues").GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task El_ultimo_activo_no_se_desactiva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await DesactivarAsync(escenario, TiposDeVisitaSembrados.ConsultaDeSeguimiento)).EsExito);

        var resultado = await DesactivarAsync(escenario, TiposDeVisitaSembrados.PrimeraConsulta);

        Assert.Equal(CodigosDeAgenda.UltimoTipoActivo, resultado.Codigo);
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.tipo_de_visita where activo"));
    }

    [Fact]
    public async Task Inexistente_o_ya_inactivo_no_se_cambia_ni_se_desactiva_dos_veces()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await DesactivarAsync(escenario, TiposDeVisitaSembrados.ConsultaDeSeguimiento)).EsExito);

        Assert.Equal(CodigosDeAgenda.TipoNoEncontrado, (await DesactivarAsync(escenario, TiposDeVisitaSembrados.ConsultaDeSeguimiento)).Codigo);
        Assert.Equal(CodigosDeAgenda.TipoNoEncontrado,
            (await CambiarAsync(escenario, Cambio(TiposDeVisitaSembrados.ConsultaDeSeguimiento, "Seguimiento", 30, 2))).Codigo);
        Assert.Equal(CodigosDeAgenda.TipoNoEncontrado, (await CambiarAsync(escenario, Cambio(TipoDeVisitaId.Nuevo(), "Otro", 30, 2))).Codigo);
        Assert.Single(await BitacoraAsync(escenario));
    }

    [Fact]
    public async Task La_forma_y_los_mismos_datos_no_dejan_bitacora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var sinNombre = await CambiarAsync(escenario, Cambio(TiposDeVisitaSembrados.PrimeraConsulta, " ", 30, 1));
        var cero = await CambiarAsync(escenario, Cambio(TiposDeVisitaSembrados.PrimeraConsulta, "Primera consulta", 0, 1));
        var largo = await CambiarAsync(escenario, Cambio(TiposDeVisitaSembrados.PrimeraConsulta, "Primera consulta", 600, 1));
        var vigente = Assert.Single((await EditablesAsync(escenario)), t => t.Id == TiposDeVisitaSembrados.PrimeraConsulta);
        var mismos = await CambiarAsync(escenario, new CambiarTipoDeVisita
        {
            Id = vigente.Id, Nombre = vigente.Nombre, Descripcion = vigente.Descripcion, DuracionMin = vigente.DuracionMin,
            ReservableEnLinea = vigente.ReservableEnLinea, Orden = vigente.Orden,
        });

        Assert.Equal([DatosDeTipoDeVisita.MensajeDeNombre], sinNombre.Errores[nameof(DatosDeTipoDeVisita.Nombre)]);
        Assert.Equal([DatosDeTipoDeVisita.MensajeDeDuracion], cero.Errores[nameof(DatosDeTipoDeVisita.DuracionMin)]);
        Assert.Equal([DatosDeTipoDeVisita.MensajeDeDuracion], largo.Errores[nameof(DatosDeTipoDeVisita.DuracionMin)]);
        Assert.True(mismos.EsExito);
        Assert.Empty(await BitacoraAsync(escenario));
    }

    [Fact]
    public async Task La_consulta_da_los_activos_en_orden_y_sin_sesion_nada_se_puede()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var editables = await EditablesAsync(escenario);
        Assert.Equal([TiposDeVisitaSembrados.PrimeraConsulta, TiposDeVisitaSembrados.ConsultaDeSeguimiento], editables.Select(t => t.Id));

        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var proveedor = ambito.ServiceProvider;
        Assert.Equal(FormaDeResultado.NoAutorizado, (await proveedor.GetRequiredService<ICommandHandler<CrearTipoDeVisita, TipoDeVisitaGuardado>>()
            .HandleAsync(new CrearTipoDeVisita { Nombre = "Otro", DuracionMin = 30, Orden = 3 }, Cancelacion)).Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, (await proveedor.GetRequiredService<ICommandHandler<CambiarTipoDeVisita, TipoDeVisitaGuardado>>()
            .HandleAsync(Cambio(TiposDeVisitaSembrados.PrimeraConsulta, "Otra", 30, 1), Cancelacion)).Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, (await proveedor.GetRequiredService<ICommandHandler<DesactivarTipoDeVisita, TipoDeVisitaGuardado>>()
            .HandleAsync(new DesactivarTipoDeVisita { Id = TiposDeVisitaSembrados.ConsultaDeSeguimiento }, Cancelacion)).Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, (await proveedor.GetRequiredService<IQueryHandler<TiposDeVisitaEnConfiguracion, IReadOnlyList<TipoDeVisitaEditable>>>()
            .HandleAsync(new TiposDeVisitaEnConfiguracion(), Cancelacion)).Forma);
        Assert.Empty(await BitacoraAsync(escenario));
    }

    private static CambiarTipoDeVisita Cambio(TipoDeVisitaId id, string nombre, int duracion, int orden) => new()
    {
        Id = id, Nombre = nombre, DuracionMin = duracion, ReservableEnLinea = true, Orden = orden,
    };

    private static async Task<T> LlamarAsync<THandler, T>(Escenario escenario, Func<THandler, Task<T>> llamar)
        where THandler : notnull
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));
        return await llamar(ambito.ServiceProvider.GetRequiredService<THandler>());
    }

    private static Task<Resultado<TipoDeVisitaGuardado>> CambiarAsync(Escenario escenario, CambiarTipoDeVisita comando) =>
        LlamarAsync(escenario, (ICommandHandler<CambiarTipoDeVisita, TipoDeVisitaGuardado> h) => h.HandleAsync(comando, Cancelacion));

    private static Task<Resultado<TipoDeVisitaGuardado>> DesactivarAsync(Escenario escenario, TipoDeVisitaId id) =>
        LlamarAsync(escenario, (ICommandHandler<DesactivarTipoDeVisita, TipoDeVisitaGuardado> h) =>
            h.HandleAsync(new DesactivarTipoDeVisita { Id = id }, Cancelacion));

    private static async Task<IReadOnlyList<TipoDeVisitaEditable>> EditablesAsync(Escenario escenario) =>
        (await LlamarAsync(escenario, (IQueryHandler<TiposDeVisitaEnConfiguracion, IReadOnlyList<TipoDeVisitaEditable>> h) =>
            h.HandleAsync(new TiposDeVisitaEnConfiguracion(), Cancelacion))).Valor;

    private static async Task<IReadOnlyList<TipoDeVisitaReservable>> ReservablesAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<TiposDeVisitaReservables, IReadOnlyList<TipoDeVisitaReservable>>>()
            .HandleAsync(new TiposDeVisitaReservables(), Cancelacion);
        return resultado.Valor;
    }

    private static async Task<IReadOnlyList<DiaConHorarios>> HorariosAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static Task<List<(string Actor, string? Id, string EntidadId, string Accion, string Detalle)>> BitacoraAsync(Escenario escenario) =>
        escenario.FilasAsync(
            "select actor_tipo, actor_id, entidad_id, accion, detalle::text from agenda.bitacora where entidad = 'TipoDeVisita'",
            l => (l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1), l.GetString(2), l.GetString(3), l.GetString(4)));
}
