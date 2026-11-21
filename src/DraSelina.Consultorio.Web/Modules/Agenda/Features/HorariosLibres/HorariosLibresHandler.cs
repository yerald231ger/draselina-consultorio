using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los horarios libres de un tipo de visita. Orquesta y no decide (T-13): lee el tipo, las reglas, la agenda de
/// atención (las custom de la ventana) y las Citas activas de la ventana, y le deja el cálculo a
/// <see cref="CalculadoraDeDisponibilidad"/> (T-21). Con <see cref="HorariosLibres.Aparte"/>, esa Cita no cuenta entre
/// las activas; la calculadora no cambia. Es una consulta: no abre transacción ni cambia estado (T-14).
/// </summary>
public sealed class HorariosLibresHandler(
    ITiposDeVisita tiposDeVisita,
    IReglasDeReserva reglasDeReserva,
    IAgendaDelRango agendaDelRango,
    ICitasActivas citasActivas,
    IClock reloj) : IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>
{
    public async Task<Resultado<IReadOnlyList<DiaConHorarios>>> HandleAsync(
        HorariosLibres consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var ahora = reloj.UtcNow;

        var tipo = await tiposDeVisita.ObtenerAsync(consulta.TipoDeVisitaId, cancellationToken);
        if (tipo is not { SeAgendaEnLinea: true })
        {
            return Resultado.Fallo(CodigosDeAgenda.TipoNoReservable).Como<IReadOnlyList<DiaConHorarios>>();
        }

        var reglas = await reglasDeReserva.ObtenerAsync(cancellationToken);
        // Solo las fechas de la ventana, por el puerto de lectura (entrada 6.10): la calculadora no pregunta por otras.
        var ventana = CalculadoraDeDisponibilidad.Ventana(reglas, ahora);
        var agenda = await agendaDelRango.LeerAsync(ventana.PrimeraFecha, ventana.UltimaFecha, cancellationToken);
        var (desde, hasta) = ventana.EnUtc();
        var citas = await citasActivas.EntreAsync(desde, hasta, consulta.Aparte, cancellationToken);

        var dias = CalculadoraDeDisponibilidad.Calcular(agenda, citas, tipo.Duracion, reglas, ahora);

        return Resultado.Exito<IReadOnlyList<DiaConHorarios>>(
            [.. dias.Select(d => new DiaConHorarios(d.Fecha, [.. d.Horarios.Select(h => h.Hora)]))]);
    }
}
