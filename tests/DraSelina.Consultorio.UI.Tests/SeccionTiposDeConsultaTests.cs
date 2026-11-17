using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Los Tipos de consulta de Configuración (entrada 6.7) con consulta y comandos falsos: la lista editable, guardar un
/// cambio, desactivar con confirmación, el último activo, agregar uno y los errores bajo su campo.
/// </summary>
public sealed class SeccionTiposDeConsultaTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    private static readonly TipoDeVisitaId Primera = new(Guid.CreateVersion7());

    private static readonly TipoDeVisitaId Seguimiento = new(Guid.CreateVersion7());

    private readonly Comando<CrearTipoDeVisita> _crear = new();

    private readonly Comando<CambiarTipoDeVisita> _cambiar = new();

    private readonly Comando<DesactivarTipoDeVisita> _desactivar = new();

    private readonly Lista _lista = new();

    public SeccionTiposDeConsultaTests()
    {
        Services.AddSingleton<IQueryHandler<TiposDeVisitaEnConfiguracion, IReadOnlyList<TipoDeVisitaEditable>>>(_lista);
        Services.AddSingleton<ICommandHandler<CrearTipoDeVisita, TipoDeVisitaGuardado>>(_crear);
        Services.AddSingleton<ICommandHandler<CambiarTipoDeVisita, TipoDeVisitaGuardado>>(_cambiar);
        Services.AddSingleton<ICommandHandler<DesactivarTipoDeVisita, TipoDeVisitaGuardado>>(_desactivar);
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Lista_cada_tipo_con_sus_datos_y_el_formulario_para_agregar()
    {
        var seccion = Render<SeccionTiposDeConsulta>();

        var tipos = seccion.FindAll(".dsm-tipos__tipo");
        Assert.Equal(2, tipos.Count);
        Assert.Equal("Primera consulta", seccion.Find($"#{Id(Primera)}-nombre").GetAttribute("value"));
        Assert.Equal("30", seccion.Find($"#{Id(Primera)}-duracion").GetAttribute("value"));
        Assert.True(seccion.Find($"#{Id(Primera)}-en-linea").HasAttribute("checked"));
        Assert.Equal("3", seccion.Find("#nuevo-orden").GetAttribute("value"));
    }

    [Fact]
    public void Guardar_manda_el_cambio_y_dice_que_se_guardo()
    {
        var seccion = Render<SeccionTiposDeConsulta>();

        seccion.Find($"#{Id(Primera)}-duracion").Change("45");
        seccion.Find($"form[data-tipo='{Id(Primera)}']").Submit();

        var enviado = Assert.Single(_cambiar.Recibidos);
        Assert.Equal((Primera, "Primera consulta", 45, 1), (enviado.Id!.Value, enviado.Nombre, enviado.DuracionMin!.Value, enviado.Orden!.Value));
        Assert.Equal(AvisoDeSeccion.Guardada,
            seccion.Find($"form[data-tipo='{Id(Primera)}'] .dsm-seccion-de-configuracion__guardado").TextContent);
    }

    [Fact]
    public void Desactivar_pide_confirmar_y_el_ultimo_activo_lo_dice()
    {
        _desactivar.Responder = _ => Resultado.Fallo(CodigosDeAgenda.UltimoTipoActivo).Como<TipoDeVisitaGuardado>();
        var seccion = Render<SeccionTiposDeConsulta>();
        var formulario = $"form[data-tipo='{Id(Seguimiento)}']";

        seccion.Find($"{formulario} .dsm-tipos__desactivar").Click();
        Assert.Empty(_desactivar.Recibidos);
        seccion.Find($"{formulario} .dsm-tipos__confirmar").Click();

        Assert.Equal(Seguimiento, Assert.Single(_desactivar.Recibidos).Id);
        Assert.Equal(SeccionTiposDeConsulta.UltimoActivo, seccion.Find($"{formulario} .dsm-tipos__fallo").TextContent);
        Assert.Empty(seccion.FindAll($"{formulario} .dsm-seccion-de-configuracion__error"));
    }

    [Fact]
    public void Desactivar_lo_quita_de_la_lista_y_lo_avisa()
    {
        var seccion = Render<SeccionTiposDeConsulta>();
        _lista.Tipos = [.. _lista.Tipos.Where(t => t.Id != Seguimiento)];
        var formulario = $"form[data-tipo='{Id(Seguimiento)}']";

        seccion.Find($"{formulario} .dsm-tipos__desactivar").Click();
        seccion.Find($"{formulario} .dsm-tipos__confirmar").Click();

        Assert.Single(seccion.FindAll(".dsm-tipos__tipo"));
        Assert.Equal("Se desactivó Consulta de seguimiento.", seccion.Find(".dsm-tipos__desactivado").TextContent);
    }

    [Fact]
    public void Agregar_manda_el_tipo_nuevo_y_los_errores_van_bajo_su_campo()
    {
        _crear.Responder = _ => Resultado.Invalido(nameof(DatosDeTipoDeVisita.Nombre), DatosDeTipoDeVisita.MensajeDeNombre).Como<TipoDeVisitaGuardado>();
        var seccion = Render<SeccionTiposDeConsulta>();

        seccion.Find(".dsm-tipos__nuevo").Submit();
        Assert.Equal(DatosDeTipoDeVisita.MensajeDeNombre, seccion.Find("#error-nuevo-Nombre").TextContent);

        _crear.Responder = _ => Resultado.Exito(new TipoDeVisitaGuardado(new TipoDeVisitaId(Guid.CreateVersion7())));
        seccion.Find("#nuevo-nombre").Change("Infiltración");
        seccion.Find("#nuevo-duracion").Change("45");
        seccion.Find("#nuevo-en-linea").Change(false);
        seccion.Find(".dsm-tipos__nuevo").Submit();

        var enviado = _crear.Recibidos[^1];
        Assert.Equal(("Infiltración", 45, false, 3), (enviado.Nombre, enviado.DuracionMin!.Value, enviado.ReservableEnLinea, enviado.Orden!.Value));
    }

    private static string Id(TipoDeVisitaId id) => $"tipo-{id.Valor:N}";

    private sealed class Lista : IQueryHandler<TiposDeVisitaEnConfiguracion, IReadOnlyList<TipoDeVisitaEditable>>
    {
        public IReadOnlyList<TipoDeVisitaEditable> Tipos { get; set; } =
        [
            new(Primera, "Primera consulta", "Valoración", 30, true, 1),
            new(Seguimiento, "Consulta de seguimiento", null, 30, true, 2),
        ];

        public Task<Resultado<IReadOnlyList<TipoDeVisitaEditable>>> HandleAsync(TiposDeVisitaEnConfiguracion consulta, CancellationToken cancellationToken) =>
            Task.FromResult(Resultado.Exito(Tipos));
    }

    private sealed class Comando<TComando> : ICommandHandler<TComando, TipoDeVisitaGuardado>
        where TComando : ICommand<TipoDeVisitaGuardado>
    {
        public List<TComando> Recibidos { get; } = [];

        public Func<TComando, Resultado<TipoDeVisitaGuardado>> Responder { get; set; } = c => Resultado.Exito(new TipoDeVisitaGuardado(c switch
        {
            CambiarTipoDeVisita cambio => cambio.Id!.Value,
            DesactivarTipoDeVisita desactivar => desactivar.Id!.Value,
            _ => new TipoDeVisitaId(Guid.CreateVersion7()),
        }));

        public Task<Resultado<TipoDeVisitaGuardado>> HandleAsync(TComando comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return Task.FromResult(Responder(comando));
        }
    }
}
