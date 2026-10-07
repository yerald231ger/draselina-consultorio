using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>Lo que el host registra de la infraestructura de Notificaciones.</summary>
public static class Registro
{
    public static IServiceCollection AddNotificaciones(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios.AddDbContext<NotificacionesDbContext>(opciones => NotificacionesDbContext.Configurar(opciones, cadenaDeConexion));
}
