using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El Recordatorio previsto de una Cita: la hora de <c>configuracion_de_envio</c> y, con
/// <see cref="HoraDelRecordatorio.ProgramadoPara"/>, si ya pasó. Una consulta: no abre transacción ni cambia nada.
/// </summary>
public sealed class RecordatorioPrevistoHandler(IRecordatorios recordatorios, IClock reloj)
    : IQueryHandler<RecordatorioPrevisto, Recordatorio>
{
    public async Task<Resultado<Recordatorio>> HandleAsync(RecordatorioPrevisto consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var hora = await recordatorios.HoraDelRecordatorioAsync(cancellationToken);
        var local = consulta.Fecha.ToDateTime(consulta.Hora, DateTimeKind.Unspecified);
        var inicio = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, ZonaDelConsultorio.Zona), TimeSpan.Zero);

        return Resultado.Exito(new Recordatorio(HoraDelRecordatorio.ProgramadoPara(inicio, hora) > reloj.UtcNow, hora));
    }
}
