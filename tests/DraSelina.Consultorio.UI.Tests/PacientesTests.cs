using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
// Las páginas y las consultas se llaman como los módulos: los alias evitan los choques.
using ConsultaDelHistorial = DraSelina.Consultorio.Agenda.Contracts.HistorialDelPaciente;
using PaginaAgenda = DraSelina.Consultorio.UI.Pages.Administracion.Agenda;
using PaginaDelHistorial = DraSelina.Consultorio.UI.Pages.Administracion.HistorialDelPaciente;
using PaginaDePacientes = DraSelina.Consultorio.UI.Pages.Administracion.Pacientes;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Pacientes: búsqueda e historial (entrada 5.7) con las dos consultas falsas, en interactivo: la lista con y sin texto,
/// sin coincidencias, más de 50, sin correo, la búsqueda que el circuito recuerda sin ponerla en la dirección, y el historial con sus estados, su resumen, sin
/// Citas, sin correo y el Paciente que no existe. "Sin sesión" y la navegación del layout están en las de integración (la
/// redirección la hace el host y el layout se pinta en SSR). Hoy es el lunes 9 de noviembre de 2026, 4:10 pm en Ciudad
/// de México.
/// </summary>
public sealed class PacientesTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 22, 10, 0, TimeSpan.Zero);

    private static readonly PatientId Mariana = PatientId.Nuevo();

    private static readonly PatientId Perez = PatientId.Nuevo();

    private static readonly CitaId Proxima = CitaId.Nuevo();

    private static readonly CitaId Tardia = CitaId.Nuevo();

    private static readonly CitaId DelAnioPasado = CitaId.Nuevo();

    private readonly BusquedaFalsa _busqueda = new();
    private readonly HistorialFalso _historial = new();

    public PacientesTests()
    {
        Services.AddSingleton<IQueryHandler<BuscarPacientes, PacientesEncontrados>>(_busqueda);
        Services.AddSingleton<IQueryHandler<ConsultaDelHistorial, PacienteConHistorial>>(_historial);
        Services.AddSingleton<IClock>(new Reloj(Ahora));
        Services.AddScoped<BusquedaRecordada>();
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Sin_texto_pide_todos_y_cada_fila_lleva_nombre_al_historial_telefono_y_sin_correo_si_no_tiene()
    {
        _busqueda.Responde = _ => Dos();

        var pagina = Render<PaginaDePacientes>();

        Assert.Equal(new BuscarPacientes(""), Assert.Single(_busqueda.Pedidas));
        Assert.Equal(
            [
                ($"/admin/pacientes/{Mariana.Valor}", "Mariana López", "444 555 1001", false),
                ($"/admin/pacientes/{Perez.Valor}", "MARIANA Pérez", "444 555 1002", true),
            ],
            pagina.FindAll(".dsm-pacientes__paciente").Select(fila => (
                fila.QuerySelector("a.dsm-pacientes__abrir")!.GetAttribute("href"),
                fila.QuerySelector("a.dsm-pacientes__abrir")!.TextContent,
                fila.QuerySelector(".dsm-pacientes__telefono")!.TextContent,
                fila.QuerySelector(".dsm-pacientes__sin-correo") is not null)));
        var sinCorreo = pagina.Find($"[data-paciente='{Perez.Valor}'] .dsm-pacientes__sin-correo");
        Assert.Equal(PaginaAgenda.SinCorreo, sinCorreo.TextContent);
        Assert.Contains("dsm-chip-de-estado--neutro", sinCorreo.ClassName, StringComparison.Ordinal);
        Assert.Empty(pagina.FindAll(".dsm-pacientes__hay-mas"));
        Assert.Empty(pagina.FindAll(".dsm-pacientes__aviso"));
    }

    [Fact]
    public void Buscar_pide_esa_busqueda_sin_navegar_y_el_circuito_la_recuerda()
    {
        _busqueda.Responde = _ => Dos();
        var pagina = Render<PaginaDePacientes>();
        var navegacion = Services.GetRequiredService<NavigationManager>();
        var antes = navegacion.Uri;

        pagina.Find("#q").Change("  López ");
        pagina.Find("form").Submit();

        Assert.Equal(new BuscarPacientes("López"), _busqueda.Pedidas[^1]);
        Assert.Equal(antes, navegacion.Uri);
        Assert.Equal("López", Services.GetRequiredService<BusquedaRecordada>().Texto);
    }

    [Fact]
    public void Al_volver_del_historial_la_lista_muestra_la_misma_busqueda_y_sus_resultados()
    {
        _busqueda.Responde = c => c.Texto == "mariana" ? Dos() : Dos() with { Pacientes = [] };
        var lista = Render<PaginaDePacientes>();
        lista.Find("#q").Change("mariana");
        lista.Find("form").Submit();
        Assert.Equal(2, lista.FindAll(".dsm-pacientes__paciente").Count);

        // Al historial y de vuelta: la página de Pacientes se crea de nuevo en el mismo circuito.
        _historial.Responde = _ => Historial();
        var historial = AbrirHistorial(Mariana);
        Assert.Equal("/admin/pacientes", historial.Find(".dsm-volver").GetAttribute("href"));
        lista.Dispose();
        var deVuelta = Render<PaginaDePacientes>();

        Assert.Equal("mariana", deVuelta.Find("#q").GetAttribute("value"));
        Assert.Equal(new BuscarPacientes("mariana"), _busqueda.Pedidas[^1]);
        Assert.Equal(
            ["Mariana López", "MARIANA Pérez"],
            deVuelta.FindAll(".dsm-pacientes__abrir").Select(a => a.TextContent));
    }

    [Fact]
    public void Un_circuito_nuevo_empieza_sin_busqueda()
    {
        _busqueda.Responde = _ => Dos();

        var pagina = Render<PaginaDePacientes>();

        Assert.Equal(new BuscarPacientes(""), Assert.Single(_busqueda.Pedidas));
        Assert.True(string.IsNullOrEmpty(pagina.Find("#q").GetAttribute("value")));
    }

    [Fact]
    public void El_texto_buscado_no_aparece_en_ningun_enlace_ni_en_la_direccion()
    {
        _busqueda.Responde = _ => Dos() with { HayMas = true };
        _historial.Responde = _ => Historial();
        var lista = Render<PaginaDePacientes>();
        lista.Find("#q").Change("mariana");
        lista.Find("form").Submit();
        var historial = AbrirHistorial(Mariana);

        Assert.DoesNotContain("q=", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("q=", lista.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("q=", historial.Markup, StringComparison.Ordinal);
        Assert.All(
            lista.FindAll("a").Concat(historial.FindAll("a")).Select(a => a.GetAttribute("href") ?? ""),
            href => Assert.DoesNotContain("?", href, StringComparison.Ordinal));

        var formulario = lista.Find("form");
        Assert.Null(formulario.GetAttribute("action"));
        Assert.Null(formulario.GetAttribute("method"));
        Assert.Null(lista.Find("#q").GetAttribute("name"));
    }

    [Fact]
    public void Una_busqueda_sin_coincidencias_lo_dice()
    {
        _busqueda.Responde = c => c.Texto == "" ? Dos() : new PacientesEncontrados([], HayMas: false);
        var pagina = Render<PaginaDePacientes>();

        pagina.Find("#q").Change("zzz");
        pagina.Find("form").Submit();

        Assert.Equal(PaginaDePacientes.NoEncontramos, pagina.Find(".dsm-pacientes__aviso").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-pacientes__lista"));
    }

    [Fact]
    public void Sin_texto_y_sin_Pacientes_dice_que_todavia_no_hay()
    {
        _busqueda.Responde = _ => new PacientesEncontrados([], HayMas: false);

        var pagina = Render<PaginaDePacientes>();

        Assert.Equal(PaginaDePacientes.SinPacientes, pagina.Find(".dsm-pacientes__aviso").TextContent);
    }

    [Theory]
    [InlineData("", PaginaDePacientes.EscribeUnNombre)]
    [InlineData("paciente", PaginaDePacientes.AcotaLaBusqueda)]
    public void Con_mas_de_los_que_caben_lo_dice_bajo_la_lista(string texto, string mensaje)
    {
        Services.GetRequiredService<BusquedaRecordada>().Texto = texto;
        _busqueda.Responde = _ => Dos() with { HayMas = true };

        var pagina = Render<PaginaDePacientes>();

        Assert.Equal(mensaje, pagina.Find(".dsm-pacientes__hay-mas").TextContent);
        Assert.Equal(2, pagina.FindAll(".dsm-pacientes__paciente").Count);
    }

    [Fact]
    public void Si_la_busqueda_no_vuelve_con_exito_lo_dice()
    {
        var pagina = Render<PaginaDePacientes>();

        Assert.Equal(PaginaDePacientes.NoSeCargo, pagina.Find(".dsm-pacientes__aviso").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-pacientes__lista"));
    }

    [Fact]
    public void El_historial_muestra_contacto_resumen_y_cada_Cita_con_su_estado_y_el_enlace_a_su_Detalle()
    {
        _historial.Responde = _ => Historial();

        var pagina = AbrirHistorial(Mariana);

        Assert.Equal(new ConsultaDelHistorial(Mariana), Assert.Single(_historial.Pedidas));
        Assert.Equal("Mariana López", pagina.Find("h1").TextContent);
        Assert.Equal("3 citas · 2 cancelaciones tardías", pagina.Find(".dsm-historial__resumen").TextContent);
        Assert.Equal("444 555 1001", pagina.Find("[data-dato=telefono] a").TextContent);
        Assert.Equal("tel:+524445551001", pagina.Find("[data-dato=telefono] a").GetAttribute("href"));
        Assert.Equal("mariana@correo.com", pagina.Find("[data-dato=correo] dd").TextContent);
        Assert.Equal(
            [
                ($"/admin/citas/{Proxima.Valor}", "Martes 10 de noviembre, 4:00 pm", "Primera consulta · En línea", "Agendada", "dsm-chip-de-estado"),
                ($"/admin/citas/{Tardia.Valor}", "Lunes 2 de noviembre, 10:00 am", "Revisión · Del consultorio", "Cancelada (tardía)", "dsm-chip-de-estado dsm-chip-de-estado--tardia"),
                ($"/admin/citas/{DelAnioPasado.Valor}", "Lunes 3 de noviembre de 2025, 9:30 am", "Tipo no encontrado · En línea", "Cancelada (tardía)", "dsm-chip-de-estado dsm-chip-de-estado--tardia"),
            ],
            pagina.FindAll(".dsm-historial__cita").Select(cita => (
                cita.QuerySelector("a.dsm-historial__abrir")!.GetAttribute("href"),
                cita.QuerySelector("a.dsm-historial__abrir")!.TextContent,
                cita.QuerySelector(".dsm-historial__tipo")!.TextContent,
                cita.QuerySelector(".dsm-historial__estado")!.TextContent,
                cita.QuerySelector(".dsm-historial__estado")!.ClassName!.Replace(" dsm-historial__estado", "", StringComparison.Ordinal))));
        var volver = pagina.Find(".dsm-volver");
        Assert.Equal("/admin/pacientes", volver.GetAttribute("href"));
        Assert.Contains(PaginaDelHistorial.VolverAPacientes, volver.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EstadoEnLaAgenda.Atendida, null, "Atendida", "dsm-chip-de-estado")]
    [InlineData(EstadoEnLaAgenda.NoSePresento, null, "No se presentó", "dsm-chip-de-estado dsm-chip-de-estado--neutro")]
    [InlineData(EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElPaciente, "Cancelada (por el paciente)", "dsm-chip-de-estado dsm-chip-de-estado--neutro")]
    [InlineData(EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio, "Cancelada (por el consultorio)", "dsm-chip-de-estado dsm-chip-de-estado--neutro")]
    public void Cada_estado_lleva_el_chip_de_la_Agenda_y_el_ambar_solo_la_tardia(
        EstadoEnLaAgenda estado, CancelacionEnLaAgenda? cancelacion, string etiqueta, string clase)
    {
        _historial.Responde = _ => Historial() with
        {
            Citas = [new CitaDelHistorial(Proxima, new DateOnly(2026, 11, 2), new TimeOnly(16, 0), "Revisión", OrigenEnLaAgenda.EnLinea, estado, cancelacion)],
        };

        var pagina = AbrirHistorial(Mariana);

        var chip = pagina.Find(".dsm-historial__estado");
        Assert.Equal(etiqueta, chip.TextContent);
        Assert.Equal($"{clase} dsm-historial__estado", chip.ClassName);
    }

    [Fact]
    public void Sin_correo_lo_dice_con_la_etiqueta_de_la_Agenda()
    {
        _historial.Responde = _ => Historial() with { Paciente = new ContactoDelPaciente("MARIANA Pérez", "+524445551002", null) };

        var pagina = AbrirHistorial(Mariana);

        Assert.Equal(PaginaAgenda.SinCorreo, pagina.Find("[data-dato=correo] .dsm-historial__sin-correo").TextContent);
    }

    [Fact]
    public void Un_Paciente_sin_Citas_lo_dice_sin_resumen()
    {
        _historial.Responde = _ => Historial() with { Resumen = new ResumenDelHistorial(0, 0, 0), Citas = [] };

        var pagina = AbrirHistorial(Mariana);

        Assert.Equal("Mariana López", pagina.Find("h1").TextContent);
        Assert.Equal(PaginaDelHistorial.SinCitas, pagina.Find(".dsm-historial__vacio").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-historial__resumen"));
        Assert.Empty(pagina.FindAll(".dsm-historial__citas"));
    }

    [Fact]
    public void Un_Paciente_que_no_existe_lo_dice_con_el_enlace_a_Pacientes()
    {
        _historial.Codigo = CodigosDeAgenda.PacienteNoEncontrado;

        var pagina = AbrirHistorial(PatientId.Nuevo());

        Assert.Equal(PaginaDelHistorial.NoExiste, pagina.Find("h1").TextContent);
        Assert.Equal("/admin/pacientes", pagina.Find(".dsm-volver").GetAttribute("href"));
        Assert.Empty(pagina.FindAll(".dsm-detalle__datos"));
    }

    [Fact]
    public void Si_el_historial_no_vuelve_con_exito_lo_dice()
    {
        var pagina = AbrirHistorial(Mariana);

        Assert.Equal(PaginaDelHistorial.NoSeCargo, pagina.Find(".dsm-detalle__no-cargo").TextContent);
        Assert.Empty(pagina.FindAll(".dsm-detalle__datos"));
    }

    [Theory]
    [InlineData(6, 1, 1, "6 citas · 1 no se presentó · 1 cancelación tardía")]
    [InlineData(5, 1, 2, "5 citas · 1 no se presentó · 2 cancelaciones tardías")]
    [InlineData(4, 2, 0, "4 citas · 2 no se presentó")]
    [InlineData(3, 0, 1, "3 citas · 1 cancelación tardía")]
    [InlineData(1, 0, 0, "1 cita")]
    [InlineData(0, 0, 0, null)]
    public void El_resumen_omite_las_partes_en_cero(int citas, int noSePresento, int tardias, string? esperado)
    {
        Assert.Equal(esperado, PaginaDelHistorial.Resumen(new ResumenDelHistorial(citas, noSePresento, tardias)));
    }

    [Fact]
    public void La_direccion_del_historial_lleva_solo_el_id_del_Paciente()
    {
        Assert.Equal($"/admin/pacientes/{Mariana.Valor}", PaginaDelHistorial.Direccion(Mariana));
    }

    private IRenderedComponent<PaginaDelHistorial> AbrirHistorial(PatientId paciente) =>
        Render<PaginaDelHistorial>(p => p.Add(x => x.Id, paciente.Valor));

    private static PacientesEncontrados Dos() => new(
        [
            new PacienteEncontrado(Mariana, "Mariana López", "+524445551001", TieneCorreo: true),
            new PacienteEncontrado(Perez, "MARIANA Pérez", "+524445551002", TieneCorreo: false),
        ],
        HayMas: false);

    private static PacienteConHistorial Historial() => new(
        Mariana,
        new ContactoDelPaciente("Mariana López", "+524445551001", "mariana@correo.com"),
        new ResumenDelHistorial(3, 0, 2),
        [
            new CitaDelHistorial(Proxima, new DateOnly(2026, 11, 10), new TimeOnly(16, 0), "Primera consulta", OrigenEnLaAgenda.EnLinea, EstadoEnLaAgenda.Agendada, null),
            new CitaDelHistorial(Tardia, new DateOnly(2026, 11, 2), new TimeOnly(10, 0), "Revisión", OrigenEnLaAgenda.DelConsultorio, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia),
            new CitaDelHistorial(DelAnioPasado, new DateOnly(2025, 11, 3), new TimeOnly(9, 30), null, OrigenEnLaAgenda.EnLinea, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia),
        ]);

    private sealed class BusquedaFalsa : IQueryHandler<BuscarPacientes, PacientesEncontrados>
    {
        /// <summary>Lo que devuelve; nulo, no autorizado.</summary>
        public Func<BuscarPacientes, PacientesEncontrados?> Responde { get; set; } = _ => null;

        public List<BuscarPacientes> Pedidas { get; } = [];

        public Task<Resultado<PacientesEncontrados>> HandleAsync(BuscarPacientes consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            return Task.FromResult(Responde(consulta) is { } encontrados
                ? Resultado.Exito(encontrados)
                : Resultado.NoAutorizado().Como<PacientesEncontrados>());
        }
    }

    private sealed class HistorialFalso : IQueryHandler<ConsultaDelHistorial, PacienteConHistorial>
    {
        /// <summary>El historial que devuelve; nulo, un fallo con <see cref="Codigo"/> o no autorizado si no hay código.</summary>
        public Func<ConsultaDelHistorial, PacienteConHistorial?> Responde { get; set; } = _ => null;

        public string? Codigo { get; set; }

        public List<ConsultaDelHistorial> Pedidas { get; } = [];

        public Task<Resultado<PacienteConHistorial>> HandleAsync(ConsultaDelHistorial consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            var historial = Responde(consulta);
            return Task.FromResult(
                historial is not null ? Resultado.Exito(historial)
                : Codigo is not null ? Resultado.Fallo(Codigo).Como<PacienteConHistorial>()
                : Resultado.NoAutorizado().Como<PacienteConHistorial>());
        }
    }

    private sealed class Reloj(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow { get; } = ahora;
    }
}
