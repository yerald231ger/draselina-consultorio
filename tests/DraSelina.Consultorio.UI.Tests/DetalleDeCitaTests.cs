using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
// La página y la consulta se llaman igual, y la Agenda como el módulo: los alias evitan los choques.
using ConsultaDelDetalle = DraSelina.Consultorio.Agenda.Contracts.DetalleDeCita;
using PaginaAgenda = DraSelina.Consultorio.UI.Pages.Administracion.Agenda;
using PaginaDelDetalle = DraSelina.Consultorio.UI.Pages.Administracion.DetalleDeCita;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// El Detalle de Cita (entrada 6) con la consulta y los dos comandos falsos, en interactivo: los datos, las acciones por
/// estado, la confirmación en la página, el estado y la bitácora nuevos sin recargar a mano, el rechazo que vuelve a
/// pedir la consulta, sin correo, la Cita que no existe y el enlace desde la Agenda. "Sin sesión" está en las de
/// integración (la redirección la hace el host). Hoy es el lunes 9 de noviembre de 2026, 4:10 pm en Ciudad de México.
/// </summary>
public sealed class DetalleDeCitaTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 22, 10, 0, TimeSpan.Zero);

    private static readonly DateOnly Martes = new(2026, 11, 10);

    private static readonly CitaId Id = CitaId.Nuevo();

    private static readonly EntradaDeLaBitacora Agendada = new(
        new DateOnly(2026, 11, 2), new TimeOnly(12, 0), Actor.Paciente, AccionEnLaBitacora.Agendada, OrigenEnLaAgenda.EnLinea, null, null, null);

    private static readonly CitaDeLaAgenda CitaEnLaAgenda = new(
        Id, new TimeOnly(16, 0), new TimeOnly(16, 30), "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada, null, false, false, null);

    private readonly ConsultaFalsa _consulta = new();
    private readonly MarcarFalso _marcar = new();
    private readonly CancelarFalso _cancelar = new();

    public DetalleDeCitaTests()
    {
        Services.AddSingleton<IQueryHandler<ConsultaDelDetalle, CitaEnDetalle>>(_consulta);
        Services.AddSingleton<ICommandHandler<MarcarNoSePresento, CitaMarcadaNoSePresento>>(_marcar);
        Services.AddSingleton<ICommandHandler<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>>(_cancelar);
        Services.AddSingleton<IClock>(new Reloj(Ahora));
        Services.AddSingleton<IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>>(new DiasConUnaCita(Martes, CitaEnLaAgenda));
        Services.AddSingleton(_ => new RefrescoDeLaAgenda(TimeSpan.FromHours(1)));
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Una_Cita_futura_en_linea_muestra_sus_datos_su_estado_solo_Cancelar_y_su_bitacora()
    {
        _consulta.Responde = _ => Cita();

        var pagina = Abrir();

        Assert.Equal(new ConsultaDelDetalle(Id), Assert.Single(_consulta.Pedidas));
        Assert.Equal("Ana Sofía López", pagina.Find(".dsm-detalle__paciente").TextContent);
        Assert.Equal("Agendada", pagina.Find(".dsm-detalle__estado").TextContent);
        Assert.Equal("Martes 10 de noviembre, 4:00 pm a 4:30 pm", pagina.Find(".dsm-detalle__cuando").TextContent);
        Assert.Equal(
            [
                ("Teléfono", "444 123 4567"),
                ("Correo", "ana@correo.com"),
                ("Tipo de consulta", "Primera consulta"),
                ("Fecha", "Martes 10 de noviembre"),
                ("Hora", "4:00 pm a 4:30 pm"),
                ("Origen", "En línea"),
                ("Motivo de la consulta", "Dolor de rodilla"),
                ("Consentimiento", "En línea · 2 de noviembre de 2026, 12:00 pm · aviso 2026-11"),
            ],
            pagina.FindAll(".dsm-detalle__dato").Select(d => (d.QuerySelector("dt")!.TextContent, d.QuerySelector("dd")!.TextContent)));
        Assert.Equal("tel:+524441234567", pagina.Find("[data-dato=telefono] a").GetAttribute("href"));
        Assert.Equal([PaginaDelDetalle.CancelarCitaTexto], pagina.FindAll(".dsm-detalle__acciones button").Select(b => b.TextContent));
        Assert.Equal(
            [("Lun 2 nov · 12:00 pm", "Agendada · en línea · Paciente")],
            pagina.FindAll(".dsm-detalle__entrada").Select(e => (
                e.QuerySelector(".dsm-detalle__entrada-cuando")!.TextContent, e.QuerySelector(".dsm-detalle__entrada-que")!.TextContent)));
        var volver = pagina.Find(".dsm-volver");
        Assert.Equal("/admin?fecha=2026-11-10&vista=dia", volver.GetAttribute("href"));
        Assert.Contains(PaginaDelDetalle.VolverALaAgenda, volver.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_Cita_que_ya_empezo_se_ve_Atendida_y_solo_ofrece_No_se_presento()
    {
        _consulta.Responde = _ => Cita() with { Estado = EstadoEnLaAgenda.Atendida, PuedeMarcarNoSePresento = true, PuedeCancelar = false };

        var pagina = Abrir();

        Assert.Equal("Atendida", pagina.Find(".dsm-detalle__estado").TextContent);
        Assert.Equal([PaginaDelDetalle.MarcarNoSePresentoTexto], pagina.FindAll(".dsm-detalle__acciones button").Select(b => b.TextContent));
    }

    [Theory]
    [InlineData(EstadoEnLaAgenda.NoSePresento, null, "No se presentó", "dsm-chip-de-estado--neutro")]
    [InlineData(EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElPaciente, "Cancelada (por el paciente)", "dsm-chip-de-estado--neutro")]
    [InlineData(EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia, "Cancelada (tardía)", "dsm-chip-de-estado--tardia")]
    [InlineData(EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio, "Cancelada (por el consultorio)", "dsm-chip-de-estado--neutro")]
    public void En_otro_estado_no_hay_acciones_y_el_chip_lleva_los_colores_de_la_Agenda(
        EstadoEnLaAgenda estado, CancelacionEnLaAgenda? cancelacion, string etiqueta, string clase)
    {
        _consulta.Responde = _ => Cita() with { Estado = estado, Cancelacion = cancelacion, PuedeMarcarNoSePresento = false, PuedeCancelar = false };

        var pagina = Abrir();

        var chip = pagina.Find(".dsm-detalle__estado");
        Assert.Equal(etiqueta, chip.TextContent);
        Assert.Contains(clase, chip.ClassName, StringComparison.Ordinal);
        Assert.Empty(pagina.FindAll(".dsm-detalle__acciones"));
    }

    [Fact]
    public void Una_Cita_del_consultorio_dice_su_origen_y_su_nota()
    {
        _consulta.Responde = _ => Cita() with
        {
            Origen = OrigenEnLaAgenda.DelConsultorio,
            Nota = "lo pidió Selina",
            MotivoDeConsulta = null,
            Consentimiento = new ConsentimientoEnDetalle(CanalEnDetalle.Externo, new DateOnly(2026, 11, 9), new TimeOnly(10, 5), "2026-11"),
        };

        var pagina = Abrir();

        Assert.Equal("Del consultorio", pagina.Find("[data-dato=origen] dd").TextContent);
        Assert.Equal("lo pidió Selina", pagina.Find("[data-dato=nota] dd").TextContent);
        Assert.Empty(pagina.FindAll("[data-dato=motivo]"));
        Assert.StartsWith("Por fuera del sitio · ", pagina.Find("[data-dato=consentimiento] dd").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_correo_lo_dice_y_la_confirmacion_de_cancelar_lo_repite()
    {
        _consulta.Responde = _ => Cita() with { Paciente = new ContactoDelPaciente("Ana Sofía López", "+524441234567", null), SinCorreo = true };
        var pagina = Abrir();

        Assert.Equal(PaginaDelDetalle.SinCorreo, pagina.Find("[data-dato=correo] dd").TextContent);

        pagina.Find(".dsm-detalle__cancelar").Click();

        Assert.Equal(PaginaDelDetalle.AvisarleAMano, pagina.Find(".dsm-detalle__consecuencia").TextContent);
    }

    [Fact]
    public void Un_Paciente_que_Pacientes_no_conoce_se_muestra_sin_contacto()
    {
        _consulta.Responde = _ => Cita() with { Paciente = null, SinCorreo = true };

        var pagina = Abrir();

        Assert.Equal(PaginaAgenda.PacienteNoEncontrado, pagina.Find(".dsm-detalle__paciente").TextContent);
        Assert.Empty(pagina.FindAll("[data-dato=telefono]"));
        Assert.Equal(PaginaDelDetalle.SinCorreo, pagina.Find("[data-dato=correo] dd").TextContent);
    }

    [Fact]
    public void El_primer_clic_en_Cancelar_pide_confirmacion_sin_cambiar_nada_y_No_regresa()
    {
        _consulta.Responde = _ => Cita();
        var pagina = Abrir();

        pagina.Find(".dsm-detalle__cancelar").Click();

        Assert.Empty(_cancelar.Recibidos);
        Assert.Equal(PaginaDelDetalle.PreguntaDeCancelar, pagina.Find(".dsm-detalle__pregunta").TextContent);
        Assert.Equal(PaginaDelDetalle.RecibiraDisculpa, pagina.Find(".dsm-detalle__consecuencia").TextContent);
        Assert.Equal(PaginaDelDetalle.ConfirmarCancelar, pagina.Find(".dsm-detalle__confirmar").TextContent.Trim());
        Assert.Equal("Agendada", pagina.Find(".dsm-detalle__estado").TextContent);

        pagina.Find(".dsm-detalle__no-confirmar").Click();

        Assert.Empty(pagina.FindAll(".dsm-detalle__confirmacion"));
        Assert.Single(pagina.FindAll(".dsm-detalle__cancelar"));
        Assert.Empty(_cancelar.Recibidos);
        Assert.Single(_consulta.Pedidas);
    }

    [Fact]
    public void Confirmar_Cancelar_manda_el_comando_y_se_queda_con_el_estado_y_la_bitacora_nuevos()
    {
        var cancelada = false;
        _consulta.Responde = _ => cancelada
            ? Cita() with
            {
                Estado = EstadoEnLaAgenda.Cancelada,
                Cancelacion = CancelacionEnLaAgenda.PorElConsultorio,
                PuedeCancelar = false,
                Bitacora =
                [
                    Agendada,
                    new(new DateOnly(2026, 11, 9), new TimeOnly(16, 10), Actor.Administrador, AccionEnLaBitacora.Cancelada, null,
                        CancelacionEnLaAgenda.PorElConsultorio, null, null),
                ],
            }
            : Cita();
        _cancelar.Responde = c =>
        {
            cancelada = true;
            return Resultado.Exito(new CitaCanceladaPorConsultorio(c.CitaId!.Value, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio));
        };
        var pagina = Abrir();

        pagina.Find(".dsm-detalle__cancelar").Click();
        pagina.Find(".dsm-detalle__confirmar").Click();

        Assert.Equal(Id, Assert.Single(_cancelar.Recibidos).CitaId);
        Assert.Empty(_marcar.Recibidos);
        Assert.Equal(2, _consulta.Pedidas.Count);
        Assert.Equal("Cancelada (por el consultorio)", pagina.Find(".dsm-detalle__estado").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-detalle__acciones"));
        Assert.Empty(pagina.FindAll(".dsm-aviso"));
        Assert.Equal(
            ["Agendada · en línea · Paciente", "Cancelada · por el Consultorio · Administrador"],
            pagina.FindAll(".dsm-detalle__entrada-que").Select(e => e.TextContent));
        Assert.Equal("/admin?fecha=2026-11-10&vista=dia", pagina.Find(".dsm-volver").GetAttribute("href"));
    }

    [Fact]
    public void Confirmar_No_se_presento_manda_el_comando_y_se_queda_con_el_estado_y_la_bitacora_nuevos()
    {
        var marcada = false;
        _consulta.Responde = _ => marcada
            ? Cita() with
            {
                Estado = EstadoEnLaAgenda.NoSePresento,
                PuedeMarcarNoSePresento = false,
                PuedeCancelar = false,
                Bitacora =
                [
                    Agendada,
                    new(new DateOnly(2026, 11, 10), new TimeOnly(16, 15), Actor.Administrador, AccionEnLaBitacora.NoSePresento, null, null, null, null),
                ],
            }
            : Cita() with { Estado = EstadoEnLaAgenda.Atendida, PuedeMarcarNoSePresento = true, PuedeCancelar = false };
        _marcar.Responde = c =>
        {
            marcada = true;
            return Resultado.Exito(new CitaMarcadaNoSePresento(c.CitaId!.Value, EstadoEnLaAgenda.NoSePresento));
        };
        var pagina = Abrir();

        pagina.Find(".dsm-detalle__marcar").Click();
        Assert.Equal(PaginaDelDetalle.PreguntaDeNoSePresento, pagina.Find(".dsm-detalle__pregunta").TextContent);
        Assert.Equal(PaginaDelDetalle.NoSeDeshace, pagina.Find(".dsm-detalle__consecuencia").TextContent);
        Assert.Empty(_marcar.Recibidos);
        pagina.Find(".dsm-detalle__confirmar").Click();

        Assert.Equal(Id, Assert.Single(_marcar.Recibidos).CitaId);
        Assert.Equal("No se presentó", pagina.Find(".dsm-detalle__estado").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-detalle__acciones"));
        Assert.Equal(
            ["Agendada · en línea · Paciente", "No se presentó · Administrador"],
            pagina.FindAll(".dsm-detalle__entrada-que").Select(e => e.TextContent));
    }

    [Fact]
    public void Un_rechazo_se_muestra_y_la_pagina_vuelve_a_pedir_la_consulta_que_ofrece_No_se_presento()
    {
        var vueltas = 0;
        _consulta.Responde = _ => ++vueltas == 1
            ? Cita()
            : Cita() with { Estado = EstadoEnLaAgenda.Atendida, PuedeMarcarNoSePresento = true, PuedeCancelar = false };
        _cancelar.Responde = _ => Resultado.Fallo(CodigosDeAgenda.YaEmpezo).Como<CitaCanceladaPorConsultorio>();
        var pagina = Abrir();

        pagina.Find(".dsm-detalle__cancelar").Click();
        pagina.Find(".dsm-detalle__confirmar").Click();

        Assert.Equal(PaginaDelDetalle.YaEmpezo, pagina.Find(".dsm-aviso__mensaje").TextContent);
        Assert.Equal(2, _consulta.Pedidas.Count);
        Assert.Equal("Atendida", pagina.Find(".dsm-detalle__estado").TextContent);
        Assert.Equal([PaginaDelDetalle.MarcarNoSePresentoTexto], pagina.FindAll(".dsm-detalle__acciones button").Select(b => b.TextContent));
    }

    [Theory]
    [InlineData(CodigosDeAgenda.CitaNoAgendada, PaginaDelDetalle.YaNoEstaAgendada)]
    [InlineData(CodigosDeAgenda.AunNoEmpieza, PaginaDelDetalle.AunNoEmpieza)]
    [InlineData("agenda.otro", PaginaDelDetalle.NoSeHizo)]
    public void Cada_rechazo_de_No_se_presento_tiene_su_mensaje(string codigo, string mensaje)
    {
        _consulta.Responde = _ => Cita() with { Estado = EstadoEnLaAgenda.Atendida, PuedeMarcarNoSePresento = true, PuedeCancelar = false };
        _marcar.Responde = _ => Resultado.Fallo(codigo).Como<CitaMarcadaNoSePresento>();
        var pagina = Abrir();

        pagina.Find(".dsm-detalle__marcar").Click();
        pagina.Find(".dsm-detalle__confirmar").Click();

        Assert.Equal(mensaje, pagina.Find(".dsm-aviso__mensaje").TextContent);
        Assert.Equal(2, _consulta.Pedidas.Count);
    }

    [Fact]
    public void Sin_sesion_al_confirmar_lo_dice_y_conserva_lo_mostrado()
    {
        var vueltas = 0;
        _consulta.Responde = _ => ++vueltas == 1 ? Cita() : null;
        _cancelar.Responde = _ => Resultado.NoAutorizado().Como<CitaCanceladaPorConsultorio>();
        var pagina = Abrir();

        pagina.Find(".dsm-detalle__cancelar").Click();
        pagina.Find(".dsm-detalle__confirmar").Click();

        Assert.Equal(PaginaDelDetalle.SinSesion, pagina.Find(".dsm-aviso__mensaje").TextContent);
        Assert.Equal("Ana Sofía López", pagina.Find(".dsm-detalle__paciente").TextContent);
    }

    [Fact]
    public void Mientras_el_comando_corre_el_boton_se_deshabilita_y_un_segundo_clic_no_manda_otro()
    {
        var detenido = new TaskCompletionSource<Resultado<CitaCanceladaPorConsultorio>>();
        _consulta.Responde = _ => Cita();
        _cancelar.RespondeAsync = _ => detenido.Task;
        var pagina = Abrir();
        pagina.Find(".dsm-detalle__cancelar").Click();

        pagina.Find(".dsm-detalle__confirmar").Click();

        var confirmar = pagina.Find(".dsm-detalle__confirmar");
        Assert.True(confirmar.HasAttribute("disabled"));
        Assert.Equal("Guardando…", confirmar.TextContent.Trim());
        Assert.True(pagina.Find(".dsm-detalle__no-confirmar").HasAttribute("disabled"));
        pagina.Find(".dsm-detalle__confirmar").Click();
        Assert.Single(_cancelar.Recibidos);

        pagina.InvokeAsync(() => detenido.SetResult(
            Resultado.Exito(new CitaCanceladaPorConsultorio(Id, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio))));
        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll(".dsm-detalle__confirmacion")));
        Assert.Single(_cancelar.Recibidos);
    }

    [Fact]
    public void Una_Cita_que_no_existe_lo_dice_con_el_enlace_a_la_Agenda()
    {
        _consulta.Responde = _ => null;
        _consulta.Codigo = CodigosDeAgenda.CitaNoEncontrada;

        var pagina = Abrir();

        Assert.Equal(PaginaDelDetalle.NoExiste, pagina.Find("h1").TextContent);
        Assert.Equal("/admin", pagina.Find(".dsm-volver").GetAttribute("href"));
        Assert.Empty(pagina.FindAll(".dsm-detalle__datos"));
    }

    [Fact]
    public void Si_la_consulta_no_vuelve_con_exito_lo_dice()
    {
        _consulta.Responde = _ => null;

        var pagina = Abrir();

        Assert.Equal(PaginaDelDetalle.NoSeCargo, pagina.Find(".dsm-detalle__no-cargo").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-detalle__datos"));
    }

    [Fact]
    public void Una_carga_fallida_no_cuenta_como_cargada_y_se_vuelve_a_pedir_y_una_exitosa_no()
    {
        // bUnit no pasa [PersistentState]: un fallo cargado (como el del prerenderizado) y una nueva pasada de parámetros.
        var pagina = Abrir();
        Assert.Equal(PaginaDelDetalle.NoSeCargo, pagina.Find(".dsm-detalle__no-cargo").TextContent);
        Assert.True(pagina.Instance.Cargado!.Fallo);
        _consulta.Responde = _ => Cita();

        pagina.Render(p => p.Add(x => x.Id, Id.Valor));

        Assert.Equal(2, _consulta.Pedidas.Count);
        Assert.Equal("Ana Sofía López", pagina.Find(".dsm-detalle__paciente").TextContent);

        pagina.Render(p => p.Add(x => x.Id, Id.Valor));

        Assert.Equal(2, _consulta.Pedidas.Count);
    }

    [Fact]
    public void Las_fechas_de_la_bitacora_de_otro_anio_llevan_su_anio()
    {
        var anterior = new EntradaDeLaBitacora(
            new DateOnly(2025, 11, 3), new TimeOnly(12, 0), Actor.Paciente, AccionEnLaBitacora.Agendada, OrigenEnLaAgenda.EnLinea, null, null, null);
        var reprogramada = new EntradaDeLaBitacora(
            new DateOnly(2026, 1, 5), new TimeOnly(9, 30), Actor.Paciente, AccionEnLaBitacora.Reprogramada, null, null,
            new MomentoLocal(new DateOnly(2025, 11, 4), new TimeOnly(16, 0)), new MomentoLocal(Martes, new TimeOnly(16, 0)));
        _consulta.Responde = _ => Cita() with { Bitacora = [anterior, reprogramada] };

        var pagina = Abrir();

        Assert.Equal(
            [
                ("Lun 3 nov 2025 · 12:00 pm", "Agendada · en línea · Paciente"),
                ("Lun 5 ene · 9:30 am", "Reprogramada de 4 de noviembre de 2025, 4:00 pm a 10 de noviembre, 4:00 pm · Paciente"),
            ],
            pagina.FindAll(".dsm-detalle__entrada").Select(e => (
                e.QuerySelector(".dsm-detalle__entrada-cuando")!.TextContent, e.QuerySelector(".dsm-detalle__entrada-que")!.TextContent)));
    }

    [Fact]
    public void La_direccion_lleva_solo_el_id_de_la_Cita()
    {
        Assert.Equal($"/admin/citas/{Id.Valor}", PaginaDelDetalle.Direccion(Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Desde_la_Agenda_cada_tarjeta_abre_su_Detalle_en_vista_de_dia_y_de_semana(bool semana)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(PaginaAgenda.Direccion(Martes, semana));

        var agenda = Render<PaginaAgenda>();

        var enlace = agenda.Find($"[data-cita='{Id}'] a.dsm-agenda__abrir");
        Assert.Equal($"/admin/citas/{Id.Valor}", enlace.GetAttribute("href"));
        Assert.Equal("Ana Sofía López", enlace.TextContent);
    }

    private IRenderedComponent<PaginaDelDetalle> Abrir() => Render<PaginaDelDetalle>(p => p.Add(x => x.Id, Id.Valor));

    private static CitaEnDetalle Cita() => new(
        Id,
        new ContactoDelPaciente("Ana Sofía López", "+524441234567", "ana@correo.com"),
        "Primera consulta",
        Martes,
        new TimeOnly(16, 0),
        new TimeOnly(16, 30),
        OrigenEnLaAgenda.EnLinea,
        null,
        "Dolor de rodilla",
        new ConsentimientoEnDetalle(CanalEnDetalle.EnLinea, new DateOnly(2026, 11, 2), new TimeOnly(12, 0), "2026-11"),
        EstadoEnLaAgenda.Agendada,
        null,
        SinCorreo: false,
        PuedeMarcarNoSePresento: false,
        PuedeCancelar: true,
        [Agendada]);

    private sealed class ConsultaFalsa : IQueryHandler<ConsultaDelDetalle, CitaEnDetalle>
    {
        /// <summary>El Detalle que devuelve; nulo, un fallo con <see cref="Codigo"/> o no autorizado si no hay código.</summary>
        public Func<ConsultaDelDetalle, CitaEnDetalle?> Responde { get; set; } = _ => null;

        public string? Codigo { get; set; }

        public List<ConsultaDelDetalle> Pedidas { get; } = [];

        public Task<Resultado<CitaEnDetalle>> HandleAsync(ConsultaDelDetalle consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            var detalle = Responde(consulta);
            return Task.FromResult(
                detalle is not null ? Resultado.Exito(detalle)
                : Codigo is not null ? Resultado.Fallo(Codigo).Como<CitaEnDetalle>()
                : Resultado.NoAutorizado().Como<CitaEnDetalle>());
        }
    }

    private sealed class MarcarFalso : ICommandHandler<MarcarNoSePresento, CitaMarcadaNoSePresento>
    {
        public Func<MarcarNoSePresento, Resultado<CitaMarcadaNoSePresento>> Responde { get; set; } =
            c => Resultado.Exito(new CitaMarcadaNoSePresento(c.CitaId!.Value, EstadoEnLaAgenda.NoSePresento));

        public List<MarcarNoSePresento> Recibidos { get; } = [];

        public Task<Resultado<CitaMarcadaNoSePresento>> HandleAsync(MarcarNoSePresento comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return Task.FromResult(Responde(comando));
        }
    }

    private sealed class CancelarFalso : ICommandHandler<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>
    {
        public Func<CancelarCitaPorConsultorio, Resultado<CitaCanceladaPorConsultorio>> Responde { get; set; } =
            c => Resultado.Exito(new CitaCanceladaPorConsultorio(c.CitaId!.Value, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio));

        public Func<CancelarCitaPorConsultorio, Task<Resultado<CitaCanceladaPorConsultorio>>>? RespondeAsync { get; set; }

        public List<CancelarCitaPorConsultorio> Recibidos { get; } = [];

        public Task<Resultado<CitaCanceladaPorConsultorio>> HandleAsync(CancelarCitaPorConsultorio comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return RespondeAsync?.Invoke(comando) ?? Task.FromResult(Responde(comando));
        }
    }

    private sealed class DiasConUnaCita(DateOnly fecha, CitaDeLaAgenda cita) : IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>
    {
        public Task<Resultado<IReadOnlyList<DiaDeLaAgenda>>> HandleAsync(DiasDeLaAgenda consulta, CancellationToken cancellationToken)
        {
            IReadOnlyList<DiaDeLaAgenda> dias =
            [
                .. Enumerable.Range(0, consulta.Hasta.DayNumber - consulta.Desde.DayNumber + 1)
                    .Select(i => consulta.Desde.AddDays(i))
                    .Select(d => new DiaDeLaAgenda(d, TipoDeDia.Normal, null, [], d == fecha ? [cita] : [])),
            ];
            return Task.FromResult(Resultado.Exito(dias));
        }
    }

    private sealed class Reloj(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow { get; } = ahora;
    }
}
