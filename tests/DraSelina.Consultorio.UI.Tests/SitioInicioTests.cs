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
        Services.AddSingleton<IQueryHandler<SemanaDeAtencion, IReadOnlyList<DiaDeLaBase>>>(_horario);
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
        _contenido.SinWhatsApp = true;

        var inicio = Render<Inicio>();

        Assert.Empty(inicio.FindAll(".dsm-ubicacion__horario"));
        Assert.Contains("Por ahora no hay horarios en línea.", inicio.Find("#ubicacion").TextContent, StringComparison.Ordinal);
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
    public void El_mapa_es_el_de_Contenido_y_sin_el_no_hay_iframe()
    {
        var mapa = Render<Inicio>().Find("#ubicacion iframe");
        Assert.Equal(
            "https://www.google.com/maps?q=Hospital%20Star%20M%C3%A9dica%2C%20consultorio%20101%2C%20Mariano%20Arista%20No.%20735%2C%20Col.%20Barrio%20de%20Tequisquiapan%2C%20San%20Luis%20Potos%C3%AD&output=embed",
            mapa.GetAttribute("src"));

        _contenido.ConsultorioDePrueba = Contenido.ContenidoApi.Consultorio with { MapaIncrustado = null };

        Assert.Empty(Render<Inicio>().FindAll("#ubicacion iframe"));
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
        _contenido.SinWhatsApp = true;

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
    public void Con_WhatsApp_Ubicacion_trae_la_conversacion_y_llamar()
    {
        _contenido.WhatsAppDePrueba = "4441234567";

        var ubicacion = Render<Inicio>().Find("#ubicacion");

        Assert.Contains("WhatsApp o llamada", ubicacion.TextContent, StringComparison.Ordinal);
        Assert.Equal("https://wa.me/524441234567", ubicacion.QuerySelector(".dsm-ubicacion__whatsapp")!.GetAttribute("href"));
        var llamar = ubicacion.QuerySelector(".dsm-ubicacion__llamar")!;
        Assert.Equal("tel:+524441234567", llamar.GetAttribute("href"));
        Assert.Equal("Llamar", llamar.TextContent);
    }

    [Fact]
    public void Sobre_mi_trae_formacion_certificacion_cedulas_semblanza_y_afiliaciones()
    {
        var sobreMi = Render<Inicio>().Find("#sobre-mi");

        Assert.Equal(
            ["Formación", "Certificación", "Cédula profesional", "Cédula de especialidad"],
            sobreMi.QuerySelectorAll("dt").Select(dt => dt.TextContent));
        Assert.Equal(
            [Contenido.ContenidoApi.Doctora.Formacion!, "Consejo de Ortopedia, 24/8379/2", "12093135", "14899218"],
            sobreMi.QuerySelectorAll("dd").Select(dd => dd.TextContent.Trim()));
        Assert.Empty(sobreMi.QuerySelectorAll(".dsm-pendiente"));
        Assert.Contains("FEMECOT", sobreMi.QuerySelector(".dsm-sobre-mi__afiliaciones")!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_formacion_semblanza_ni_afiliaciones_las_marca_pendientes()
    {
        _contenido.DoctoraDePrueba = Contenido.ContenidoApi.Doctora with { Formacion = null, Semblanza = null, Afiliaciones = null };

        var sobreMi = Render<Inicio>().Find("#sobre-mi");

        Assert.Equal("[Pendiente: universidad]", sobreMi.QuerySelector("dd")!.TextContent.Trim());
        Assert.Equal(3, sobreMi.QuerySelectorAll(".dsm-pendiente").Length);
        Assert.Contains("[Pendiente: semblanza", sobreMi.QuerySelector(".dsm-sobre-mi__semblanza")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("[Pendiente: afiliaciones", sobreMi.QuerySelector(".dsm-sobre-mi__afiliaciones")!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void La_semblanza_sale_en_un_parrafo_por_cada_bloque()
    {
        _contenido.DoctoraDePrueba = Contenido.ContenidoApi.Doctora with { Semblanza = "Atiende a deportistas.\n\nOpera en San Luis." };

        var parrafos = Render<Inicio>().FindAll("#sobre-mi .dsm-sobre-mi__semblanza");

        Assert.Equal(["Atiende a deportistas.", "Opera en San Luis."], parrafos.Select(p => p.TextContent));
    }

    [Fact]
    public void Padecimientos_se_despliegan_por_categoria_con_lo_que_atiende()
    {
        _contenido.DoctoraDePrueba = Contenido.ContenidoApi.Doctora with
        {
            Padecimientos = [new("Rodilla", ["Lesión de menisco", "Artroscopia de rodilla"]), new("Hombro", ["Hombro congelado"])],
        };

        var padecimientos = Render<Inicio>().Find("#padecimientos");

        Assert.Equal("¿En qué te puedo ayudar?", padecimientos.QuerySelector("h2")!.TextContent);
        var categorias = padecimientos.QuerySelectorAll("details");
        Assert.Equal(["Rodilla", "Hombro"], categorias.Select(d => d.QuerySelector("summary")!.TextContent));
        Assert.All(categorias, d => Assert.False(d.HasAttribute("open")));
        Assert.Equal(
            ["Lesión de menisco", "Artroscopia de rodilla"],
            categorias[0].QuerySelectorAll("li").Select(li => li.TextContent));
    }

    [Fact]
    public void En_pantalla_ancha_van_en_tres_columnas_por_fila_y_cada_una_lleva_su_orden()
    {
        _contenido.DoctoraDePrueba = Contenido.ContenidoApi.Doctora with
        {
            Padecimientos = [.. new[] { "A", "B", "C", "D", "E" }.Select(nombre => new Padecimiento(nombre, ["x"]))],
        };

        var columnas = Render<Inicio>().FindAll("#padecimientos .dsm-padecimientos__columna");

        Assert.Equal(
            ["A D", "B E", "C"],
            columnas.Select(c => string.Join(' ', c.QuerySelectorAll("summary").Select(s => s.TextContent))));
        Assert.Equal(
            ["--orden: 0", "--orden: 3", "--orden: 1", "--orden: 4", "--orden: 2"],
            columnas.SelectMany(c => c.QuerySelectorAll("details")).Select(d => d.GetAttribute("style")));
    }

    [Fact]
    public void Los_padecimientos_no_incluyen_pediatria()
    {
        var todos = Contenido.ContenidoApi.Doctora.Padecimientos.SelectMany(p => p.Atiende.Append(p.Nombre));

        Assert.DoesNotContain(todos, texto => texto.Contains("pediátric", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class HorarioFalso : IQueryHandler<SemanaDeAtencion, IReadOnlyList<DiaDeLaBase>>
    {
        public IReadOnlyList<DiaDeLaBase> Semana { get; set; } = [];

        public Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(SemanaDeAtencion consulta, CancellationToken cancellationToken) =>
            Task.FromResult(Resultado.Exito(Semana));
    }
}
