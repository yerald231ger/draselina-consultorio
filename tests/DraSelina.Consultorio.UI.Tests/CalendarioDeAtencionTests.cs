using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// El Calendario de atención (entrada 6.1) con una consulta y un comando falsos: el mes de lunes a domingo, cada día con
/// su marca y sus Tramos, los días pasados sin editar, el formulario lleno con lo que vale el día, agregar y quitar
/// Tramos, el comando que se manda, y los mensajes de cada rechazo, incluido el de Citas fuera. Hoy es la fecha local del
/// reloj de la aplicación.
/// </summary>
public sealed class CalendarioDeAtencionTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    /// <summary>Lunes 9 de noviembre de 2026, 4:10 pm en Ciudad de México (UTC-6).</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 22, 10, 0, TimeSpan.Zero);

    private static readonly DateOnly Hoy = new(2026, 11, 9);

    private static readonly DateOnly Sabado = new(2026, 11, 14);

    private readonly ConsultaFalsa _consulta = new();

    private readonly ComandoFalso _comando = new();

    public CalendarioDeAtencionTests()
    {
        Services.AddSingleton<IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>>(_consulta);
        Services.AddSingleton<ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>>(_comando);
        Services.AddSingleton<IClock>(new Reloj(Ahora));
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Sin_mes_pide_el_de_hoy_y_lo_pinta_de_lunes_a_domingo()
    {
        var calendario = Render<CalendarioDeAtencion>();

        Assert.Equal(new MesDeAtencion(2026, 11), Assert.Single(_consulta.Pedidas));
        Assert.Equal("Noviembre 2026", calendario.Find(".dsm-calendario__mes-titulo").TextContent);
        Assert.Equal(["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"], calendario.FindAll("thead th").Select(t => t.TextContent));
        // Noviembre de 2026 empieza en domingo: seis celdas vacías antes del 1.
        Assert.Equal(6, calendario.FindAll("tbody tr")[0].QuerySelectorAll(".dsm-calendario__dia--fuera").Length);
        Assert.Equal(30, calendario.FindAll("td[data-fecha]").Count);
        Assert.Equal(
            ["/admin/calendario?mes=2026-10", "/admin/calendario?mes=2026-12"],
            calendario.FindAll(".dsm-calendario__meses a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void Un_mes_en_la_direccion_pide_ese_mes()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/calendario?mes=2027-02");

        Render<CalendarioDeAtencion>();

        Assert.Equal(new MesDeAtencion(2027, 2), Assert.Single(_consulta.Pedidas));
    }

    [Fact]
    public void Cada_dia_dice_su_marca_con_su_motivo_y_sus_Tramos()
    {
        _consulta.Personalizar = d => d.Fecha.Day switch
        {
            10 => d with { Tipo = TipoDeDia.HorarioEspecial, MotivoDeCustom = "cirugía", Tramos = [Tramo(15, 30, 17, 0)] },
            11 => d with { Tipo = TipoDeDia.CustomSinAtencion, MotivoDeCustom = "vacaciones", Tramos = [] },
            14 => d with { Tipo = TipoDeDia.DiaAbierto, MotivoDeCustom = "sábado abierto", Tramos = [Tramo(10, 0, 13, 0)] },
            _ => d,
        };

        var calendario = Render<CalendarioDeAtencion>();

        Assert.Equal("Horario especial · cirugía", Celda(calendario, 10).QuerySelector(".dsm-calendario__marca")!.TextContent);
        Assert.Equal("No se atiende · vacaciones", Celda(calendario, 11).QuerySelector(".dsm-calendario__marca")!.TextContent);
        Assert.Equal("Día abierto · sábado abierto", Celda(calendario, 14).QuerySelector(".dsm-calendario__marca")!.TextContent);
        Assert.Equal("No se atiende", Celda(calendario, 15).QuerySelector(".dsm-calendario__marca")!.TextContent);
        Assert.Null(Celda(calendario, 12).QuerySelector(".dsm-calendario__marca"));
        Assert.Contains("dsm-calendario__dia--custom", Celda(calendario, 14).ClassName, StringComparison.Ordinal);
        Assert.Contains("dsm-calendario__dia--cerrado", Celda(calendario, 11).ClassName, StringComparison.Ordinal);
        Assert.Equal("10:00 am a 1:00 pm", Celda(calendario, 14).QuerySelector(".dsm-calendario__horas")!.TextContent);
    }

    [Fact]
    public void Los_dias_pasados_se_ven_sin_boton_y_hoy_se_puede_tocar()
    {
        var calendario = Render<CalendarioDeAtencion>();

        Assert.Null(Celda(calendario, 8).QuerySelector("button"));
        Assert.NotNull(Celda(calendario, 8).QuerySelector(".dsm-calendario__pasado"));
        Assert.NotNull(Celda(calendario, 9).QuerySelector("button"));
    }

    [Fact]
    public void Tocar_un_dia_abre_el_formulario_lleno_con_sus_Tramos_y_su_motivo()
    {
        _consulta.Personalizar = d => d.Fecha == Sabado
            ? d with { Tipo = TipoDeDia.DiaAbierto, MotivoDeCustom = "sábado abierto", Tramos = [Tramo(10, 0, 13, 0)] }
            : d;
        var calendario = Render<CalendarioDeAtencion>();

        Celda(calendario, 14).QuerySelector("button")!.Click();

        Assert.Contains("Sábado 14 de noviembre", calendario.Find("#editor-del-dia").TextContent, StringComparison.Ordinal);
        Assert.Equal("10:00", calendario.Find("#inicio-0").GetAttribute("value"));
        Assert.Equal("13:00", calendario.Find("#fin-0").GetAttribute("value"));
        Assert.Equal("sábado abierto", calendario.Find("#motivo").GetAttribute("value"));
    }

    [Fact]
    public void Quitar_todos_los_Tramos_dice_que_no_se_atiende_y_guarda_sin_Tramos()
    {
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 11).QuerySelector("button")!.Click();

        calendario.Find(".dsm-calendario__quitar").Click();
        Assert.Contains(CalendarioDeAtencion.SinTramos, calendario.Find(".dsm-calendario__sin-tramos").TextContent, StringComparison.Ordinal);
        calendario.Find("#motivo").Change("vacaciones");
        calendario.Find("form").Submit();

        var enviado = Assert.Single(_comando.Recibidos);
        Assert.Equal(new DateOnly(2026, 11, 11), enviado.Fecha);
        Assert.Equal("vacaciones", enviado.Motivo);
        Assert.Empty(enviado.Tramos);
    }

    [Fact]
    public void Agregar_un_Tramo_y_guardar_manda_los_dos_y_vuelve_a_pedir_el_mes()
    {
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 10).QuerySelector("button")!.Click();

        calendario.Find(".dsm-calendario__agregar").Click();
        calendario.Find("#fin-0").Change("17:00");
        calendario.Find("#inicio-1").Change("17:30");
        calendario.Find("#fin-1").Change("18:00");
        calendario.Find("#motivo").Change("junta");
        calendario.Find("form").Submit();

        var enviado = Assert.Single(_comando.Recibidos);
        Assert.Equal(
            [(new TimeOnly(15, 30), new TimeOnly(17, 0)), (new TimeOnly(17, 30), new TimeOnly(18, 0))],
            enviado.Tramos.Select(t => (t.Inicio!.Value, t.Fin!.Value)));
        Assert.Equal(2, _consulta.Pedidas.Count);
        Assert.Empty(calendario.FindAll("form"));
        Assert.Equal("Se guardó el horario propio del Martes 10 de noviembre.", calendario.Find(".dsm-calendario__guardado").TextContent);
    }

    [Theory]
    [InlineData(CodigosDeAgenda.CitasFuera, CalendarioDeAtencion.CitasFuera)]
    [InlineData(CodigosDeAgenda.TramosEncimados, CalendarioDeAtencion.TramosEncimados)]
    [InlineData(CodigosDeAgenda.TramoCorto, CalendarioDeAtencion.TramoCorto)]
    [InlineData(CodigosDeAgenda.FechaPasada, CalendarioDeAtencion.FechaPasada)]
    public void Un_rechazo_deja_el_formulario_abierto_con_su_mensaje(string codigo, string mensaje)
    {
        _comando.Responder = _ => Resultado.Fallo(codigo).Como<AgendaCustomGuardada>();
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 10).QuerySelector("button")!.Click();
        calendario.Find("#motivo").Change("cirugía");

        calendario.Find("form").Submit();

        Assert.Equal(mensaje, calendario.Find(".dsm-calendario__error").TextContent.Trim());
        Assert.NotEmpty(calendario.FindAll("form"));
        Assert.Single(_consulta.Pedidas);
    }

    [Fact]
    public void Los_errores_de_forma_van_bajo_su_campo()
    {
        _comando.Responder = _ => Resultado.Invalido(
            [
                (nameof(GuardarAgendaCustom.Motivo), GuardarAgendaCustom.MensajeDeMotivo),
                (GuardarAgendaCustom.PropiedadDelTramo(0), GuardarAgendaCustom.MensajeDeTramoAlReves),
            ]).Como<AgendaCustomGuardada>();
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 10).QuerySelector("button")!.Click();

        calendario.Find("form").Submit();

        Assert.Equal(GuardarAgendaCustom.MensajeDeMotivo, calendario.Find("#error-Motivo").TextContent);
        Assert.Equal(GuardarAgendaCustom.MensajeDeTramoAlReves, calendario.Find("#error-Tramos-0").TextContent);
        Assert.Equal("true", calendario.Find("#motivo").GetAttribute("aria-invalid"));
        Assert.Equal("error-Tramos-0", calendario.Find("#inicio-0").GetAttribute("aria-describedby"));
    }

    private static AngleSharp.Dom.IElement Celda(IRenderedComponent<CalendarioDeAtencion> calendario, int dia) =>
        calendario.Find($"td[data-fecha='2026-11-{dia:00}']");

    private static TramoDeLaAgenda Tramo(int h1, int m1, int h2, int m2) => new(new TimeOnly(h1, m1), new TimeOnly(h2, m2));

    private sealed class ConsultaFalsa : IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>
    {
        public List<MesDeAtencion> Pedidas { get; } = [];

        /// <summary>Cambia un día del mes base: de lunes a viernes, 3:30 a 6:00 pm; sábado y domingo sin atención.</summary>
        public Func<DiaDeAtencion, DiaDeAtencion> Personalizar { get; set; } = d => d;

        public Task<Resultado<IReadOnlyList<DiaDeAtencion>>> HandleAsync(MesDeAtencion consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            var primero = new DateOnly(consulta.Anio, consulta.Mes, 1);
            IReadOnlyList<DiaDeAtencion> dias =
            [
                .. Enumerable.Range(0, primero.AddMonths(1).AddDays(-1).Day).Select(i =>
                {
                    var fecha = primero.AddDays(i);
                    var finDeSemana = fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    return Personalizar(new DiaDeAtencion(
                        fecha,
                        finDeSemana ? TipoDeDia.SinAtencion : TipoDeDia.Normal,
                        null,
                        finDeSemana ? [] : [Tramo(15, 30, 18, 0)]));
                }),
            ];
            return Task.FromResult(Resultado.Exito(dias));
        }
    }

    private sealed class ComandoFalso : ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>
    {
        public List<GuardarAgendaCustom> Recibidos { get; } = [];

        public Func<GuardarAgendaCustom, Resultado<AgendaCustomGuardada>> Responder { get; set; } =
            c => Resultado.Exito(new AgendaCustomGuardada(c.Fecha!.Value, Registrada: true, SinCambios: false));

        public Task<Resultado<AgendaCustomGuardada>> HandleAsync(GuardarAgendaCustom comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return Task.FromResult(Responder(comando));
        }
    }

    private sealed class Reloj(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow { get; } = ahora;
    }
}
