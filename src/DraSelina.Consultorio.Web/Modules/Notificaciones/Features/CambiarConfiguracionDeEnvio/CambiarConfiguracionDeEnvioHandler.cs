using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Guarda la configuración de envío (CAP-16) en una transacción de Notificaciones. La Agenda del día y la alerta tardía
/// ya leen la configuración en cada envío: basta con guardar. Si cambió la hora del Recordatorio, en la misma
/// transacción recalcula cada Recordatorio pendiente con <see cref="HoraDelRecordatorio.ProgramadoPara"/>:
/// <list type="bullet">
/// <item>Si la nueva hora es posterior a <see cref="IClock.UtcNow"/>, mueve la fila a esa hora.</item>
/// <item>Si no, borra la fila y registra un <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/>
/// <see cref="EstadoDeMensaje.Omitido"/> de la Cita, sin evento (Gerardo, 2026-11-09).</item>
/// </list>
/// Sin bitácora: la configuración de envío no está en CAP-17. Nada de los valores va a registros (AD-23).
/// </summary>
public sealed class CambiarConfiguracionDeEnvioHandler(
    ITransaccionDeNotificaciones transaccion,
    IConfiguracionDeEnvio configuracion,
    IRecordatorios recordatorios,
    IMensajes mensajes,
    IClock reloj) : ICommandHandler<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores>
{
    public async Task<Resultado<ConfiguracionDeEnvioValores>> HandleAsync(
        CambiarConfiguracionDeEnvio comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma ya pasó (AD-12): los cuatro valores vienen.
        var valores = new ConfiguracionDeEnvioValores(
            comando.HoraDelRecordatorio!.Value,
            comando.HoraDeLaAgendaDelDia!.Value,
            comando.CorreoDeLaDoctora!,
            comando.CorreoDelAdministrador!);

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        var horaAnterior = await configuracion.CambiarAsync(valores, cancellationToken);
        if (horaAnterior != valores.HoraDelRecordatorio)
        {
            await RecalcularRecordatoriosAsync(valores.HoraDelRecordatorio, cancellationToken);
        }

        await tx.ConfirmarAsync(cancellationToken);
        return Resultado.Exito(valores);
    }

    private async Task RecalcularRecordatoriosAsync(TimeOnly hora, CancellationToken cancellationToken)
    {
        var ahora = reloj.UtcNow;
        foreach (var recordatorio in await recordatorios.ApartarTodosAsync(cancellationToken))
        {
            var programadoPara = HoraDelRecordatorio.ProgramadoPara(recordatorio.Inicio, hora);
            if (programadoPara > ahora)
            {
                await recordatorios.MoverAsync(recordatorio with { ProgramadoPara = programadoPara }, cancellationToken);
                continue;
            }

            await recordatorios.BorrarAsync(recordatorio.CitaId, cancellationToken);
            await mensajes.RegistrarAsync(
                new MensajeRegistrado(
                    Guid.CreateVersion7(),
                    EventoId: null,
                    recordatorio.CitaId,
                    TipoDeMensaje.Recordatorio,
                    CanalDeMensaje.Correo,
                    EstadoDeMensaje.Omitido,
                    ahora),
                cancellationToken);
        }
    }
}
