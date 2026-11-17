using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Las Citas que quedan fuera de unos Tramos de una fecha (AD-6, RN-9): las Agendadas, de cualquier origen, que todavía
/// no empiezan (RN-31) y que no caben completas en un solo Tramo, con la regla de <see cref="Cita.QuedaFueraDe"/> (no se
/// reimplementa). Lo usan quien guarda una Agenda custom (entradas 6.1 y 6.2) y quien cambia la base (entrada 6.4).
/// Quien guarda lo llama después de tomar el candado de la fecha (AD-26). Solo lee.
/// </summary>
public sealed class CitasQueQuedanFuera(ICitasDeLaAgenda citas, IClock reloj)
{
    /// <summary>Las Citas de <paramref name="nuevos"/>.Fecha que quedarían fuera de esos Tramos, en orden de inicio.</summary>
    public async Task<IReadOnlyList<CitaQueQuedaFuera>> DeAsync(TramosVigentes nuevos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nuevos);

        var leidas = await citas.EntreAsync(
            HoraLocal.AUtc(nuevos.Fecha, TimeOnly.MinValue),
            HoraLocal.AUtc(nuevos.Fecha.AddDays(1), TimeOnly.MinValue),
            cancellationToken);
        var ahora = reloj.UtcNow;

        return
        [
            .. leidas
                .Where(c => c.Estado == EstadoCita.Agendada && c.Inicio > ahora)
                .Where(c => Cita.QuedaFueraDe(RangoDeTiempo.Crear(c.Inicio, c.Fin), nuevos))
                .Select(c => new CitaQueQuedaFuera(c.Id, c.PacienteId, c.Inicio, c.Fin, c.Origen)),
        ];
    }
}

/// <summary>Una Cita que quedaría fuera. Sin motivo de consulta, nota ni contacto (AD-23).</summary>
/// <param name="Inicio">En UTC.</param>
/// <param name="Fin">En UTC.</param>
public sealed record CitaQueQuedaFuera(CitaId Id, PatientId PacienteId, DateTimeOffset Inicio, DateTimeOffset Fin, OrigenDeCita Origen);
