using System.Net;
using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La pantalla "Agendar cita" (entrada 5.4) contra PostgreSQL real: sus tres consultas de apoyo
/// (<see cref="TiposDeVisitaActivos"/>, <see cref="HorarioDeLaCitaDelConsultorio"/> y <see cref="PacienteReconocido"/>,
/// con RN-25 y sin sesión), la búsqueda sin crear de <see cref="IPacientesApi"/>, y por HTTP con sesión: la Agenda
/// abre la página con su fecha, la página se pinta con ella y, guardada la Cita, la Agenda de ese día la muestra. El
/// guardado mismo corre en el circuito de Blazor, que el cliente HTTP no abre: aquí va por el comando, en un ámbito con
/// la sesión del Administrador, como lo pide la página. Con el escenario y el reloj de <see cref="ReservaEnLineaTests"/>:
/// lunes 2 de noviembre de 2026 a las 12:00 local; base sembrada de lunes a viernes, 3:30 a 6:00 pm.
/// </summary>
public sealed class PantallaAgendarCitaTests(Arnes arnes)
{
    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Los_tipos_activos_incluyen_los_que_no_se_reservan_en_linea_en_su_orden_y_no_los_inactivos()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cirugia = await InsertarTipoAsync(escenario, "Cirugía menor", activo: true, orden: 3);
        await InsertarTipoAsync(escenario, "Ya no se da", activo: false, orden: 0);

        var tipos = await ComoAdministradorAsync(escenario, new TiposDeVisitaActivos());

        Assert.Equal(
            [
                new TipoDeVisitaActivo(TiposDeVisitaSembrados.PrimeraConsulta, "Primera consulta", 30, true),
                new TipoDeVisitaActivo(TiposDeVisitaSembrados.ConsultaDeSeguimiento, "Consulta de seguimiento", 30, true),
                new TipoDeVisitaActivo(cirugia, "Cirugía menor", 60, false),
            ],
            tipos);
    }

    [Theory]
    [InlineData(3, 16, 0, false, false)]
    [InlineData(3, 16, 10, false, false)]
    [InlineData(3, 17, 45, true, false)]
    [InlineData(3, 19, 0, true, false)]
    [InlineData(7, 10, 15, true, true)]
    public async Task El_horario_dice_si_la_Cita_queda_fuera_de_los_Tramos_o_en_un_dia_que_no_se_atiende(
        int dia, int hora, int minuto, bool fuera, bool sinAtencion)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var horario = await ComoAdministradorAsync(
            escenario,
            new HorarioDeLaCitaDelConsultorio(TiposDeVisitaSembrados.PrimeraConsulta, new DateOnly(2026, 11, dia), new TimeOnly(hora, minuto)));

        Assert.Equal(new HorarioDeLaCita(fuera, sinAtencion), horario);
    }

    [Fact]
    public async Task El_horario_usa_los_Tramos_vigentes_y_la_duracion_del_tipo_como_el_comando()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));
        var cirugia = await InsertarTipoAsync(escenario, "Cirugía menor", activo: true, orden: 3);

        Assert.Equal(
            new HorarioDeLaCita(false, false),
            await ComoAdministradorAsync(escenario, new HorarioDeLaCitaDelConsultorio(TiposDeVisitaSembrados.PrimeraConsulta, Sabado, new TimeOnly(10, 15))));
        // 5:15 a 6:15 con el de 60 minutos: se sale del Tramo de 3:30 a 6:00.
        Assert.Equal(
            new HorarioDeLaCita(true, false),
            await ComoAdministradorAsync(escenario, new HorarioDeLaCitaDelConsultorio(cirugia, Manana, new TimeOnly(17, 15))));

        // El comando, con la misma Cita y sin nota, pide la nota: la misma regla.
        var comando = Comando(Manana, new TimeOnly(17, 15));
        comando.TipoDeVisitaId = cirugia;
        Assert.Equal(CodigosDeAgenda.NotaObligatoria, (await AgendarAsync(escenario, comando)).Codigo);
    }

    [Fact]
    public async Task Un_tipo_que_no_existe_no_tiene_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await ConsultarAsync(
            escenario, new HorarioDeLaCitaDelConsultorio(TipoDeVisitaId.Nuevo(), Manana, new TimeOnly(16, 0)), conSesion: true);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.TipoNoReservable, resultado.Codigo);
    }

    [Fact]
    public async Task Un_Paciente_conocido_trae_su_proxima_Cita_Agendada_y_la_ultima_que_ya_empezo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        // Pasadas: la más reciente, Agendada, se ve Atendida (RN-31).
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 10, 28), new TimeOnly(16, 0), new TimeOnly(16, 30), ana);
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 10, 30), new TimeOnly(16, 0), new TimeOnly(16, 30), ana);
        await CambiarEstadoAsync(escenario, new DateOnly(2026, 10, 28), new TimeOnly(16, 0), "NoSePresento", null);
        // Futuras: la más cercana está cancelada; la siguiente Agendada es la próxima.
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(16, 0), new TimeOnly(16, 30), ana);
        await CambiarEstadoAsync(escenario, Manana, new TimeOnly(16, 0), "Cancelada", "tarde");
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 11, 5), new TimeOnly(17, 0), new TimeOnly(17, 30), ana);
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 11, 10), new TimeOnly(16, 0), new TimeOnly(16, 30), ana);
        // De otro Paciente, más cerca: no cuenta.
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 11, 4), new TimeOnly(16, 0), new TimeOnly(16, 30));

        var paciente = await ComoAdministradorAsync(escenario, new PacienteReconocido("ana sofia lopez", "+52 444 123 4567"));

        Assert.Equal(
            new PacienteDeLaCita(
                true,
                new CitaDelPaciente(new DateOnly(2026, 11, 5), new TimeOnly(17, 0), "Primera consulta", EstadoEnLaAgenda.Agendada, null),
                new CitaDelPaciente(new DateOnly(2026, 10, 30), new TimeOnly(16, 0), "Primera consulta", EstadoEnLaAgenda.Atendida, null)),
            paciente);
    }

    [Fact]
    public async Task Un_Paciente_conocido_sin_Citas_futuras_solo_trae_la_anterior_con_su_cancelacion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 10, 30), new TimeOnly(16, 0), new TimeOnly(16, 30), ana);
        await CambiarEstadoAsync(escenario, new DateOnly(2026, 10, 30), new TimeOnly(16, 0), "Cancelada", "consultorio");

        var paciente = await ComoAdministradorAsync(escenario, new PacienteReconocido("Ana Sofía López", "4441234567"));

        Assert.True(paciente.Existe);
        Assert.Null(paciente.Proxima);
        Assert.Equal(
            new CitaDelPaciente(new DateOnly(2026, 10, 30), new TimeOnly(16, 0), "Primera consulta", EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio),
            paciente.Anterior);
    }

    [Fact]
    public async Task Un_Paciente_sin_Citas_existe_sin_proxima_ni_anterior()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.PacienteDeAnaAsync();

        Assert.Equal(
            new PacienteDeLaCita(true, null, null),
            await ComoAdministradorAsync(escenario, new PacienteReconocido("Ana Sofía López", "4441234567")));
    }

    [Theory]
    [InlineData("Bruno Díaz", "4441234567")]
    [InlineData("Ana Sofía López", "4449876543")]
    [InlineData("Ana Sofía López", "44412345")]
    [InlineData("   ", "4441234567")]
    public async Task Otro_nombre_con_el_mismo_celular_otro_celular_o_datos_invalidos_no_se_reconocen_ni_se_crean(string nombre, string telefono)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.PacienteDeAnaAsync();

        Assert.Equal(PacienteDeLaCita.Desconocido, await ComoAdministradorAsync(escenario, new PacienteReconocido(nombre, telefono)));
        Assert.Single(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task La_busqueda_de_Pacientes_sin_crear_da_el_mismo_id_que_BuscarOCrear()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();

        Assert.Equal(new PatientId(ana), await api.BuscarAsync("ANA SOFÍA LÓPEZ ", "444-123-4567", Cancelacion));
        Assert.Null(await api.BuscarAsync("Bruno Díaz", "4449876543", Cancelacion));
        Assert.Single(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Reconocido_en_el_mismo_ambito_el_correo_del_comando_reemplaza_al_que_otro_ambito_guardo_entre_tanto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        // Ana empieza con ana@correo.com (A).
        var ana = await escenario.PacienteDeAnaAsync();

        // Un solo ámbito con la sesión, como el circuito de la página: primero reconoce, después agenda.
        await using var pagina = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(pagina);
        var reconocido = await pagina.ServiceProvider
            .GetRequiredService<IQueryHandler<PacienteReconocido, PacienteDeLaCita>>()
            .HandleAsync(new PacienteReconocido("Ana Sofía López", "4441234567"), Cancelacion);
        Assert.True(reconocido.Valor.Existe);

        // Entre tanto, otro ámbito le cambia el correo a B.
        await using (var otro = escenario.Web.Services.CreateAsyncScope())
        {
            var cambiado = await otro.ServiceProvider.GetRequiredService<IPacientesApi>()
                .BuscarOCrearAsync("Ana Sofía López", "4441234567", "ana.nueva@correo.com", Cancelacion);
            Assert.Equal(ana, cambiado.Valor.Valor);
        }

        // El comando llega con A: reemplaza a B (RN-30). Con un Paciente rastreado de la consulta, que todavía tiene A,
        // no habría cambio que guardar y quedaría B.
        var comando = Comando(Manana, new TimeOnly(16, 0));
        comando.Correo = "ana@correo.com";
        var agendada = await pagina.ServiceProvider
            .GetRequiredService<ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>>()
            .HandleAsync(comando, Cancelacion);

        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        Assert.Equal(ana, Assert.Single(await escenario.CitasAsync()).PacienteId);
        var paciente = Assert.Single(await escenario.PacientesAsync());
        Assert.Equal(ana, paciente.Id);
        Assert.Equal("ana@correo.com", paciente.Correo);
    }

    [Fact]
    public async Task Sin_sesion_las_tres_consultas_vuelven_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.PacienteDeAnaAsync();

        Assert.Equal(FormaDeResultado.NoAutorizado, (await ConsultarAsync(escenario, new TiposDeVisitaActivos(), conSesion: false)).Forma);
        Assert.Equal(
            FormaDeResultado.NoAutorizado,
            (await ConsultarAsync(escenario, new PacienteReconocido("Ana Sofía López", "4441234567"), conSesion: false)).Forma);
        Assert.Equal(
            FormaDeResultado.NoAutorizado,
            (await ConsultarAsync(
                escenario, new HorarioDeLaCitaDelConsultorio(TiposDeVisitaSembrados.PrimeraConsulta, Manana, new TimeOnly(16, 0)), conSesion: false)).Forma);
    }

    [Fact]
    public void La_consulta_del_Paciente_no_muestra_nombre_ni_celular_al_convertirse_en_texto()
    {
        var texto = new PacienteReconocido("Ana Sofía López", "4441234567").ToString();

        Assert.DoesNotContain("Ana", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("4441234567", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_sesion_la_pagina_lleva_a_entrar_con_su_direccion_de_regreso()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        using var cliente = EntrarConSegundoFactorTests.Cliente(escenario.Web);

        using var respuesta = await cliente.GetAsync("/admin/agendar?fecha=2026-11-07", Cancelacion);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(
            "/admin/entrar?ReturnUrl=%2Fadmin%2Fagendar%3Ffecha%3D2026-11-07",
            respuesta.Headers.Location!.IsAbsoluteUri ? respuesta.Headers.Location.PathAndQuery : respuesta.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task Con_sesion_la_Agenda_abre_la_pagina_con_su_fecha_y_la_Cita_guardada_queda_en_la_Agenda_del_dia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        using var cliente = await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);

        // La Agenda del sábado lleva "Agendar cita" con su fecha.
        var agenda = await LeerAsync(cliente, "/admin?fecha=2026-11-07&vista=dia");
        Assert.Contains("href=\"/admin/agendar?fecha=2026-11-07\"", agenda, StringComparison.Ordinal);

        // La página se pinta con la fecha puesta y los tipos activos.
        var pagina = await LeerAsync(cliente, "/admin/agendar?fecha=2026-11-07");
        Assert.Contains(">Agendar cita</h1>", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"2026-11-07\"", pagina, StringComparison.Ordinal);
        Assert.Contains("Primera consulta · 30 min", pagina, StringComparison.Ordinal);
        Assert.Contains("Consulta de seguimiento · 30 min", pagina, StringComparison.Ordinal);

        // UJ-6 con el Paciente de UJ-4: el sábado a las 10:15, con nota; es el mismo Paciente, no otro.
        var comando = Comando(Sabado, new TimeOnly(10, 15));
        comando.Nota = "lo pidió Selina, no puede entre semana";
        var agendada = await AgendarAsync(escenario, comando);
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        Assert.True(agendada.Valor.FueraDeHorario);
        Assert.Equal(ana, Assert.Single(await escenario.CitasAsync()).PacienteId);
        Assert.Single(await escenario.PacientesAsync());

        // La página vuelve a la Agenda de la fecha de la Cita, en vista de día: ahí está.
        var deVuelta = await LeerAsync(cliente, DraSelina.Consultorio.UI.Pages.Administracion.Agenda.Direccion(agendada.Valor.Fecha, semana: false));
        Assert.Contains("Ana Sofía López", deVuelta, StringComparison.Ordinal);
        Assert.Contains("lo pidió Selina, no puede entre semana", deVuelta, StringComparison.Ordinal);
        Assert.Contains("Fuera de horario", deVuelta, StringComparison.Ordinal);
    }

    private static AgendarCitaPorConsultorio Comando(DateOnly fecha, TimeOnly hora) => new()
    {
        TipoDeVisitaId = TiposDeVisitaSembrados.PrimeraConsulta,
        Fecha = fecha,
        Hora = hora,
        Nombre = "Ana Sofía López",
        Telefono = "444 123 4567",
        Consentimiento = true,
    };

    private static async Task<string> LeerAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {ruta}: {respuesta.StatusCode}");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        ((IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "cuenta-del-administrador"), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")))));

    private static async Task<Resultado<CitaAgendadaPorConsultorio>> AgendarAsync(Escenario escenario, AgendarCitaPorConsultorio comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>>()
            .HandleAsync(comando, Cancelacion);
    }

    private static async Task<Resultado<T>> ConsultarAsync<T>(Escenario escenario, IQuery<T> consulta, bool conSesion)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        if (conSesion)
        {
            ComoAdministrador(ambito);
        }
        else
        {
            SinSesion(ambito);
        }

        var handler = ambito.ServiceProvider.GetRequiredService(typeof(IQueryHandler<,>).MakeGenericType(consulta.GetType(), typeof(T)));
        var tarea = (Task<Resultado<T>>)handler.GetType().GetMethod("HandleAsync")!.Invoke(handler, [consulta, Cancelacion])!;
        return await tarea;
    }

    private static async Task<T> ComoAdministradorAsync<T>(Escenario escenario, IQuery<T> consulta)
    {
        var resultado = await ConsultarAsync(escenario, consulta, conSesion: true);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    /// <summary>Un tipo de visita de 60 minutos que no se reserva en línea, escrito directo en la base.</summary>
    private static async Task<TipoDeVisitaId> InsertarTipoAsync(Escenario escenario, string nombre, bool activo, int orden)
    {
        var id = TipoDeVisitaId.Nuevo();
        await escenario.EjecutarAsync(
            $"""
            insert into agenda.tipo_de_visita (id, nombre, descripcion, duracion_min, reservable_en_linea, orden, activo)
            values ('{id.Valor}', '{nombre}', null, 60, false, {orden}, {(activo ? "true" : "false")})
            """);
        return id;
    }

    private static Task CambiarEstadoAsync(Escenario escenario, DateOnly fecha, TimeOnly inicio, string estado, string? motivoDeCancelacion) =>
        escenario.EjecutarAsync(
            $"""
            update agenda.cita
            set estado = '{estado}',
                motivo_de_cancelacion = {(motivoDeCancelacion is null ? "null" : $"'{motivoDeCancelacion}'")},
                cancelada_en = {(estado == "Cancelada" ? "now()" : "null")}
            where inicio = '{HoraLocal.AUtc(fecha, inicio):O}'
            """);
}
