using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary><see cref="IConfiguracionDeEnvio"/> sobre la única fila de <c>configuracion_de_envio</c>.</summary>
public sealed class ConfiguracionDeEnvio(NotificacionesDbContext db) : IConfiguracionDeEnvio
{
    public async Task<string> CorreoDelAdministradorAsync(CancellationToken cancellationToken) =>
        await db.ConfiguracionDeEnvio.AsNoTracking().Select(c => c.CorreoAdministrador).SingleAsync(cancellationToken);
}
