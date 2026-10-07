using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// Lo que el host registra de Notificaciones: su contexto, su puerto de transacción y sus handlers. Cada handler se
/// registra aquí, uno por línea, por su interfaz (AD-10):
/// <c>.AddScoped&lt;ICommandHandler&lt;MiComando, T&gt;, MiComandoHandler&gt;()</c>.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddNotificaciones(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<NotificacionesDbContext>(opciones => NotificacionesDbContext.Configurar(opciones, cadenaDeConexion))
            .AddScoped<ITransaccionDeNotificaciones, TransaccionDeNotificaciones>();
}
