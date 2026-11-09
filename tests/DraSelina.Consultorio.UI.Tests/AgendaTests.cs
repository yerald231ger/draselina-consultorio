using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
// La página se llama como el módulo: el alias evita el choque con el namespace DraSelina.Consultorio.Agenda.
using PaginaAgenda = DraSelina.Consultorio.UI.Pages.Administracion.Agenda;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// La Agenda del Administrador (entrada 5.1) con una consulta falsa: la matriz de estados del plan, en orden, con
/// nombre y motivo; el día vacío; el Paciente que no existe. Pide la fecha local de hoy, del reloj de la aplicación.
/// </summary>
public sealed class AgendaTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    /// <summary>Lunes 9 de noviembre de 2026, 4:10 pm en Ciudad de México (UTC-6).</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 22, 10, 0, TimeSpan.Zero);

    private readonly ConsultaFalsa _consulta = new();

    public AgendaTests()
    {
        Services.AddSingleton<IQueryHandler<CitasDeLaFecha, IReadOnlyList<CitaDeLaFecha>>>(_consulta);
        Services.AddSingleton<IClock>(new RelojFijo(Ahora));
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Pide_las_Citas_de_la_fecha_local_de_hoy()
    {
        _consulta.Citas = [];

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(new DateOnly(2026, 11, 9), Assert.Single(_consulta.Fechas));
        Assert.Contains("Hoy, Lunes 9 de noviembre", agenda.Find(".dsm-agenda__fecha").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Cada_estado_lleva_su_etiqueta_en_orden_con_nombre_y_motivo()
    {
        _consulta.Citas =
        [
            Cita(15, 30, "Bruno Díaz", null, EstadoEnLaAgenda.NoSePresento),
            Cita(16, 0, "Ana Sofía López", "Dolor de rodilla", EstadoEnLaAgenda.Atendida),
            Cita(17, 0, "Bruno Díaz", "Revisión de hombro", EstadoEnLaAgenda.Agendada),
            Cita(17, 30, "Ana Sofía López", "Esguince", EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia),
        ];

        var agenda = Render<PaginaAgenda>();
        var filas = agenda.FindAll(".dsm-agenda__cita");

        Assert.Equal(
            [
                ("3:30 pm", "Bruno Díaz", "No se presentó"),
                ("4:00 pm", "Ana Sofía López", "Atendida"),
                ("5:00 pm", "Bruno Díaz", "Agendada"),
                ("5:30 pm", "Ana Sofía López", "Cancelada (tardía)"),
            ],
            filas.Select(f => (
                f.QuerySelector(".dsm-agenda__inicio")!.TextContent,
                f.QuerySelector(".dsm-agenda__paciente")!.TextContent,
                f.QuerySelector(".dsm-chip-de-estado")!.TextContent)));
        Assert.Null(filas[0].QuerySelector(".dsm-agenda__motivo"));
        Assert.Equal("Dolor de rodilla", filas[1].QuerySelector(".dsm-agenda__motivo")!.TextContent);
        Assert.Contains("dsm-agenda__cita--atendida", filas[1].ClassName, StringComparison.Ordinal);
        Assert.Contains("dsm-agenda__cita--cancelada", filas[3].ClassName, StringComparison.Ordinal);
        Assert.Empty(agenda.FindAll(".dsm-agenda__vacia"));
    }

    [Theory]
    [InlineData(CancelacionEnLaAgenda.PorElPaciente, "Cancelada (por el paciente)")]
    [InlineData(CancelacionEnLaAgenda.Tardia, "Cancelada (tardía)")]
    [InlineData(CancelacionEnLaAgenda.PorElConsultorio, "Cancelada (por el consultorio)")]
    public void Las_tres_cancelaciones_se_distinguen(CancelacionEnLaAgenda cancelacion, string etiqueta)
    {
        _consulta.Citas = [Cita(17, 0, "Ana Sofía López", "Dolor", EstadoEnLaAgenda.Cancelada, cancelacion)];

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(etiqueta, agenda.Find(".dsm-chip-de-estado").TextContent);
    }

    [Fact]
    public void Un_dia_sin_Citas_lo_dice()
    {
        _consulta.Citas = [];

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(PaginaAgenda.DiaVacio, agenda.Find(".dsm-agenda__vacia").TextContent);
        Assert.Empty(agenda.FindAll(".dsm-agenda__cita"));
    }

    [Fact]
    public void Una_Cita_sin_Paciente_se_lista_como_Paciente_no_encontrado()
    {
        _consulta.Citas = [Cita(17, 0, null, "Dolor", EstadoEnLaAgenda.Agendada)];

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(PaginaAgenda.PacienteNoEncontrado, agenda.Find(".dsm-agenda__paciente").TextContent);
    }

    [Fact]
    public void Si_la_consulta_no_vuelve_con_exito_no_dice_que_el_dia_esta_vacio()
    {
        _consulta.Citas = null;

        var agenda = Render<PaginaAgenda>();

        Assert.Equal(PaginaAgenda.NoSeCargo, agenda.Find(".dsm-agenda__vacia").TextContent);
        Assert.DoesNotContain(PaginaAgenda.DiaVacio, agenda.Markup, StringComparison.Ordinal);
    }

    private static CitaDeLaFecha Cita(
        int hora, int minuto, string? paciente, string? motivo, EstadoEnLaAgenda estado, CancelacionEnLaAgenda? cancelacion = null) =>
        new(CitaId.Nuevo(), new TimeOnly(hora, minuto), new TimeOnly(hora, minuto).AddMinutes(30), paciente, motivo, estado, cancelacion);

    private sealed class ConsultaFalsa : IQueryHandler<CitasDeLaFecha, IReadOnlyList<CitaDeLaFecha>>
    {
        /// <summary>Lo que devuelve; nulo, no autorizado.</summary>
        public IReadOnlyList<CitaDeLaFecha>? Citas { get; set; }

        public List<DateOnly> Fechas { get; } = [];

        public Task<Resultado<IReadOnlyList<CitaDeLaFecha>>> HandleAsync(CitasDeLaFecha consulta, CancellationToken cancellationToken)
        {
            Fechas.Add(consulta.Fecha);
            return Task.FromResult(Citas is { } citas
                ? Resultado.Exito(citas)
                : Resultado.NoAutorizado().Como<IReadOnlyList<CitaDeLaFecha>>());
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow => ahora;
    }
}
