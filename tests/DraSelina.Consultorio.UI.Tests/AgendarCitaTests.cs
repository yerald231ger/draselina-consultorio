using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
// La página de la Agenda se llama como el módulo: el alias evita el choque con el namespace DraSelina.Consultorio.Agenda.
using PaginaAgenda = DraSelina.Consultorio.UI.Pages.Administracion.Agenda;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// La pantalla "Agendar cita" (entrada 5.4) con el comando y las consultas falsas, en interactivo: una prueba por fila
/// de la matriz del plan, salvo "Sin sesión" (la redirección la hace el host; está en las de integración). Hoy es el
/// lunes 9 de noviembre de 2026, 4:10 pm en Ciudad de México.
/// </summary>
public sealed class AgendarCitaTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 22, 10, 0, TimeSpan.Zero);

    private static readonly DateOnly Martes = new(2026, 11, 10);

    private static readonly DateOnly Sabado = new(2026, 11, 14);

    private static readonly TipoDeVisitaActivo Primera = new(TipoDeVisitaId.Nuevo(), "Primera consulta", 30, ReservableEnLinea: true);

    private static readonly TipoDeVisitaActivo Cirugia = new(TipoDeVisitaId.Nuevo(), "Cirugía menor", 60, ReservableEnLinea: false);

    private readonly TiposFalsos _tipos = new();
    private readonly ReconocerFalso _reconocer = new();
    private readonly HorarioFalso _horario = new();
    private readonly AgendarFalso _agendar = new();

    public AgendarCitaTests()
    {
        Services.AddSingleton<IQueryHandler<TiposDeVisitaActivos, IReadOnlyList<TipoDeVisitaActivo>>>(_tipos);
        Services.AddSingleton<IQueryHandler<PacienteReconocido, PacienteDeLaCita>>(_reconocer);
        Services.AddSingleton<IQueryHandler<HorarioDeLaCitaDelConsultorio, HorarioDeLaCita>>(_horario);
        Services.AddSingleton<ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>>(_agendar);
        Services.AddSingleton<IClock>(new Reloj(Ahora));
        Services.AddSingleton(_ => new RefrescoDeLaAgenda(TimeSpan.FromHours(1)));
        Services.AddSingleton<IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>>(new DiasVacios());
        SetRendererInfo(Interactivo);
    }

    private NavigationManager Navegacion => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void La_Agenda_lleva_Agendar_cita_con_la_fecha_que_se_esta_viendo()
    {
        Navegacion.NavigateTo("/admin?fecha=2026-11-10&vista=semana");

        var agenda = Render<PaginaAgenda>();

        var boton = agenda.Find(".dsm-agenda__agendar");
        Assert.Equal("Agendar cita", boton.TextContent);
        Assert.Equal("/admin/agendar?fecha=2026-11-10", boton.GetAttribute("href"));
    }

    [Fact]
    public void Abierta_desde_la_Agenda_trae_la_fecha_puesta_y_los_tipos_activos_en_su_orden()
    {
        var pagina = Abrir(Martes);

        Assert.Equal("2026-11-10", pagina.Find("#Fecha").GetAttribute("value"));
        Assert.Equal(
            ["Elige el tipo de consulta", "Primera consulta · 30 min", "Cirugía menor · 60 min"],
            pagina.FindAll("#TipoDeVisitaId option").Select(o => o.TextContent));
        Assert.Equal("/admin?fecha=2026-11-10&vista=dia", pagina.Find(".dsm-volver").GetAttribute("href"));
        Assert.Single(_tipos.Pedidas);
        Assert.Empty(pagina.FindAll(".dsm-paciente-reconocido"));
        Assert.Empty(pagina.FindAll(".dsm-aviso-de-horario"));
    }

    [Fact]
    public void Sin_fecha_en_la_direccion_es_hoy()
    {
        Navegacion.NavigateTo("/admin/agendar?fecha=hola");

        var pagina = Render<AgendarCita>();

        Assert.Equal("2026-11-09", pagina.Find("#Fecha").GetAttribute("value"));
    }

    [Fact]
    public void Un_Paciente_conocido_dice_que_ya_existe_con_su_proxima_y_su_anterior()
    {
        _reconocer.Responde = _ => new PacienteDeLaCita(
            true,
            new CitaDelPaciente(new DateOnly(2026, 11, 17), new TimeOnly(16, 0), "Primera consulta", EstadoEnLaAgenda.Agendada, null),
            new CitaDelPaciente(new DateOnly(2026, 11, 2), new TimeOnly(17, 30), "Primera consulta", EstadoEnLaAgenda.Atendida, null));
        var pagina = Abrir(Martes);

        Paciente(pagina, "444 123 4567", "Ana Sofía López");

        Assert.Equal(new PacienteReconocido("Ana Sofía López", "444 123 4567"), _reconocer.Pedidas[^1]);
        Assert.Equal(AgendarCita.YaExiste, pagina.Find(".dsm-paciente-reconocido__titulo").TextContent);
        Assert.Equal(
            "Próxima: Martes 17 de noviembre, 4:00 pm · Primera consulta",
            pagina.Find(".dsm-paciente-reconocido__cita--proxima").TextContent);
        Assert.Equal(
            "Anterior: Lunes 2 de noviembre, 5:30 pm · Primera consulta · Atendida",
            pagina.Find(".dsm-paciente-reconocido__cita--anterior").TextContent);
    }

    [Fact]
    public void Solo_con_celular_y_nombre_se_pregunta_por_el_Paciente()
    {
        var pagina = Abrir(Martes);

        pagina.Find("#Telefono").Change("4441234567");

        Assert.Empty(_reconocer.Pedidas);
    }

    [Fact]
    public void Conocido_sin_Citas_futuras_solo_muestra_la_anterior_con_su_cancelacion()
    {
        _reconocer.Responde = _ => new PacienteDeLaCita(
            true,
            null,
            new CitaDelPaciente(new DateOnly(2025, 10, 6), new TimeOnly(16, 0), "Primera consulta", EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia));
        var pagina = Abrir(Martes);

        Paciente(pagina, "4441234567", "Ana Sofía López");

        Assert.Equal(AgendarCita.YaExiste, pagina.Find(".dsm-paciente-reconocido__titulo").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-paciente-reconocido__cita--proxima"));
        Assert.Equal(
            "Anterior: Lunes 6 de octubre de 2025, 4:00 pm · Primera consulta · Cancelada (tardía)",
            pagina.Find(".dsm-paciente-reconocido__cita--anterior").TextContent);
    }

    [Fact]
    public void Un_Paciente_nuevo_o_con_el_mismo_celular_y_otro_nombre_no_lleva_aviso()
    {
        _reconocer.Responde = c => c.Nombre == "Ana Sofía López"
            ? new PacienteDeLaCita(true, null, null)
            : PacienteDeLaCita.Desconocido;
        var pagina = Abrir(Martes);

        Paciente(pagina, "4441234567", "Ana Sofía López");
        Assert.Single(pagina.FindAll(".dsm-paciente-reconocido"));

        // RN-25: el mismo celular con otro nombre es otro Paciente; el aviso anterior se va.
        pagina.Find("#Nombre").Change("Bruno Díaz");

        Assert.Empty(pagina.FindAll(".dsm-paciente-reconocido"));
    }

    [Fact]
    public void Dentro_de_horario_no_hay_aviso_y_la_nota_es_opcional()
    {
        var pagina = Abrir(Martes);

        Cuando(pagina, Primera, "16:00");

        Assert.Equal(new HorarioDeLaCitaDelConsultorio(Primera.Id, Martes, new TimeOnly(16, 0)), _horario.Pedidas[^1]);
        Assert.Empty(pagina.FindAll(".dsm-aviso-de-horario"));
        Assert.Equal("Nota (opcional)", pagina.Find("label[for=Nota]").TextContent);
        Assert.Null(pagina.Find("#Nota").GetAttribute("aria-required"));
    }

    [Fact]
    public void Fuera_de_horario_avisa_pide_la_nota_y_sin_ella_el_error_va_en_la_Nota()
    {
        _horario.Responde = c => c.Fecha == Sabado ? new HorarioDeLaCita(true, DiaSinAtencion: true) : new HorarioDeLaCita(false, false);
        _agendar.Responde = c => string.IsNullOrWhiteSpace(c.Nota)
            ? Resultado.Fallo(CodigosDeAgenda.NotaObligatoria).Como<CitaAgendadaPorConsultorio>()
            : Agendada(c);
        var pagina = Abrir(Sabado);
        Llenar(pagina);

        Cuando(pagina, Primera, "10:15");

        Assert.Equal(AgendarCita.DiaSinAtencion, pagina.Find(".dsm-aviso-de-horario").TextContent.Trim());
        Assert.Equal("Nota (obligatoria)", pagina.Find("label[for=Nota]").TextContent);
        Assert.Equal("true", pagina.Find("#Nota").GetAttribute("aria-required"));

        pagina.Find("form").Submit();

        Assert.Equal(AgendarCita.NotaObligatoria, pagina.Find("#Nota-error-1").TextContent);
        Assert.Equal("true", pagina.Find("#Nota").GetAttribute("aria-invalid"));
        Assert.True(pagina.Find("#Nota").HasAttribute("autofocus"));
        Assert.Equal(["Nota"], pagina.FindAll("[autofocus]").Select(e => e.Id));
        JSInterop.VerifyFocusAsyncInvoke(calledTimes: 1);
        Assert.EndsWith("/admin/agendar?fecha=2026-11-14", Navegacion.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void El_aviso_de_horario_se_actualiza_al_cambiar_tipo_fecha_u_hora_sin_recargar()
    {
        // Dentro de horario solo la de 30 min a las 4:00 pm de un martes.
        _horario.Responde = c => new HorarioDeLaCita(
            !(c.TipoDeVisitaId == Primera.Id && c.Fecha == Martes && c.Hora == new TimeOnly(16, 0)), DiaSinAtencion: false);
        var pagina = Abrir(Martes);

        Cuando(pagina, Primera, "16:00");
        Assert.Empty(pagina.FindAll(".dsm-aviso-de-horario"));

        pagina.Find("#TipoDeVisitaId").Change(Cirugia.Id.ToString());
        Assert.Equal(AgendarCita.FueraDeHorario, pagina.Find(".dsm-aviso-de-horario").TextContent.Trim());

        pagina.Find("#TipoDeVisitaId").Change(Primera.Id.ToString());
        Assert.Empty(pagina.FindAll(".dsm-aviso-de-horario"));

        pagina.Find("#Hora").Change("17:45");
        Assert.Single(pagina.FindAll(".dsm-aviso-de-horario"));

        pagina.Find("#Hora").Change("16:00");
        pagina.Find("#Fecha").Change("2026-11-11");
        Assert.Single(pagina.FindAll(".dsm-aviso-de-horario"));
        Assert.Equal(new HorarioDeLaCitaDelConsultorio(Primera.Id, new DateOnly(2026, 11, 11), new TimeOnly(16, 0)), _horario.Pedidas[^1]);
    }

    [Fact]
    public void Guardar_fuera_de_horario_con_nota_vuelve_a_la_Agenda_del_sabado_en_vista_de_dia()
    {
        _horario.Responde = _ => new HorarioDeLaCita(true, DiaSinAtencion: true);
        var pagina = Abrir(Sabado);
        Llenar(pagina);
        Cuando(pagina, Primera, "10:15");
        pagina.Find("#Nota").Change("lo pidió Selina, no puede entre semana");

        pagina.Find("form").Submit();

        var comando = Assert.Single(_agendar.Recibidos);
        Assert.Equal(Primera.Id, comando.TipoDeVisitaId);
        Assert.Equal(Sabado, comando.Fecha);
        Assert.Equal(new TimeOnly(10, 15), comando.Hora);
        Assert.Equal("Ana Sofía López", comando.Nombre);
        Assert.Equal("4441234567", comando.Telefono);
        Assert.Equal("lo pidió Selina, no puede entre semana", comando.Nota);
        Assert.True(comando.Consentimiento);
        Assert.EndsWith("/admin?fecha=2026-11-14&vista=dia", Navegacion.Uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CodigosDeAgenda.HorarioOcupado, AgendarCita.HorarioOcupado)]
    [InlineData(CodigosDeAgenda.EnElPasado, AgendarCita.HoraPasada)]
    public void Un_traslape_o_una_hora_pasada_lo_dicen_y_conservan_lo_capturado(string codigo, string mensaje)
    {
        _agendar.Responde = _ => Resultado.Fallo(codigo).Como<CitaAgendadaPorConsultorio>();
        var pagina = Abrir(Martes);
        Llenar(pagina);
        Cuando(pagina, Primera, "16:15");
        pagina.Find("#Motivo").Change("Dolor de rodilla");

        pagina.Find("form").Submit();

        Assert.Equal(mensaje, pagina.Find(".dsm-aviso .dsm-aviso__mensaje").TextContent);
        Assert.Equal("Ana Sofía López", pagina.Find("#Nombre").GetAttribute("value"));
        Assert.Equal("4441234567", pagina.Find("#Telefono").GetAttribute("value"));
        Assert.Equal("16:15:00", pagina.Find("#Hora").GetAttribute("value"));
        Assert.Equal("Dolor de rodilla", pagina.Find("#Motivo").GetAttribute("value") ?? pagina.Find("#Motivo").TextContent);
        Assert.EndsWith("/admin/agendar?fecha=2026-11-10", Navegacion.Uri, StringComparison.Ordinal);

        // Otro intento con éxito limpia el aviso.
        _agendar.Responde = Agendada;
        pagina.Find("#Hora").Change("17:00");
        pagina.Find("form").Submit();
        Assert.EndsWith("/admin?fecha=2026-11-10&vista=dia", Navegacion.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_consentimiento_el_error_va_en_la_casilla()
    {
        _agendar.Responde = c => c.Consentimiento
            ? Agendada(c)
            : Resultado.Invalido(nameof(AgendarCitaPorConsultorio.Consentimiento), AgendarCitaPorConsultorio.MensajeDeConsentimiento)
                .Como<CitaAgendadaPorConsultorio>();
        var pagina = Abrir(Martes);
        Llenar(pagina, consentimiento: false);
        Cuando(pagina, Primera, "16:00");

        pagina.Find("form").Submit();

        Assert.Equal(AgendarCitaPorConsultorio.MensajeDeConsentimiento, pagina.Find("#Consentimiento-error-1").TextContent);
        Assert.Equal("true", pagina.Find("#Consentimiento").GetAttribute("aria-invalid"));
        Assert.Equal(["Consentimiento"], pagina.FindAll("[autofocus]").Select(e => e.Id));
        JSInterop.VerifyFocusAsyncInvoke(calledTimes: 1);
        Assert.Empty(pagina.FindAll(".dsm-aviso"));
    }

    [Fact]
    public void Un_celular_de_8_digitos_lleva_el_mensaje_de_la_reserva_en_su_campo()
    {
        _agendar.Responde = _ => Resultado.Invalido(nameof(AgendarCitaPorConsultorio.Telefono), AgendarCitaPorConsultorio.MensajeDeTelefono)
            .Como<CitaAgendadaPorConsultorio>();
        var pagina = Abrir(Martes);
        Llenar(pagina, telefono: "44412345");
        Cuando(pagina, Primera, "16:00");

        pagina.Find("form").Submit();

        Assert.Equal(AgendarCitaPorConsultorio.MensajeDeTelefono, pagina.Find("#Telefono-error-1").TextContent);
        Assert.Equal("true", pagina.Find("#Telefono").GetAttribute("aria-invalid"));
        JSInterop.VerifyFocusAsyncInvoke(calledTimes: 1);
        Assert.Empty(pagina.FindAll("[autofocus]"));
    }

    [Fact]
    public void Dos_clics_en_guardar_mandan_un_solo_comando_y_el_boton_se_deshabilita_mientras_corre()
    {
        var detenido = new TaskCompletionSource<Resultado<CitaAgendadaPorConsultorio>>();
        _agendar.RespondeAsync = _ => detenido.Task;
        var pagina = Abrir(Martes);
        Llenar(pagina);
        Cuando(pagina, Primera, "16:00");

        pagina.Find("form").Submit();
        Assert.True(pagina.Find("button[type=submit]").HasAttribute("disabled"));
        pagina.Find("form").Submit();

        detenido.SetResult(Resultado.Fallo(CodigosDeAgenda.HorarioOcupado).Como<CitaAgendadaPorConsultorio>());

        pagina.WaitForAssertion(() => Assert.False(pagina.Find("button[type=submit]").HasAttribute("disabled")));
        Assert.Single(_agendar.Recibidos);
    }

    [Fact]
    public void Ni_la_direccion_ni_las_consultas_llevan_datos_personales_al_convertirse_en_texto()
    {
        _reconocer.Responde = _ => new PacienteDeLaCita(true, null, null);
        var pagina = Abrir(Martes);

        Paciente(pagina, "4441234567", "Ana Sofía López");

        Assert.DoesNotContain("Ana", Navegacion.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("4441234567", Navegacion.Uri, StringComparison.Ordinal);
        var texto = _reconocer.Pedidas[^1].ToString();
        Assert.DoesNotContain("Ana", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("4441234567", texto, StringComparison.Ordinal);
    }

    private IRenderedComponent<AgendarCita> Abrir(DateOnly fecha)
    {
        Navegacion.NavigateTo(AgendarCita.Direccion(fecha));
        return Render<AgendarCita>();
    }

    private static void Paciente(IRenderedComponent<AgendarCita> pagina, string telefono, string nombre)
    {
        pagina.Find("#Telefono").Change(telefono);
        pagina.Find("#Nombre").Change(nombre);
    }

    private static void Llenar(IRenderedComponent<AgendarCita> pagina, string telefono = "4441234567", bool consentimiento = true)
    {
        Paciente(pagina, telefono, "Ana Sofía López");
        if (consentimiento)
        {
            pagina.Find("#Consentimiento").Change(true);
        }
    }

    private static void Cuando(IRenderedComponent<AgendarCita> pagina, TipoDeVisitaActivo tipo, string hora)
    {
        pagina.Find("#TipoDeVisitaId").Change(tipo.Id.ToString());
        pagina.Find("#Hora").Change(hora);
    }

    private static Resultado<CitaAgendadaPorConsultorio> Agendada(AgendarCitaPorConsultorio comando) =>
        Resultado.Exito(new CitaAgendadaPorConsultorio(CitaId.Nuevo(), comando.Fecha!.Value, comando.Hora!.Value, FueraDeHorario: false));

    private sealed class TiposFalsos : IQueryHandler<TiposDeVisitaActivos, IReadOnlyList<TipoDeVisitaActivo>>
    {
        public List<TiposDeVisitaActivos> Pedidas { get; } = [];

        public Task<Resultado<IReadOnlyList<TipoDeVisitaActivo>>> HandleAsync(TiposDeVisitaActivos consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            IReadOnlyList<TipoDeVisitaActivo> tipos = [Primera, Cirugia];
            return Task.FromResult(Resultado.Exito(tipos));
        }
    }

    private sealed class ReconocerFalso : IQueryHandler<PacienteReconocido, PacienteDeLaCita>
    {
        public Func<PacienteReconocido, PacienteDeLaCita> Responde { get; set; } = _ => PacienteDeLaCita.Desconocido;

        public List<PacienteReconocido> Pedidas { get; } = [];

        public Task<Resultado<PacienteDeLaCita>> HandleAsync(PacienteReconocido consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            return Task.FromResult(Resultado.Exito(Responde(consulta)));
        }
    }

    private sealed class HorarioFalso : IQueryHandler<HorarioDeLaCitaDelConsultorio, HorarioDeLaCita>
    {
        public Func<HorarioDeLaCitaDelConsultorio, HorarioDeLaCita> Responde { get; set; } = _ => new HorarioDeLaCita(false, false);

        public List<HorarioDeLaCitaDelConsultorio> Pedidas { get; } = [];

        public Task<Resultado<HorarioDeLaCita>> HandleAsync(HorarioDeLaCitaDelConsultorio consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            return Task.FromResult(Resultado.Exito(Responde(consulta)));
        }
    }

    private sealed class AgendarFalso : ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>
    {
        public Func<AgendarCitaPorConsultorio, Resultado<CitaAgendadaPorConsultorio>> Responde { get; set; } = Agendada;

        public Func<AgendarCitaPorConsultorio, Task<Resultado<CitaAgendadaPorConsultorio>>>? RespondeAsync { get; set; }

        public List<AgendarCitaPorConsultorio> Recibidos { get; } = [];

        public Task<Resultado<CitaAgendadaPorConsultorio>> HandleAsync(AgendarCitaPorConsultorio comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return RespondeAsync?.Invoke(comando) ?? Task.FromResult(Responde(comando));
        }
    }

    private sealed class DiasVacios : IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>
    {
        public Task<Resultado<IReadOnlyList<DiaDeLaAgenda>>> HandleAsync(DiasDeLaAgenda consulta, CancellationToken cancellationToken)
        {
            IReadOnlyList<DiaDeLaAgenda> dias =
            [
                .. Enumerable.Range(0, consulta.Hasta.DayNumber - consulta.Desde.DayNumber + 1)
                    .Select(i => new DiaDeLaAgenda(consulta.Desde.AddDays(i), TipoDeDia.Normal, null, [], [])),
            ];
            return Task.FromResult(Resultado.Exito(dias));
        }
    }

    private sealed class Reloj(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow { get; } = ahora;
    }
}
