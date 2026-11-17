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
/// Tramos, el comando que se manda, y los mensajes de cada rechazo (entrada 6.1); la lista de Citas que quedan fuera con
/// cancelar o conservar, el conflicto que la recarga, y quitar el horario propio (entrada 6.3). Hoy es la fecha local del
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

    private readonly VistaPreviaFalsa _vista = new();

    private readonly BorrarFalso _borrar = new();

    public CalendarioDeAtencionTests()
    {
        Services.AddSingleton<IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>>(_consulta);
        Services.AddSingleton<ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>>(_comando);
        Services.AddSingleton<IQueryHandler<CitasFueraDelHorario, HorarioConCitasFuera>>(_vista);
        Services.AddSingleton<ICommandHandler<BorrarAgendaCustom, AgendaCustomBorrada>>(_borrar);
        Services.AddSingleton<IClock>(new Reloj(Ahora));
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Sin_mes_pide_el_de_hoy_y_lo_pinta_de_lunes_a_domingo()
    {
        var calendario = Render<CalendarioDeAtencion>();

        Assert.Equal(new MesDeAtencion(2026, 11), Assert.Single(_consulta.Pedidas));
        Assert.Equal("Noviembre 2026", calendario.Find(".dsm-atencion__mes-titulo").TextContent);
        Assert.Equal(["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"], calendario.FindAll("thead th").Select(t => t.TextContent));
        // Noviembre de 2026 empieza en domingo: seis celdas vacías antes del 1.
        Assert.Equal(6, calendario.FindAll("tbody tr")[0].QuerySelectorAll(".dsm-atencion__dia--fuera").Length);
        Assert.Equal(30, calendario.FindAll("td[data-fecha]").Count);
        Assert.Equal(
            ["/admin/calendario?mes=2026-10", "/admin/calendario?mes=2026-12"],
            calendario.FindAll(".dsm-atencion__meses a").Select(a => a.GetAttribute("href")));
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

        Assert.Equal("Horario especial · cirugía", Celda(calendario, 10).QuerySelector(".dsm-atencion__marca")!.TextContent);
        Assert.Equal("No se atiende · vacaciones", Celda(calendario, 11).QuerySelector(".dsm-atencion__marca")!.TextContent);
        Assert.Equal("Día abierto · sábado abierto", Celda(calendario, 14).QuerySelector(".dsm-atencion__marca")!.TextContent);
        Assert.Equal("No se atiende", Celda(calendario, 15).QuerySelector(".dsm-atencion__marca")!.TextContent);
        Assert.Null(Celda(calendario, 12).QuerySelector(".dsm-atencion__marca"));
        Assert.Contains("dsm-atencion__dia--custom", Celda(calendario, 14).ClassName, StringComparison.Ordinal);
        Assert.Contains("dsm-atencion__dia--cerrado", Celda(calendario, 11).ClassName, StringComparison.Ordinal);
        Assert.Equal("10:00 am a 1:00 pm", Celda(calendario, 14).QuerySelector(".dsm-atencion__horas")!.TextContent);
    }

    [Fact]
    public void Los_dias_pasados_se_ven_sin_boton_y_hoy_se_puede_tocar()
    {
        var calendario = Render<CalendarioDeAtencion>();

        Assert.Null(Celda(calendario, 8).QuerySelector("button"));
        Assert.NotNull(Celda(calendario, 8).QuerySelector(".dsm-atencion__pasado"));
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
        Assert.Equal("10:00:00", calendario.Find("#inicio-0").GetAttribute("value"));
        Assert.Equal("13:00:00", calendario.Find("#fin-0").GetAttribute("value"));
        Assert.Equal("sábado abierto", calendario.Find("#motivo").GetAttribute("value"));
    }

    [Fact]
    public void Quitar_todos_los_Tramos_dice_que_no_se_atiende_y_guarda_sin_Tramos()
    {
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 11).QuerySelector("button")!.Click();

        calendario.Find(".dsm-atencion__quitar").Click();
        Assert.Contains(CalendarioDeAtencion.SinTramos, calendario.Find(".dsm-atencion__sin-tramos").TextContent, StringComparison.Ordinal);
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

        calendario.Find(".dsm-atencion__agregar").Click();
        calendario.Find("#fin-0").Change("17:00:00");
        calendario.Find("#inicio-1").Change("17:30:00");
        calendario.Find("#fin-1").Change("18:00:00");
        calendario.Find("#motivo").Change("junta");
        calendario.Find("form").Submit();

        var enviado = Assert.Single(_comando.Recibidos);
        Assert.Equal(
            [(new TimeOnly(15, 30), new TimeOnly(17, 0)), (new TimeOnly(17, 30), new TimeOnly(18, 0))],
            enviado.Tramos.Select(t => (t.Inicio!.Value, t.Fin!.Value)));
        Assert.Equal(2, _consulta.Pedidas.Count);
        Assert.Empty(calendario.FindAll("form"));
        Assert.Equal("Se guardó el horario propio del Martes 10 de noviembre.", calendario.Find(".dsm-atencion__guardado").TextContent);
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

        Assert.Equal(mensaje, calendario.Find(".dsm-atencion__error").TextContent.Trim());
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

    [Fact]
    public void Con_Citas_fuera_muestra_la_lista_antes_de_guardar_con_su_preseleccion()
    {
        _vista.Responder = _ => CuatroFuera();
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 14).QuerySelector("button")!.Click();
        calendario.Find("#motivo").Change("asunto personal");

        calendario.Find("form").Submit();

        Assert.Empty(_comando.Recibidos);
        var pedida = Assert.Single(_vista.Pedidas);
        Assert.Equal((Sabado, false), (pedida.Fecha, pedida.Quitar));
        Assert.Empty(pedida.Tramos);
        Assert.Equal(ListaDeCitasFuera.Titulo(4), calendario.Find(".dsm-citas-fuera__titulo").TextContent);
        var filas = calendario.FindAll(".dsm-citas-fuera__cita");
        Assert.Equal(
            ["10:00 am a 10:30 am", "10:30 am a 11:00 am", "11:00 am a 11:30 am", "12:30 pm a 1:00 pm"],
            filas.Select(f => f.QuerySelector(".dsm-citas-fuera__hora")!.TextContent));
        Assert.Equal("Bruno Díaz", filas[1].QuerySelector(".dsm-citas-fuera__paciente")!.TextContent);
        Assert.NotNull(filas[1].QuerySelector(".dsm-citas-fuera__sin-correo"));
        Assert.Null(filas[0].QuerySelector(".dsm-citas-fuera__sin-correo"));
        // En línea sin elegir; la del Consultorio (12:30) en conservar.
        Assert.All(filas.Take(3), f => Assert.Empty(f.QuerySelectorAll("input[checked]")));
        Assert.True(filas[3].QuerySelector(".dsm-citas-fuera__conservar")!.HasAttribute("checked"));
    }

    [Fact]
    public void Sin_elegir_todas_no_manda_nada_y_lo_dice()
    {
        _vista.Responder = _ => CuatroFuera();
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 14).QuerySelector("button")!.Click();
        calendario.Find("#motivo").Change("asunto personal");
        calendario.Find("form").Submit();

        calendario.Find("form").Submit();

        Assert.Empty(_comando.Recibidos);
        Assert.Equal(ListaDeCitasFuera.FaltaElegirTexto, calendario.Find(".dsm-citas-fuera__falta").TextContent);
    }

    [Fact]
    public void Cancelar_todas_y_conservar_la_de_las_12_30_manda_tres_y_una()
    {
        _vista.Responder = _ => CuatroFuera();
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 14).QuerySelector("button")!.Click();
        calendario.Find("#motivo").Change("asunto personal");
        calendario.Find("form").Submit();

        calendario.Find(".dsm-citas-fuera__todas").Click();
        calendario.FindAll(".dsm-citas-fuera__conservar")[3].Change("conservar");
        calendario.Find("form").Submit();

        var enviado = Assert.Single(_comando.Recibidos);
        Assert.Equal(
            [(Citas[0], true), (Citas[1], true), (Citas[2], true), (Citas[3], false)],
            enviado.Citas.Select(c => (c.CitaId!.Value, c.Cancelar)));
        Assert.Single(_vista.Pedidas);
        Assert.Equal(
            "Se guardó el horario propio del Sábado 14 de noviembre. Las 3 citas elegidas se cancelan en unos instantes y sus pacientes reciben la disculpa.",
            calendario.Find(".dsm-atencion__guardado").TextContent);
    }

    [Fact]
    public void Un_conflicto_recarga_la_lista_y_conserva_lo_elegido()
    {
        _vista.Responder = _ => CuatroFuera();
        _comando.Responder = _ => Resultado.Fallo(CodigosDeAgenda.CitasFuera).Como<AgendaCustomGuardada>();
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 14).QuerySelector("button")!.Click();
        calendario.Find("#motivo").Change("asunto personal");
        calendario.Find("form").Submit();
        calendario.Find(".dsm-citas-fuera__todas").Click();
        var nueva = new CitaFueraDelHorario(new CitaId(Guid.CreateVersion7()), new(11, 30), new(12, 0), "Carla Ruiz", false, false);
        _vista.Responder = _ => CuatroFuera() with { Citas = [.. CuatroFuera().Citas, nueva] };

        calendario.Find("form").Submit();

        Assert.Equal(CalendarioDeAtencion.CitasFuera, calendario.Find(".dsm-atencion__error").TextContent.Trim());
        Assert.Equal(2, _vista.Pedidas.Count);
        var filas = calendario.FindAll(".dsm-citas-fuera__cita");
        Assert.Equal(5, filas.Count);
        Assert.All(filas.Take(4), f => Assert.True(f.QuerySelector(".dsm-citas-fuera__cancelar")!.HasAttribute("checked")));
        Assert.Empty(filas[4].QuerySelectorAll("input[checked]"));
    }

    [Fact]
    public void Quitar_el_horario_propio_dice_a_que_Tramos_vuelve_y_borra()
    {
        _consulta.Personalizar = d => d.Fecha.Day == 11
            ? d with { Tipo = TipoDeDia.CustomSinAtencion, MotivoDeCustom = "vacaciones", Tramos = [] }
            : d;
        _vista.Responder = c => new HorarioConCitasFuera([Tramo(15, 30, 18, 0)], []);
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 11).QuerySelector("button")!.Click();

        calendario.Find(".dsm-atencion__quitar-custom").Click();

        Assert.Equal(CitasFueraDelHorario.AlQuitar(new DateOnly(2026, 11, 11)), Assert.Single(_vista.Pedidas));
        Assert.Equal("El día vuelve a la semana tipo: 3:30 pm a 6:00 pm.", calendario.Find(".dsm-atencion__vuelve").TextContent);
        Assert.Empty(calendario.FindAll(".dsm-citas-fuera"));

        calendario.Find(".dsm-atencion__confirmar-quitar").Click();

        var borrado = Assert.Single(_borrar.Recibidos);
        Assert.Equal(new DateOnly(2026, 11, 11), borrado.Fecha);
        Assert.Empty(borrado.Citas);
        Assert.Equal("El Miércoles 11 de noviembre volvió a la semana tipo.", calendario.Find(".dsm-atencion__guardado").TextContent);
    }

    [Fact]
    public void Quitar_con_Citas_fuera_pide_la_misma_eleccion()
    {
        _consulta.Personalizar = d => d.Fecha == Sabado
            ? d with { Tipo = TipoDeDia.DiaAbierto, MotivoDeCustom = "sábado abierto", Tramos = [Tramo(10, 0, 13, 0)] }
            : d;
        _vista.Responder = _ => new HorarioConCitasFuera([], CuatroFuera().Citas.Take(2).ToList());
        var calendario = Render<CalendarioDeAtencion>();
        Celda(calendario, 14).QuerySelector("button")!.Click();
        calendario.Find(".dsm-atencion__quitar-custom").Click();
        Assert.Equal("El día vuelve a la semana tipo: ese día no se atiende.", calendario.Find(".dsm-atencion__vuelve").TextContent);

        calendario.Find(".dsm-atencion__confirmar-quitar").Click();
        Assert.Empty(_borrar.Recibidos);
        Assert.NotNull(calendario.Find(".dsm-citas-fuera__falta"));

        calendario.FindAll(".dsm-citas-fuera__cancelar")[0].Change("cancelar");
        calendario.FindAll(".dsm-citas-fuera__conservar")[1].Change("conservar");
        calendario.Find(".dsm-atencion__confirmar-quitar").Click();

        Assert.Equal([(Citas[0], true), (Citas[1], false)], Assert.Single(_borrar.Recibidos).Citas.Select(c => (c.CitaId!.Value, c.Cancelar)));
    }

    [Fact]
    public void Un_dia_sin_custom_no_ofrece_quitar_y_los_tres_tipos_llevan_su_clase()
    {
        _consulta.Personalizar = d => d.Fecha.Day switch
        {
            10 => d with { Tipo = TipoDeDia.HorarioEspecial, MotivoDeCustom = "cirugía", Tramos = [Tramo(15, 30, 17, 0)] },
            11 => d with { Tipo = TipoDeDia.CustomSinAtencion, MotivoDeCustom = "vacaciones", Tramos = [] },
            14 => d with { Tipo = TipoDeDia.DiaAbierto, MotivoDeCustom = "sábado abierto", Tramos = [Tramo(10, 0, 13, 0)] },
            _ => d,
        };
        var calendario = Render<CalendarioDeAtencion>();

        Assert.Contains("dsm-atencion__dia--especial", Celda(calendario, 10).ClassName, StringComparison.Ordinal);
        Assert.Contains("dsm-atencion__dia--no-se-atiende", Celda(calendario, 11).ClassName, StringComparison.Ordinal);
        Assert.Contains("dsm-atencion__dia--abierto", Celda(calendario, 14).ClassName, StringComparison.Ordinal);
        Assert.DoesNotContain("dsm-atencion__dia--", Celda(calendario, 12).ClassName!.Replace("dsm-atencion__dia--hoy", ""), StringComparison.Ordinal);

        Celda(calendario, 12).QuerySelector("button")!.Click();
        Assert.Empty(calendario.FindAll(".dsm-atencion__quitar-custom"));
    }

    private static readonly CitaId[] Citas = [.. Enumerable.Range(0, 4).Select(_ => new CitaId(Guid.CreateVersion7()))];

    // Cuatro Citas del sábado: tres en línea (la de las 10:30 sin correo) y la de las 12:30 del Consultorio.
    private static HorarioConCitasFuera CuatroFuera() => new(
        [],
        [
            new(Citas[0], new(10, 0), new(10, 30), "Ana López", false, false),
            new(Citas[1], new(10, 30), new(11, 0), "Bruno Díaz", true, false),
            new(Citas[2], new(11, 0), new(11, 30), "Carmen Soto", false, false),
            new(Citas[3], new(12, 30), new(13, 0), "Diego Paz", false, true),
        ]);

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

    private sealed class VistaPreviaFalsa : IQueryHandler<CitasFueraDelHorario, HorarioConCitasFuera>
    {
        public List<CitasFueraDelHorario> Pedidas { get; } = [];

        /// <summary>Sin Citas fuera, con los mismos Tramos.</summary>
        public Func<CitasFueraDelHorario, HorarioConCitasFuera> Responder { get; set; } = c => new HorarioConCitasFuera(c.Tramos, []);

        public Task<Resultado<HorarioConCitasFuera>> HandleAsync(CitasFueraDelHorario consulta, CancellationToken cancellationToken)
        {
            Pedidas.Add(consulta);
            return Task.FromResult(Resultado.Exito(Responder(consulta)));
        }
    }

    private sealed class BorrarFalso : ICommandHandler<BorrarAgendaCustom, AgendaCustomBorrada>
    {
        public List<BorrarAgendaCustom> Recibidos { get; } = [];

        public Task<Resultado<AgendaCustomBorrada>> HandleAsync(BorrarAgendaCustom comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return Task.FromResult(Resultado.Exito(new AgendaCustomBorrada(comando.Fecha!.Value)));
        }
    }

    private sealed class Reloj(DateTimeOffset ahora) : IClock
    {
        public DateTimeOffset UtcNow { get; } = ahora;
    }
}
