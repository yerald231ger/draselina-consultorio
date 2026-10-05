using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// Solo para <c>dotnet ef migrations add</c>: crea el contexto sin arrancar la aplicación.
/// Generar una migración no abre conexión, así que la cadena no lleva contraseña ni apunta a una base real.
/// </summary>
public sealed class NotificacionesDbContextFactory : IDesignTimeDbContextFactory<NotificacionesDbContext>
{
    public NotificacionesDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<NotificacionesDbContext>();
        NotificacionesDbContext.Configurar(opciones, "Host=localhost;Database=consultorio");
        return new NotificacionesDbContext(opciones.Options);
    }
}
