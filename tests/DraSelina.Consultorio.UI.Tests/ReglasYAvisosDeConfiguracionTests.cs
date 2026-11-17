using Bunit;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Contracts;
using DraSelina.Consultorio.UI.Pages.Administracion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Las secciones "Reglas de reserva" y "Avisos por correo" de Configuración (entrada 6.6) con consultas y comandos
/// falsos: cargar los valores guardados, mandar lo editado, "Se guardó." y los errores bajo su campo.
/// </summary>
public sealed class ReglasYAvisosDeConfiguracionTests : BunitContext
{
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    private readonly Comando<CambiarReglasDeReserva, ReglasDeReservaValores> _reglas = new(c => new ReglasDeReservaValores(
        c.AnticipacionMinimaDias!.Value, c.VentanaMeses!.Value, c.UmbralCancelacionTardiaMin!.Value, c.TopeSemanalPorTelefono!.Value));

    private readonly Comando<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores> _envio = new(c => new ConfiguracionDeEnvioValores(
        c.HoraDelRecordatorio!.Value, c.HoraDeLaAgendaDelDia!.Value, c.CorreoDeLaDoctora!, c.CorreoDelAdministrador!));

    public ReglasYAvisosDeConfiguracionTests()
    {
        Services.AddSingleton<IQueryHandler<ReglasDeReservaVigentes, ReglasDeReservaValores>>(
            new Consulta<ReglasDeReservaVigentes, ReglasDeReservaValores>(new ReglasDeReservaValores(1, 2, 120, 3)));
        Services.AddSingleton<ICommandHandler<CambiarReglasDeReserva, ReglasDeReservaValores>>(_reglas);
        Services.AddSingleton<IQueryHandler<ConfiguracionDeEnvioVigente, ConfiguracionDeEnvioValores>>(
            new Consulta<ConfiguracionDeEnvioVigente, ConfiguracionDeEnvioValores>(
                new ConfiguracionDeEnvioValores(new(9, 0), new(14, 0), "doctora@correo.com", "admin@correo.com")));
        Services.AddSingleton<ICommandHandler<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores>>(_envio);
        SetRendererInfo(Interactivo);
    }

    [Fact]
    public void Las_reglas_cargan_sus_valores_y_guardan_lo_editado()
    {
        var seccion = Render<SeccionReglasDeReserva>();
        Assert.Equal(["1", "2", "120", "3"], seccion.FindAll("input").Select(i => i.GetAttribute("value")));
        // Los errores son los de la app, no los del navegador (entrada 6.9).
        Assert.True(seccion.Find("form").HasAttribute("novalidate"));
        Assert.Equal("Tope semanal por celular (citas)", seccion.Find("label[for='TopeSemanalPorTelefono']").TextContent);

        seccion.Find("#VentanaMeses").Change("3");
        seccion.Find("form").Submit();

        var enviado = Assert.Single(_reglas.Recibidos);
        Assert.Equal((1, 3, 120, 3), (enviado.AnticipacionMinimaDias, enviado.VentanaMeses, enviado.UmbralCancelacionTardiaMin, enviado.TopeSemanalPorTelefono));
        Assert.Equal(AvisoDeSeccion.Guardada, seccion.Find(".dsm-seccion-de-configuracion__guardado").TextContent);
    }

    [Fact]
    public void El_error_de_una_regla_va_bajo_su_campo()
    {
        _reglas.Responder = _ => Resultado.Invalido(nameof(CambiarReglasDeReserva.AnticipacionMinimaDias), CambiarReglasDeReserva.MensajeDeAnticipacion)
            .Como<ReglasDeReservaValores>();
        var seccion = Render<SeccionReglasDeReserva>();
        seccion.Find("#AnticipacionMinimaDias").Change("-1");

        seccion.Find("form").Submit();

        Assert.Equal(CambiarReglasDeReserva.MensajeDeAnticipacion, seccion.Find("#error-AnticipacionMinimaDias").TextContent);
        Assert.Equal("true", seccion.Find("#AnticipacionMinimaDias").GetAttribute("aria-invalid"));
        Assert.Empty(seccion.FindAll(".dsm-seccion-de-configuracion__guardado"));
    }

    [Fact]
    public void Los_avisos_cargan_sus_valores_y_guardan_la_hora_nueva()
    {
        var seccion = Render<SeccionAvisosPorCorreo>();
        Assert.Equal("09:00:00", seccion.Find("#HoraDelRecordatorio").GetAttribute("value"));
        Assert.True(seccion.Find("form").HasAttribute("novalidate"));
        Assert.Equal("doctora@correo.com", seccion.Find("#CorreoDeLaDoctora").GetAttribute("value"));

        seccion.Find("#HoraDelRecordatorio").Change("08:00:00");
        seccion.Find("form").Submit();

        Assert.Equal(new TimeOnly(8, 0), Assert.Single(_envio.Recibidos).HoraDelRecordatorio);
        Assert.Equal(AvisoDeSeccion.Guardada, seccion.Find(".dsm-seccion-de-configuracion__guardado").TextContent);
    }

    [Fact]
    public void Un_correo_invalido_va_bajo_su_campo_y_sin_sesion_se_dice()
    {
        _envio.Responder = _ => Resultado.Invalido(nameof(CambiarConfiguracionDeEnvio.CorreoDeLaDoctora), CambiarConfiguracionDeEnvio.MensajeDeCorreo)
            .Como<ConfiguracionDeEnvioValores>();
        var seccion = Render<SeccionAvisosPorCorreo>();

        seccion.Find("form").Submit();
        Assert.Equal(CambiarConfiguracionDeEnvio.MensajeDeCorreo, seccion.Find("#error-CorreoDeLaDoctora").TextContent);

        _envio.Responder = _ => Resultado.NoAutorizado().Como<ConfiguracionDeEnvioValores>();
        seccion.Find("form").Submit();
        Assert.Equal(AvisoDeSeccion.SinSesion, seccion.Find(".dsm-seccion-de-configuracion__error").TextContent.Trim());
    }

    private sealed class Consulta<TConsulta, T>(T valor) : IQueryHandler<TConsulta, T>
        where TConsulta : IQuery<T>
    {
        public Task<Resultado<T>> HandleAsync(TConsulta consulta, CancellationToken cancellationToken) => Task.FromResult(Resultado.Exito(valor));
    }

    private sealed class Comando<TComando, T>(Func<TComando, T> eco) : ICommandHandler<TComando, T>
        where TComando : ICommand<T>
    {
        public List<TComando> Recibidos { get; } = [];

        public Func<TComando, Resultado<T>> Responder { get; set; } = c => Resultado.Exito(eco(c));

        public Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken cancellationToken)
        {
            Recibidos.Add(comando);
            return Task.FromResult(Responder(comando));
        }
    }
}
