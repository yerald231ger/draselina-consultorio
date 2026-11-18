using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Sitio · Inicio con la Agenda base falsa. Ubicación y horarios (entrada 7.1): los grupos del horario y, con la semana
/// sin atención, el aviso en lugar de la lista. Portada, Sobre mí y Padecimientos (entrada 7.2): los datos de la doctora
/// de Contenido, lo pendiente marcado y el WhatsApp con y sin número.
/// </summary>
public sealed class SitioInicioTests : BunitContext
{
    private readonly HorarioFalso _horario = new();
    private readonly ContenidoDePrueba _contenido = new();

    public SitioInicioTests()
    {
        Services.AddSingleton<IQueryHandler<HorarioDeAtencion, IReadOnlyList<DiaDeLaBase>>>(_horario);
        Services.AddSingleton<IContenidoApi>(_contenido);
    }

    [Fact]
    public void Muestra_cada_grupo_del_horario()
    {
        _horario.Semana =
        [
            new DiaDeLaBase(DayOfWeek.Monday, [new TramoDeLaAgenda(new(15, 30), new(18, 0))]),
            new DiaDeLaBase(DayOfWeek.Saturday, [new TramoDeLaAgenda(new(10, 0), new(13, 0))]),
        ];

        var inicio = Render<Inicio>();

        Assert.Equal(
            ["Lunes 3:30 a 6:00 pm, con cita", "Sábado 10:00 am a 1:00 pm, con cita"],
            inicio.FindAll(".dsm-ubicacion__horario li").Select(li => li.TextContent));
    }

    [Fact]
    public void Sin_atencion_dice_que_no_hay_horario_y_no_lista_nada()
    {
        _horario.Semana = [.. Enum.GetValues<DayOfWeek>().Select(d => new DiaDeLaBase(d, []))];

        var inicio = Render<Inicio>();

        Assert.Empty(inicio.FindAll(".dsm-ubicacion__horario"));
        Assert.Contains("Por ahora no hay horario de atención en línea.", inicio.Find("#ubicacion").TextContent, StringComparison.Ordinal);
        Assert.Contains("Escríbenos por WhatsApp", inicio.Find("#ubicacion").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Ubicacion_trae_los_iconos_de_lugar_reloj_y_WhatsApp_ocultos_al_lector()
    {
        _horario.Semana = [new DiaDeLaBase(DayOfWeek.Monday, [new TramoDeLaAgenda(new(15, 30), new(18, 0))])];

        var datos = Render<Inicio>().FindAll("#ubicacion .dsm-ubicacion__dato");

        Assert.Equal(3, datos.Count);
        var iconos = datos.Select(dato =>
        {
            var icono = Assert.Single(dato.Children, hijo => hijo.LocalName == "svg");
            Assert.Same(dato.FirstElementChild, icono);
            Assert.Equal("true", icono.GetAttribute("aria-hidden"));
            Assert.Equal("false", icono.GetAttribute("focusable"));
            return icono;
        }).ToList();

        // Los del mockup, en orden: el pin (círculo de radio 2.5), el reloj (círculo de radio 9) y el globo (sin círculo).
        Assert.Equal(
            ["2.5", "9", null],
            iconos.Select(icono => icono.QuerySelector("circle")?.GetAttribute("r")));
        Assert.StartsWith("M20 11.5", iconos[2].QuerySelector("path")!.GetAttribute("d"), StringComparison.Ordinal);
    }

    [Fact]
    public void La_portada_trae_cedulas_certificacion_Agendar_y_el_recuadro_SM()
    {
        var inicio = Render<Inicio>();
        var portada = inicio.Find("#inicio");

        Assert.Equal("Dra. Selina Medina Castillo", portada.QuerySelector("h1")!.TextContent);
        Assert.Equal(
            ["Céd. Prof. 12093135", "Céd. Especialidad 14899218", "Certificada por el Consejo de Ortopedia"],
            portada.QuerySelectorAll(".dsm-portada__cedulas li").Select(li => li.TextContent));
        var agendar = Assert.Single(portada.QuerySelectorAll("a[href='/reservar']"));
        Assert.Equal("Agendar cita en línea", agendar.TextContent);
        var retrato = portada.QuerySelector(".dsm-portada__retrato")!;
        Assert.Equal("SM", retrato.TextContent);
        Assert.Equal("true", retrato.GetAttribute("aria-hidden"));
        Assert.Empty(inicio.FindAll("img"));
    }

    [Fact]
    public void Sin_WhatsApp_la_portada_dice_escribenos_sin_boton()
    {
        var portada = Render<Inicio>().Find("#inicio");

        Assert.Contains("Escríbenos por WhatsApp y te ayudamos.", portada.TextContent, StringComparison.Ordinal);
        Assert.Empty(portada.QuerySelectorAll("a[href^='https://wa.me/']"));
    }

    [Fact]
    public void Con_WhatsApp_la_portada_trae_el_boton_a_la_conversacion()
    {
        _contenido.WhatsAppDePrueba = "4441234567";

        var portada = Render<Inicio>().Find("#inicio");

        var boton = Assert.Single(portada.QuerySelectorAll("a[href^='https://wa.me/']"));
        Assert.Equal("https://wa.me/524441234567", boton.GetAttribute("href"));
        Assert.Equal("WhatsApp", boton.TextContent);
        Assert.DoesNotContain("Escríbenos por WhatsApp", portada.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Sobre_mi_marca_lo_pendiente_y_trae_certificacion_y_cedulas()
    {
        var sobreMi = Render<Inicio>().Find("#sobre-mi");

        Assert.Equal(
            ["Formación", "Certificación", "Cédula profesional", "Cédula de especialidad"],
            sobreMi.QuerySelectorAll("dt").Select(dt => dt.TextContent));
        Assert.Equal(
            ["[Pendiente: universidad]", "Consejo de Ortopedia, 24/8379/2", "12093135", "14899218"],
            sobreMi.QuerySelectorAll("dd").Select(dd => dd.TextContent.Trim()));
        Assert.Equal(3, sobreMi.QuerySelectorAll(".dsm-pendiente").Length);
        Assert.Contains("[Pendiente: semblanza", sobreMi.QuerySelector(".dsm-sobre-mi__semblanza")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("[Pendiente: afiliaciones", sobreMi.QuerySelector(".dsm-sobre-mi__afiliaciones")!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_semblanza_se_muestra_sin_marca_de_pendiente()
    {
        _contenido.DoctoraDePrueba = Contenido.ContenidoApi.Doctora with { Semblanza = "Atiende a deportistas desde 2015." };

        var semblanza = Render<Inicio>().Find("#sobre-mi .dsm-sobre-mi__semblanza");

        Assert.Equal("Atiende a deportistas desde 2015.", semblanza.TextContent);
        Assert.Empty(semblanza.QuerySelectorAll(".dsm-pendiente"));
    }

    [Fact]
    public void Padecimientos_trae_las_seis_tarjetas_en_orden_con_su_descripcion_pendiente()
    {
        var padecimientos = Render<Inicio>().Find("#padecimientos");

        Assert.Equal("¿En qué te puedo ayudar?", padecimientos.QuerySelector("h2")!.TextContent);
        var tarjetas = padecimientos.QuerySelectorAll("article");
        Assert.Equal(
            ["Rodilla", "Hombro", "Columna", "Cadera", "Fracturas", "Lesiones deportivas"],
            tarjetas.Select(a => a.QuerySelector("h3")!.TextContent));
        Assert.All(tarjetas, a => Assert.Equal(
            "[Pendiente: descripción breve escrita con Selina]", a.QuerySelector(".dsm-pendiente")!.TextContent));
    }

    // ContenidoApi con el WhatsApp o los datos de la doctora que pida la prueba.
    private sealed class ContenidoDePrueba : IContenidoApi
    {
        private readonly Contenido.ContenidoApi _real = new();

        public string? WhatsAppDePrueba { get; set; }

        public DatosDeLaDoctora? DoctoraDePrueba { get; set; }

        public DatosDelConsultorio DatosDelConsultorio => _real.DatosDelConsultorio;

        public string VersionDelAvisoDePrivacidad => _real.VersionDelAvisoDePrivacidad;

        public IReadOnlyList<string> TextoDelAvisoDePrivacidad => _real.TextoDelAvisoDePrivacidad;

        public string? WhatsApp => WhatsAppDePrueba ?? _real.WhatsApp;

        public DatosDeLaDoctora DatosDeLaDoctora => DoctoraDePrueba ?? _real.DatosDeLaDoctora;
    }

    private sealed class HorarioFalso : IQueryHandler<HorarioDeAtencion, IReadOnlyList<DiaDeLaBase>>
    {
        public IReadOnlyList<DiaDeLaBase> Semana { get; set; } = [];

        public Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(HorarioDeAtencion consulta, CancellationToken cancellationToken) =>
            Task.FromResult(Resultado.Exito(Semana));
    }
}
