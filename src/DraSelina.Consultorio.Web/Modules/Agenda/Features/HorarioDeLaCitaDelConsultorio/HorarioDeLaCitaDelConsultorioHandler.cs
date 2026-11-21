using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Si la Cita del consultorio que se captura caería fuera de horario. Una consulta: no abre transacción, no toma el
/// candado de la fecha ni cambia estado. Lee el tipo (su duración fija el fin, INV-C3) y los Tramos vigentes de la
/// fecha, y aplica <see cref="Cita.QuedaFueraDe"/>, la misma regla con la que la Cita pide la nota (INV-C6): la página
/// no la recalcula. Es un aviso; al guardar, el comando vuelve a decidir con los Tramos de ese momento.
/// </summary>
public sealed class HorarioDeLaCitaDelConsultorioHandler(ITiposDeVisita tiposDeVisita, ITramosDeLaFecha tramosDeLaFecha)
    : IQueryHandler<HorarioDeLaCitaDelConsultorio, HorarioDeLaCita>
{
    public async Task<Resultado<HorarioDeLaCita>> HandleAsync(
        HorarioDeLaCitaDelConsultorio consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var tipo = await tiposDeVisita.ObtenerAsync(consulta.TipoDeVisitaId, cancellationToken);
        if (tipo is null)
        {
            return Resultado.Fallo(CodigosDeAgenda.TipoNoReservable).Como<HorarioDeLaCita>();
        }

        // Fecha y hora locales del consultorio; la regla se mide en instantes UTC, como la de la Cita (AD-17).
        var inicio = HoraLocal.AUtc(consulta.Fecha, consulta.Hora);
        var rango = RangoDeTiempo.Crear(inicio, inicio + tipo.Duracion);
        var vigentes = await tramosDeLaFecha.VigentesAsync(HoraLocal.DesdeUtc(inicio).Fecha, cancellationToken);

        return Resultado.Exito(new HorarioDeLaCita(Cita.QuedaFueraDe(rango, vigentes), vigentes.SinTramos));
    }
}
