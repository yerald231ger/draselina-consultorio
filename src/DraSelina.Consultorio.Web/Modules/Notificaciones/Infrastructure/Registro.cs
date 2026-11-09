using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Notificaciones.Contracts;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// Lo que el host registra de Notificaciones: su contexto, su puerto de transacción, sus puertos, su canal, su
/// configuración, sus handlers y el servicio en segundo plano de sus envíos programados. Cada handler se registra
/// aquí, uno por línea (AD-10). Los de eventos van con <see cref="AddEventHandler{THandler}"/>, por cada
/// <c>IEventHandler&lt;T&gt;</c> que implementan: así esta capa no nombra los eventos de otros módulos, que solo
/// puede usar Features (AD-1).
/// </summary>
public static class Registro
{
    public static IServiceCollection AddNotificaciones(this IServiceCollection servicios, string cadenaDeConexion)
    {
        servicios.AddOptions<OpcionesDeNotificaciones>().BindConfiguration(OpcionesDeNotificaciones.Seccion);

        return servicios
            .AddDbContext<NotificacionesDbContext>(opciones => NotificacionesDbContext.Configurar(opciones, cadenaDeConexion))
            .AddScoped<ITransaccionDeNotificaciones, TransaccionDeNotificaciones>()
            .AddScoped<IMensajes, Mensajes>()
            .AddScoped<IRecordatorios, Recordatorios>()
            .AddScoped<IConfiguracionDeEnvio, ConfiguracionDeEnvio>()
            .AddScoped<IQueryHandler<RecordatorioPrevisto, Recordatorio>, RecordatorioPrevistoHandler>()
            .AddScoped<IQueryHandler<ConfiguracionDeEnvioVigente, ConfiguracionDeEnvioValores>, ConfiguracionDeEnvioVigenteHandler>()
            .AddScoped<ICommandHandler<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores>, CambiarConfiguracionDeEnvioHandler>()
            .AddSingleton<INotificationChannel, CanalDeCorreoSmtp>()
            .AddSingleton<IDireccionDelSitio, DireccionDelSitio>()
            .AddEventHandler<ConfirmacionAlAgendar>()
            .AddEventHandler<RecordatorioAlAgendar>()
            .AddEventHandler<RecordatorioAlReprogramar>()
            .AddEventHandler<RecordatorioAlCancelar>()
            .AddEventHandler<AvisoAlReprogramar>()
            .AddEventHandler<AvisoAlCancelar>()
            .AddEventHandler<AlertaDeCancelacionTardia>()
            .AddSingleton<EnviosProgramados>()
            .AddHostedService<ServicioDeEnviosProgramados>();
    }

    // Registra THandler, por ámbito, como cada IEventHandler<T> que implementa.
    private static IServiceCollection AddEventHandler<THandler>(this IServiceCollection servicios)
        where THandler : class
    {
        var interfaces = typeof(THandler).GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            .ToList();

        if (interfaces.Count == 0)
        {
            throw new InvalidOperationException($"{typeof(THandler).Name} no implementa ningún IEventHandler<T>.");
        }

        foreach (var interfaz in interfaces)
        {
            servicios.AddScoped(interfaz, typeof(THandler));
        }

        return servicios;
    }
}
