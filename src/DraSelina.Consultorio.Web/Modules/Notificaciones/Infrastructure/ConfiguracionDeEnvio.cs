using DraSelina.Consultorio.Notificaciones.Contracts;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary><see cref="IConfiguracionDeEnvio"/> sobre la única fila de <c>configuracion_de_envio</c>.</summary>
public sealed class ConfiguracionDeEnvio(NotificacionesDbContext db) : IConfiguracionDeEnvio
{
    public async Task<string> CorreoDelAdministradorAsync(CancellationToken cancellationToken) =>
        await db.ConfiguracionDeEnvio.AsNoTracking().Select(c => c.CorreoAdministrador).SingleAsync(cancellationToken);

    public async Task<string> CorreoDeLaDoctoraAsync(CancellationToken cancellationToken) =>
        await db.ConfiguracionDeEnvio.AsNoTracking().Select(c => c.CorreoDoctora).SingleAsync(cancellationToken);

    public async Task<TimeOnly> HoraDeLaAgendaDelDiaAsync(CancellationToken cancellationToken) =>
        await db.ConfiguracionDeEnvio.AsNoTracking().Select(c => c.HoraAgendaDelDia).SingleAsync(cancellationToken);

    public async Task<ConfiguracionDeEnvioValores> LeerAsync(CancellationToken cancellationToken) =>
        await db.ConfiguracionDeEnvio.AsNoTracking()
            .Select(c => new ConfiguracionDeEnvioValores(c.HoraRecordatorio, c.HoraAgendaDelDia, c.CorreoDoctora, c.CorreoAdministrador))
            .SingleAsync(cancellationToken);

    public async Task<TimeOnly> CambiarAsync(ConfiguracionDeEnvioValores valores, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valores);
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("La configuración de envío se cambia en la transacción abierta del llamador.");
        }

        // Sin componer nada sobre la consulta: EF la manda tal cual, con su FOR UPDATE.
        var anterior = (await db.Database
            .SqlQuery<TimeOnly>(
                $"""select hora_recordatorio as "Value" from "notificaciones"."configuracion_de_envio" where id = 1 for update""")
            .ToListAsync(cancellationToken))
            .Single();

        await db.Database.ExecuteSqlAsync(
            $"""
            update "notificaciones"."configuracion_de_envio"
            set hora_recordatorio = {valores.HoraDelRecordatorio}, hora_agenda_del_dia = {valores.HoraDeLaAgendaDelDia},
                correo_doctora = {valores.CorreoDeLaDoctora}, correo_administrador = {valores.CorreoDelAdministrador}
            where id = 1
            """,
            cancellationToken);

        return anterior;
    }
}
