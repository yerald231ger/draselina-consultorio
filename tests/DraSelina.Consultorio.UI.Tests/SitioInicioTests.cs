using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Ubicación y horarios de Sitio · Inicio (entrada 7.1) con la Agenda base falsa: los grupos del horario y, con la
/// semana sin atención, el aviso en lugar de la lista.
/// </summary>
public sealed class SitioInicioTests : BunitContext
{
    private readonly HorarioFalso _horario = new();

    public SitioInicioTests()
    {
        Services.AddSingleton<IQueryHandler<HorarioDeAtencion, IReadOnlyList<DiaDeLaBase>>>(_horario);
        Services.AddSingleton<IContenidoApi>(new Contenido.ContenidoApi());
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

    private sealed class HorarioFalso : IQueryHandler<HorarioDeAtencion, IReadOnlyList<DiaDeLaBase>>
    {
        public IReadOnlyList<DiaDeLaBase> Semana { get; set; } = [];

        public Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(HorarioDeAtencion consulta, CancellationToken cancellationToken) =>
            Task.FromResult(Resultado.Exito(Semana));
    }
}
