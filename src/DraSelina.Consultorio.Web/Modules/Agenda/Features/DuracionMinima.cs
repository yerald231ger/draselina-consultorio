namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La duración del Tipo de visita activo más corto: en cada Tramo de la base o de una custom debe caber una Cita de ese
/// tipo (INV-B5, INV-A6). Se lee al cambiar, con los tipos de ese momento (T-6). Un solo lugar para la base y la custom.
/// </summary>
public sealed class DuracionMinima(ITiposDeVisitaActivos tiposActivos)
{
    /// <summary>La duración mínima. Lanza si no hay Tipos de visita activos: no habría con qué comprobarla.</summary>
    public async Task<TimeSpan> ObtenerAsync(CancellationToken cancellationToken)
    {
        var tipos = await tiposActivos.ListarAsync(cancellationToken);
        if (tipos.Count == 0)
        {
            throw new InvalidOperationException("No hay Tipos de visita activos: no se puede comprobar la duración mínima de un Tramo.");
        }

        return TimeSpan.FromMinutes(tipos.Min(t => t.DuracionMin));
    }
}
