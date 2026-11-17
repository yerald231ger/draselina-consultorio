using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// La semana tipo de Configuración (entrada 6.5) con consulta, vista previa y comando falsos: cargar la semana, copiar
/// los Tramos de un día a otros, guardar la semana completa, las Citas fuera antes de guardar (que no se cancelan) y
/// los errores de un día.
/// </summary>
public sealed class SeccionSemanaTipoTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    private static readonly DayOfWeek[] Semana =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    private readonly VistaPreviaFalsa _vista = new();

    private readonly CambiarFalso _cambiar = new();

    public SeccionSemanaTipoTests()
    {
        Services.AddSingleton<IQueryHandler<SemanaTipo, IReadOnlyList<DiaDeLaBase>>>(new SemanaFalsa());
        Services.AddSingleton<IQueryHandler<CambioDeAgendaBase, CambioDeBaseRevisado>>(_vista);
        Services.AddSingleton<ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada>>(_cambiar);
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Carga_la_semana_de_lunes_a_domingo()
    {
        var seccion = Render<SeccionSemanaTipo>();

        Assert.Equal(
            ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"],
            seccion.FindAll(".dsm-semana-tipo__nombre").Select(l => l.TextContent));
        Assert.Equal("15:30:00", seccion.Find("#monday-inicio-0").GetAttribute("value"));
        Assert.Equal("18:00:00", seccion.Find("#monday-fin-0").GetAttribute("value"));
        Assert.Equal("No se atiende", Dia(seccion, "saturday").QuerySelector(".dsm-semana-tipo__sin-tramos")!.TextContent);
    }

    [Fact]
    public void Copiar_el_lunes_a_miercoles_y_viernes_les_pone_sus_Tramos()
    {
        var seccion = Render<SeccionSemanaTipo>();
        seccion.Find("#monday-fin-0").Change("17:00:00");

        Dia(seccion, "monday").QuerySelector(".dsm-semana-tipo__copiar")!.Click();
        Casilla(seccion, "wednesday").Change(true);
        Casilla(seccion, "friday").Change(true);
        seccion.Find(".dsm-semana-tipo__aplicar").Click();

        Assert.Equal("17:00:00", seccion.Find("#wednesday-fin-0").GetAttribute("value"));
        Assert.Equal("17:00:00", seccion.Find("#friday-fin-0").GetAttribute("value"));
        Assert.Equal("18:00:00", seccion.Find("#tuesday-fin-0").GetAttribute("value"));
        Assert.Empty(seccion.FindAll(".dsm-semana-tipo__copia"));
    }

    [Fact]
    public void Guardar_sin_Citas_fuera_manda_la_semana_completa_y_dice_las_fechas_con_custom()
    {
        _cambiar.Responder = _ => Resultado.Exito(new AgendaBaseCambiada(
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], [new DateOnly(2026, 11, 4)]));
        var seccion = Render<SeccionSemanaTipo>();
        seccion.Find("#monday-fin-0").Change("17:00:00");
        Dia(seccion, "monday").QuerySelector(".dsm-semana-tipo__copiar")!.Click();
        Casilla(seccion, "wednesday").Change(true);
        Casilla(seccion, "friday").Change(true);
        seccion.Find(".dsm-semana-tipo__aplicar").Click();

        seccion.Find("form").Submit();

        Assert.Single(_vista.Pedidas);
        var enviado = Assert.Single(_cambiar.Recibidos);
        Assert.Equal(Semana, enviado.Dias.Select(d => d.Dia));
        Assert.Equal(
            [17, 18, 17, 18, 17],
            enviado.Dias.Take(5).Select(d => d.Tramos.Single().Fin!.Value.Hour));
        Assert.Empty(enviado.Dias[5].Tramos);
        Assert.Contains(SeccionSemanaTipo.Guardada, seccion.Find(".dsm-semana-tipo__guardado").TextContent, StringComparison.Ordinal);
        Assert.Equal("Miércoles 4 de noviembre", seccion.Find(".dsm-semana-tipo__fechas li").TextContent);
    }

    [Fact]
    public void Con_Citas_fuera_las_muestra_sin_cancelarlas_y_guarda_al_confirmar()
    {
        _vista.Responder = _ => new CambioDeBaseRevisado(
            [new(new CitaId(Guid.CreateVersion7()), new DateOnly(2026, 11, 9), new(17, 30), new(18, 0), "Ana López", true, false)],
            []);
        var seccion = Render<SeccionSemanaTipo>();
        seccion.Find("#monday-fin-0").Change("17:00:00");

        seccion.Find("form").Submit();

        Assert.Empty(_cambiar.Recibidos);
        Assert.Equal(SeccionSemanaTipo.NoSeCancelan, seccion.Find(".dsm-semana-tipo__fuera .dsm-citas-fuera__ayuda").TextContent);
        Assert.Equal("Lunes 9 de noviembre, 5:30 pm", seccion.Find(".dsm-citas-fuera__hora").TextContent);
        Assert.NotNull(seccion.Find(".dsm-citas-fuera__sin-correo"));
        Assert.Equal(SeccionSemanaTipo.GuardarDeTodosModos, seccion.Find(".dsm-semana-tipo__guardar").TextContent.Trim());

        seccion.Find("form").Submit();

        Assert.Single(_cambiar.Recibidos);
        Assert.Single(_vista.Pedidas);
    }

    [Fact]
    public void Cambiar_los_Tramos_despues_de_ver_la_lista_la_vuelve_a_pedir()
    {
        _vista.Responder = _ => new CambioDeBaseRevisado(
            [new(new CitaId(Guid.CreateVersion7()), new DateOnly(2026, 11, 9), new(17, 30), new(18, 0), "Ana López", false, false)],
            []);
        var seccion = Render<SeccionSemanaTipo>();
        seccion.Find("#monday-fin-0").Change("17:00:00");
        seccion.Find("form").Submit();

        seccion.Find("#monday-fin-0").Change("16:30:00");
        seccion.Find("form").Submit();

        Assert.Equal(2, _vista.Pedidas.Count);
        Assert.Empty(_cambiar.Recibidos);
    }

    [Fact]
    public void El_error_de_un_dia_va_en_ese_dia()
    {
        _cambiar.Responder = _ => Resultado.Invalido(CambiarAgendaBase.PropiedadDelDia(2), CambiarAgendaBase.MensajeDeTramosEncimados)
            .Como<AgendaBaseCambiada>();
        var seccion = Render<SeccionSemanaTipo>();

        seccion.Find("form").Submit();

        Assert.Equal(CambiarAgendaBase.MensajeDeTramosEncimados, Dia(seccion, "wednesday").QuerySelector(".dsm-campo__error")!.TextContent);
        Assert.Null(Dia(seccion, "monday").QuerySelector(".dsm-campo__error"));
        Assert.Empty(seccion.FindAll(".dsm-semana-tipo__guardado"));
    }

    private static AngleSharp.Dom.IElement Dia(IRenderedComponent<SeccionSemanaTipo> seccion, string dia) =>
        seccion.Find($"fieldset[data-dia='{dia}']");

    private static AngleSharp.Dom.IElement Casilla(IRenderedComponent<SeccionSemanaTipo> seccion, string dia) =>
        seccion.Find($".dsm-semana-tipo__destino-casilla[value='{dia}']");

    private sealed class SemanaFalsa : IQueryHandler<SemanaTipo, IReadOnlyList<DiaDeLaBase>>
    {
        public Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(SemanaTipo consulta, CancellationToken cancellationToken)
        {
            IReadOnlyList<DiaDeLaBase> semana =
            [
                .. Semana.Select(d => new DiaDeLaBase(
                    d, d is DayOfWeek.Saturday or DayOfWeek.Sunday ? [] : [new TramoDeLaAgenda(new(15, 30), new(18, 0))])),
            ];
            return Task.FromResult(Resultado.Exito(semana));
        }
    }

    private sealed class VistaPreviaFalsa : IQueryHandler<CambioDeAgendaBase, CambioDeBaseRevisado>
    {
        public List<CambioDeAgendaBase> Pedidas { get; } = [];

        public Func<CambioDeAgendaBase, CambioDeBaseRevisado> Responder { get; set; } = _ => new CambioDeBaseRevisado([], []);

        public Task<Resultado<CambioDeBaseRevisado>> HandleAsync(CambioDeAgendaBase consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            return Task.FromResult(Resultado.Exito(Responder(consulta)));
        }
    }

    private sealed class CambiarFalso : ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada>
    {
        public List<CambiarAgendaBase> Recibidos { get; } = [];

        public Func<CambiarAgendaBase, Resultado<AgendaBaseCambiada>> Responder { get; set; } =
            _ => Resultado.Exito(new AgendaBaseCambiada([DayOfWeek.Monday], []));

        public Task<Resultado<AgendaBaseCambiada>> HandleAsync(CambiarAgendaBase comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return Task.FromResult(Responder(comando));
        }
    }
}
