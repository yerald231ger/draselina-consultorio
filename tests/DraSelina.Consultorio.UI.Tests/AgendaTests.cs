using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
// La página se llama como el módulo: el alias evita el choque con el namespace DraSelina.Consultorio.Agenda.
using PaginaAgenda = DraSelina.Consultorio.UI.Pages.Administracion.Agenda;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// La Agenda del Administrador (entradas 5.1 y 5.2) con una consulta falsa: los estados en orden, con nombre y motivo;
/// el día vacío; el Paciente que no existe; la navegación por la dirección; la semana de lunes a domingo; las marcas;
/// el filtro de canceladas y el refresco. Hoy es la fecha local del reloj de la aplicación.
/// </summary>
public sealed class AgendaTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    /// <summary>Lunes 9 de noviembre de 2026, 4:10 pm en Ciudad de México (UTC-6).</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 22, 10, 0, TimeSpan.Zero);

    private static readonly DateOnly Hoy = new(2026, 11, 9);

    private readonly ConsultaFalsa _consulta = new();

    private readonly RelojMovible _reloj = new(Ahora);

    // Lo bastante largo para que ninguna prueba que no lo busca vea una vuelta; las del refresco lo acortan.
    private TimeSpan _intervalo = TimeSpan.FromHours(1);

    public AgendaTests()
    {
        Services.AddSingleton<IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>>(_consulta);
        Services.AddSingleton<IClock>(_reloj);
        Services.AddSingleton(_ => new RefrescoDeLaAgenda(_intervalo));
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Sin_parametros_pide_hoy_en_vista_de_dia()
    {
        var agenda = Render<PaginaAgenda>();

        Assert.Equal(DiasDeLaAgenda.DelDia(Hoy), Assert.Single(_consulta.Pedidas));
        Assert.Contains("Hoy, Lunes 9 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent, StringComparison.Ordinal);
        Assert.Equal(
            ["/admin?fecha=2026-11-08&vista=dia", "/admin?fecha=2026-11-09&vista=dia", "/admin?fecha=2026-11-10&vista=dia"],
            agenda.FindAll(".dsm-agenda__navegacion a").Select(a => a.GetAttribute("href")));
        Assert.Equal(["Anterior", "Hoy", "Siguiente"], agenda.FindAll(".dsm-agenda__navegacion a").Select(a => a.TextContent));
        Assert.Equal(
            ["/admin?fecha=2026-11-09&vista=dia", "/admin?fecha=2026-11-09&vista=semana"],
            agenda.FindAll(".dsm-agenda__vistas a").Select(a => a.GetAttribute("href")));
        Assert.Empty(agenda.FindAll(".dsm-agenda__semana"));
    }

    [Fact]
    public void Siguiente_lleva_a_la_direccion_del_dia_siguiente_y_pide_sus_Citas()
    {
        var agenda = Render<PaginaAgenda>();
        var siguiente = agenda.FindAll(".dsm-agenda__navegacion a")[2].GetAttribute("href")!;
        Assert.Equal("/admin?fecha=2026-11-10&vista=dia", siguiente);
        _consulta.Dias = d => Dia(d, Cita(17, 0, "Bruno Díaz", "Revisión", EstadoEnLaAgenda.Agendada));

        Services.GetRequiredService<NavigationManager>().NavigateTo(siguiente);

        agenda.WaitForAssertion(() => Assert.Equal(DiasDeLaAgenda.DelDia(Hoy.AddDays(1)), _consulta.Pedidas[^1]));
        agenda.WaitForAssertion(() => Assert.Equal("Martes 10 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent));
        Assert.Equal("Bruno Díaz", agenda.Find(".dsm-agenda__paciente").TextContent);
    }

    [Theory]
    [InlineData("/admin?fecha=hola")]
    [InlineData("/admin?fecha=2026-13-40&vista=dia")]
    [InlineData("/admin?vista=otra")]
    public void Una_fecha_o_vista_que_no_se_entiende_es_hoy_en_vista_de_dia(string direccion)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(direccion);

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(DiasDeLaAgenda.DelDia(Hoy), Assert.Single(_consulta.Pedidas));
        Assert.DoesNotContain(PaginaAgenda.NoSeCargo, agenda.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void La_semana_va_de_lunes_a_domingo_con_los_siete_dias()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin?fecha=2026-11-11&vista=semana");
        _consulta.Dias = d => d == new DateOnly(2026, 11, 14)
            ? Dia(d, TipoDeDia.SinAtencion, null, Cita(10, 15, "Ana Sofía López", null, EstadoEnLaAgenda.Agendada, fuera: true, nota: "lo pidió Selina"))
            : Dia(d);

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(new DiasDeLaAgenda(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)), Assert.Single(_consulta.Pedidas));
        Assert.Equal("Semana del 9 al 15 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent);
        var dias = agenda.FindAll(".dsm-agenda__semana .dsm-agenda__dia");
        Assert.Equal(
            ["2026-11-09", "2026-11-10", "2026-11-11", "2026-11-12", "2026-11-13", "2026-11-14", "2026-11-15"],
            dias.Select(d => d.GetAttribute("data-fecha")));
        Assert.Contains("Lun 9", dias[0].QuerySelector(".dsm-agenda__dia-titulo")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("/admin?fecha=2026-11-09&vista=dia", dias[0].QuerySelector(".dsm-agenda__dia-enlace")!.GetAttribute("href"));
        Assert.NotNull(dias[0].QuerySelector(".dsm-agenda__hoy"));
        Assert.Null(dias[1].QuerySelector(".dsm-agenda__hoy"));

        // El sábado sin Tramos se ve atenuado, dice "No se atiende" y muestra su Cita manual con su marca y su nota.
        var sabado = dias[5];
        Assert.Contains("dsm-agenda__dia--cerrado", sabado.ClassName, StringComparison.Ordinal);
        Assert.Equal(PaginaAgenda.NoSeAtiende, sabado.QuerySelector(".dsm-agenda__marca")!.TextContent);
        Assert.Equal("Ana Sofía López", sabado.QuerySelector(".dsm-agenda__paciente")!.TextContent);
        Assert.Equal(PaginaAgenda.FueraDeHorario, sabado.QuerySelector(".dsm-chip-de-estado--fuera")!.TextContent);
        Assert.Equal("lo pidió Selina", sabado.QuerySelector(".dsm-agenda__nota")!.TextContent);

        Assert.Equal(
            ["/admin?fecha=2026-11-04&vista=semana", "/admin?fecha=2026-11-09&vista=semana", "/admin?fecha=2026-11-18&vista=semana"],
            agenda.FindAll(".dsm-agenda__navegacion a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void Una_semana_que_cruza_de_mes_lo_dice()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin?fecha=2026-12-03&vista=semana");

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(new DiasDeLaAgenda(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 6)), Assert.Single(_consulta.Pedidas));
        Assert.Equal("Semana del 30 de noviembre al 6 de diciembre", agenda.Find(".dsm-agenda__fecha").TextContent);
    }

    [Theory]
    [InlineData(TipoDeDia.Normal, null, null)]
    [InlineData(TipoDeDia.SinAtencion, null, "No se atiende")]
    [InlineData(TipoDeDia.CustomSinAtencion, "vacaciones", "No se atiende · vacaciones")]
    [InlineData(TipoDeDia.DiaAbierto, "sábado abierto", "Día abierto · sábado abierto")]
    [InlineData(TipoDeDia.HorarioEspecial, "cirugía", "Horario especial · cirugía")]
    public void El_dia_lleva_su_marca_con_el_motivo_de_su_Agenda_custom(TipoDeDia tipo, string? motivo, string? marca)
    {
        _consulta.Dias = d => Dia(d, tipo, motivo);

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(marca, agenda.FindAll(".dsm-agenda__marca").SingleOrDefault()?.TextContent);
        Assert.Equal(
            tipo is TipoDeDia.CustomSinAtencion or TipoDeDia.DiaAbierto or TipoDeDia.HorarioEspecial,
            agenda.Find(".dsm-agenda__dia").ClassList.Contains("dsm-agenda__dia--custom"));
    }

    [Fact]
    public void Los_Tramos_vigentes_del_dia_se_muestran()
    {
        _consulta.Dias = d => Dia(d, TipoDeDia.HorarioEspecial, "cirugía") with
        {
            Tramos = [new TramoDeLaAgenda(new TimeOnly(15, 30), new TimeOnly(17, 0))],
        };

        var agenda = Render<PaginaAgenda>();

        Assert.Equal("3:30 pm a 5:00 pm", agenda.Find(".dsm-agenda__tramos").TextContent);
    }

    [Fact]
    public void Cada_estado_lleva_su_etiqueta_en_orden_con_nombre_y_motivo()
    {
        _consulta.Dias = d => Dia(
            d,
            Cita(15, 30, "Bruno Díaz", null, EstadoEnLaAgenda.NoSePresento),
            Cita(16, 0, "Ana Sofía López", "Dolor de rodilla", EstadoEnLaAgenda.Atendida),
            Cita(17, 0, "Bruno Díaz", "Revisión de hombro", EstadoEnLaAgenda.Agendada));

        var agenda = Render<PaginaAgenda>();
        var filas = agenda.FindAll(".dsm-agenda__cita");

        Assert.Equal(
            [
                ("3:30 pm", "Bruno Díaz", "No se presentó"),
                ("4:00 pm", "Ana Sofía López", "Atendida"),
                ("5:00 pm", "Bruno Díaz", "Agendada"),
            ],
            filas.Select(f => (
                f.QuerySelector(".dsm-agenda__inicio")!.TextContent,
                f.QuerySelector(".dsm-agenda__paciente")!.TextContent,
                f.QuerySelector(".dsm-agenda__estado")!.TextContent)));
        Assert.Null(filas[0].QuerySelector(".dsm-agenda__motivo"));
        Assert.Equal("Dolor de rodilla", filas[1].QuerySelector(".dsm-agenda__motivo")!.TextContent);
        Assert.Contains("dsm-agenda__cita--atendida", filas[1].ClassName, StringComparison.Ordinal);
        Assert.Empty(agenda.FindAll(".dsm-agenda__vacia"));
        Assert.Empty(agenda.FindAll(".dsm-agenda__marcas"));
    }

    [Fact]
    public void Sin_correo_lleva_su_etiqueta_y_una_Cita_dentro_de_horario_no_lleva_marca()
    {
        _consulta.Dias = d => Dia(
            d,
            Cita(16, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada, sinCorreo: true),
            Cita(17, 0, "Bruno Díaz", "Revisión", EstadoEnLaAgenda.Agendada));

        var agenda = Render<PaginaAgenda>();
        var filas = agenda.FindAll(".dsm-agenda__cita");

        Assert.Equal(PaginaAgenda.SinCorreo, filas[0].QuerySelector(".dsm-agenda__sin-correo")!.TextContent);
        Assert.Null(filas[0].QuerySelector(".dsm-chip-de-estado--fuera"));
        Assert.Null(filas[1].QuerySelector(".dsm-agenda__marcas"));
    }

    [Fact]
    public void Las_canceladas_se_ocultan_con_un_aviso_y_el_control_las_muestra()
    {
        _consulta.Dias = d => Dia(
            d,
            Cita(16, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada),
            Cita(17, 30, "Bruno Díaz", "Esguince", EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia));

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(["Ana Sofía López"], agenda.FindAll(".dsm-agenda__paciente").Select(p => p.TextContent));
        Assert.Equal("1 cancelada oculta", agenda.Find(".dsm-agenda__ocultas").TextContent);

        agenda.Find(".dsm-agenda__filtro input").Change(true);

        Assert.Equal(["Ana Sofía López", "Bruno Díaz"], agenda.FindAll(".dsm-agenda__paciente").Select(p => p.TextContent));
        var cancelada = agenda.FindAll(".dsm-agenda__cita")[1];
        Assert.Contains("dsm-agenda__cita--cancelada", cancelada.ClassName, StringComparison.Ordinal);
        Assert.Contains("dsm-chip-de-estado--tardia", cancelada.QuerySelector(".dsm-agenda__estado")!.ClassName, StringComparison.Ordinal);
        Assert.Empty(agenda.FindAll(".dsm-agenda__ocultas"));
        Assert.Single(_consulta.Pedidas);
    }

    [Theory]
    [InlineData(CancelacionEnLaAgenda.PorElPaciente, "Cancelada (por el paciente)", "dsm-chip-de-estado--neutro")]
    [InlineData(CancelacionEnLaAgenda.Tardia, "Cancelada (tardía)", "dsm-chip-de-estado--tardia")]
    [InlineData(CancelacionEnLaAgenda.PorElConsultorio, "Cancelada (por el consultorio)", "dsm-chip-de-estado--neutro")]
    public void Las_tres_cancelaciones_se_distinguen(CancelacionEnLaAgenda cancelacion, string etiqueta, string clase)
    {
        _consulta.Dias = d => Dia(d, Cita(17, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Cancelada, cancelacion));

        var agenda = Render<PaginaAgenda>();
        agenda.Find(".dsm-agenda__filtro input").Change(true);

        var chip = agenda.Find(".dsm-agenda__estado");
        Assert.Equal(etiqueta, chip.TextContent);
        Assert.Contains(clase, chip.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_dia_sin_Citas_lo_dice()
    {
        var agenda = Render<PaginaAgenda>();

        Assert.Equal(PaginaAgenda.DiaVacio, agenda.Find(".dsm-agenda__vacia").TextContent);
        Assert.Empty(agenda.FindAll(".dsm-agenda__cita"));
    }

    [Fact]
    public void Una_Cita_sin_Paciente_se_lista_como_Paciente_no_encontrado()
    {
        _consulta.Dias = d => Dia(d, Cita(17, 0, null, "Dolor", EstadoEnLaAgenda.Agendada));

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(PaginaAgenda.PacienteNoEncontrado, agenda.Find(".dsm-agenda__paciente").TextContent);
    }

    [Fact]
    public void Si_la_consulta_no_vuelve_con_exito_no_dice_que_el_dia_esta_vacio()
    {
        _consulta.Falla = true;

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(PaginaAgenda.NoSeCargo, agenda.Find(".dsm-agenda__vacia").TextContent);
        Assert.DoesNotContain(PaginaAgenda.DiaVacio, agenda.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Lo_cargado_en_el_prerenderizado_sirve_solo_para_su_fecha_y_vista()
    {
        // bUnit no pasa [PersistentState] como parámetro: se prueba la comparación que decide si se consulta de nuevo.
        var cargada = new PaginaAgenda.AgendaCargada(Hoy, Semana: false, [Dia(Hoy)], Fallo: false);

        Assert.True(cargada.EsDe(Hoy, semana: false));
        Assert.False(cargada.EsDe(Hoy, semana: true));
        Assert.False(cargada.EsDe(Hoy.AddDays(1), semana: false));
    }

    [Fact]
    public void El_refresco_trae_la_Cita_nueva_sin_recargar_y_una_vuelta_fallida_conserva_lo_mostrado()
    {
        _intervalo = TimeSpan.FromMilliseconds(20);
        var vueltas = 0;
        _consulta.Dias = d =>
        {
            vueltas++;
            return vueltas == 1
                ? Dia(d, Cita(16, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada))
                : Dia(d, Cita(16, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada), Cita(17, 0, "Bruno Díaz", "En línea", EstadoEnLaAgenda.Agendada));
        };

        var agenda = Render<PaginaAgenda>();

        agenda.WaitForAssertion(
            () => Assert.Equal(["Ana Sofía López", "Bruno Díaz"], agenda.FindAll(".dsm-agenda__paciente").Select(p => p.TextContent)),
            TimeSpan.FromSeconds(5));
        Assert.All(_consulta.Pedidas, p => Assert.Equal(DiasDeLaAgenda.DelDia(Hoy), p));

        // Una vuelta que no vuelve con éxito y otra que lanza dejan lo último mostrado.
        _consulta.Falla = true;
        var pedidas = _consulta.Pedidas.Count;
        Esperar(() => _consulta.Pedidas.Count >= pedidas + 2);
        _consulta.Lanza = true;
        pedidas = _consulta.Pedidas.Count;
        Esperar(() => _consulta.Pedidas.Count >= pedidas + 2);

        Assert.Equal(["Ana Sofía López", "Bruno Díaz"], agenda.FindAll(".dsm-agenda__paciente").Select(p => p.TextContent));
        Assert.Empty(agenda.FindAll(".dsm-agenda__vacia"));
    }

    [Fact]
    public void Al_navegar_no_se_pintan_los_dias_anteriores_y_una_vuelta_de_la_fecha_anterior_se_descarta()
    {
        _intervalo = TimeSpan.FromMilliseconds(20);
        var refrescoDetenido = new TaskCompletionSource();
        var navegacionDetenida = new TaskCompletionSource();
        var vueltasDeHoy = 0;
        _consulta.Dias = d => d == Hoy
            ? Dia(d, Cita(16, 0, vueltasDeHoy++ == 0 ? "Ana Sofía López" : "De la vuelta vieja", "Dolor", EstadoEnLaAgenda.Agendada))
            : Dia(d, Cita(17, 0, "Bruno Díaz", "Revisión", EstadoEnLaAgenda.Agendada));
        _consulta.Antes = c =>
            c.Desde == Hoy && vueltasDeHoy > 0 ? refrescoDetenido.Task
            : c.Desde == Hoy.AddDays(1) ? navegacionDetenida.Task
            : Task.CompletedTask;

        var agenda = Render<PaginaAgenda>();
        // La primera vuelta del refresco, con hoy, se queda esperando a la base.
        Esperar(() => _consulta.Pedidas.Count >= 2);

        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin?fecha=2026-11-10&vista=dia");

        agenda.WaitForAssertion(() => Assert.Equal("Martes 10 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent));
        Assert.Empty(agenda.FindAll(".dsm-agenda__paciente"));

        // La vuelta vieja termina después de navegar: no se guarda.
        refrescoDetenido.SetResult();
        Esperar(() => _consulta.Pedidas[^1] == DiasDeLaAgenda.DelDia(Hoy.AddDays(1)));
        Assert.DoesNotContain(
            "De la vuelta vieja",
            agenda.Instance.Cargada!.Dias!.SelectMany(d => d.Citas).Select(c => c.Paciente));

        navegacionDetenida.SetResult();
        agenda.WaitForAssertion(() => Assert.Equal(["Bruno Díaz"], agenda.FindAll(".dsm-agenda__paciente").Select(p => p.TextContent)));
    }

    [Fact]
    public void Una_cancelacion_que_no_viene_de_salir_no_detiene_el_refresco()
    {
        _intervalo = TimeSpan.FromMilliseconds(20);
        var vueltas = 0;
        _consulta.Dias = d => ++vueltas == 1
            ? Dia(d, Cita(16, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada))
            : Dia(d, Cita(16, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Agendada), Cita(17, 0, "Bruno Díaz", "En línea", EstadoEnLaAgenda.Agendada));

        var agenda = Render<PaginaAgenda>();
        _consulta.CancelacionesPorLanzar = 1;

        agenda.WaitForAssertion(
            () => Assert.Equal(["Ana Sofía López", "Bruno Díaz"], agenda.FindAll(".dsm-agenda__paciente").Select(p => p.TextContent)),
            TimeSpan.FromSeconds(5));
        Assert.Equal(0, _consulta.CancelacionesPorLanzar);
    }

    [Fact]
    public void Pasada_la_medianoche_sin_fecha_en_la_direccion_el_refresco_pide_el_dia_nuevo()
    {
        _intervalo = TimeSpan.FromMilliseconds(20);
        // Lunes 9, 11:59:50 pm en Ciudad de México.
        _reloj.UtcNow = new DateTimeOffset(2026, 11, 10, 5, 59, 50, TimeSpan.Zero);

        var agenda = Render<PaginaAgenda>();
        Assert.Equal("Hoy, Lunes 9 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent);

        _reloj.UtcNow = new DateTimeOffset(2026, 11, 10, 6, 0, 10, TimeSpan.Zero);

        agenda.WaitForAssertion(
            () => Assert.Equal("Hoy, Martes 10 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent),
            TimeSpan.FromSeconds(5));
        Assert.Equal(DiasDeLaAgenda.DelDia(Hoy.AddDays(1)), _consulta.Pedidas[^1]);
        Assert.Equal("2026-11-10", agenda.Find(".dsm-agenda__dia").GetAttribute("data-fecha"));
    }

    [Fact]
    public void Si_la_consulta_al_navegar_lanza_se_dice_que_no_se_cargo()
    {
        var agenda = Render<PaginaAgenda>();
        _consulta.Lanza = true;

        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin?fecha=2026-11-10&vista=dia");

        agenda.WaitForAssertion(() => Assert.Equal(PaginaAgenda.NoSeCargo, agenda.Find(".dsm-agenda__vacia").TextContent));
        Assert.Equal("Martes 10 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent);
        Assert.Empty(agenda.FindAll(".dsm-agenda__dia"));
    }

    [Fact]
    public async Task Al_salir_de_la_pagina_no_se_consulta_mas()
    {
        _intervalo = TimeSpan.FromMilliseconds(20);
        var agenda = Render<PaginaAgenda>();
        Esperar(() => _consulta.Pedidas.Count >= 2);

        await DisposeComponentsAsync();
        var pedidas = _consulta.Pedidas.Count;
        await Task.Delay(TimeSpan.FromMilliseconds(200), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(pedidas, _consulta.Pedidas.Count);
    }

    [Fact]
    public void En_el_prerenderizado_no_hay_refresco()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        _intervalo = TimeSpan.FromMilliseconds(20);

        Render<PaginaAgenda>();
        Thread.Sleep(200);

        Assert.Single(_consulta.Pedidas);
    }

    // WaitForAssertion solo vuelve a mirar cuando la página se pinta; una vuelta fallida no la pinta.
    private static void Esperar(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion())
        {
            Assert.True(DateTime.UtcNow < limite, "La condición no se cumplió a tiempo.");
            Thread.Sleep(10);
        }
    }

    private static DiaDeLaAgenda Dia(DateOnly fecha, params CitaDeLaAgenda[] citas) =>
        Dia(fecha, TipoDeDia.Normal, null, citas);

    private static DiaDeLaAgenda Dia(DateOnly fecha, TipoDeDia tipo, string? motivo, params CitaDeLaAgenda[] citas) =>
        new(fecha, tipo, motivo, [], citas);

    private static CitaDeLaAgenda Cita(
        int hora, int minuto, string? paciente, string? motivo, EstadoEnLaAgenda estado, CancelacionEnLaAgenda? cancelacion = null,
        bool sinCorreo = false, bool fuera = false, string? nota = null) =>
        new(CitaId.Nuevo(), new TimeOnly(hora, minuto), new TimeOnly(hora, minuto).AddMinutes(30), paciente, motivo, estado,
            cancelacion, sinCorreo, fuera, nota);

    private sealed class ConsultaFalsa : IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>
    {
        /// <summary>El día que devuelve para cada fecha pedida; por omisión, un día normal sin Citas.</summary>
        public Func<DateOnly, DiaDeLaAgenda> Dias { get; set; } = fecha => Dia(fecha);

        /// <summary>Vuelve no autorizado.</summary>
        public bool Falla { get; set; }

        /// <summary>Lanza, como si la base no contestara.</summary>
        public bool Lanza { get; set; }

        /// <summary>Cuántas veces más lanza una cancelación que no viene de salir de la página.</summary>
        public int CancelacionesPorLanzar { get; set; }

        /// <summary>Lo que se espera antes de contestar cada consulta; por omisión, nada.</summary>
        public Func<DiasDeLaAgenda, Task> Antes { get; set; } = _ => Task.CompletedTask;

        private readonly List<DiasDeLaAgenda> _pedidas = [];

        /// <summary>Lo pedido hasta ahora, copiado: el refresco agrega desde otro hilo.</summary>
        public IReadOnlyList<DiasDeLaAgenda> Pedidas
        {
            get
            {
                lock (_pedidas)
                {
                    return [.. _pedidas];
                }
            }
        }

        public async Task<Resultado<IReadOnlyList<DiaDeLaAgenda>>> HandleAsync(DiasDeLaAgenda consulta, CancellationToken cancellationToken)
        {
            lock (_pedidas)
            {
                _pedidas.Add(consulta);
            }

            await Antes(consulta);

            if (Lanza)
            {
                throw new InvalidOperationException("La base no contesta.");
            }

            if (CancelacionesPorLanzar > 0)
            {
                CancelacionesPorLanzar--;
                throw new OperationCanceledException("Se agotó el tiempo de la base.");
            }

            if (Falla)
            {
                return Resultado.NoAutorizado().Como<IReadOnlyList<DiaDeLaAgenda>>();
            }

            IReadOnlyList<DiaDeLaAgenda> dias =
            [
                .. Enumerable.Range(0, consulta.Hasta.DayNumber - consulta.Desde.DayNumber + 1)
                    .Select(i => Dias(consulta.Desde.AddDays(i))),
            ];
            return Resultado.Exito(dias);
        }
    }

    private sealed class RelojMovible(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = ahora;
    }
}
