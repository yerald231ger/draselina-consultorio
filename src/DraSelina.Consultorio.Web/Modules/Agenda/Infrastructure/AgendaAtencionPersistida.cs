using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="IAgendaAtencion"/> sobre <see cref="AgendaDbContext"/>. El agregado no tiene id propio y vive en tres
/// tablas: se arma a mano desde sus filas y, al guardar, se sincronizan las filas con él.
/// </summary>
public sealed class AgendaAtencionPersistida(AgendaDbContext db, IClock reloj) : IAgendaAtencion
{
    private static readonly DayOfWeek[] Dias = Enum.GetValues<DayOfWeek>();

    public async Task<AgendaAtencion> ObtenerAsync(DateOnly customsDesde, CancellationToken cancellationToken)
    {
        var tramosBase = await db.TramosBase.AsNoTracking().ToListAsync(cancellationToken);
        var customs = await db.AgendasCustom
            .AsNoTracking()
            .Include(c => c.Tramos)
            .Where(c => c.Fecha >= customsDesde)
            .ToListAsync(cancellationToken);

        return AgendaAtencion.Reconstituir(
            tramosBase.Select(t => (FilaDeTramoBase.Dia(t.DiaDeLaSemana), Tramo.Reconstituir(t.Inicio, t.Fin))),
            customs.Select(c => (c.Fecha, c.Motivo, c.Tramos.Select(t => Tramo.Reconstituir(t.Inicio, t.Fin)))),
            customsDesde);
    }

    public async Task GuardarAsync(AgendaAtencion agenda, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agenda);

        // Solo se sincronizan las custom desde el límite: las de fechas pasadas no participan en ninguna regla y
        // nunca se tocan. Una custom anterior al límite en el agregado es un error de programación; se revisa antes
        // de cambiar nada.
        var limite = Max(agenda.CustomsDesde, HoraLocal.DesdeUtc(reloj.UtcNow).Fecha);
        if (agenda.Customs.FirstOrDefault(c => c.Fecha < limite) is { } pasada)
        {
            throw new InvalidOperationException(
                $"La agenda custom del {pasada.Fecha:yyyy-MM-dd} es anterior al {limite:yyyy-MM-dd}: las custom pasadas no se guardan.");
        }

        await SincronizarBaseAsync(agenda, cancellationToken);
        await SincronizarCustomsAsync(agenda, limite, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Cada fila se reconoce por (día, inicio), su único: se cambia el fin, se agrega la que falta y se borra la que sobra.
    private async Task SincronizarBaseAsync(AgendaAtencion agenda, CancellationToken cancellationToken)
    {
        var filas = await db.TramosBase.ToListAsync(cancellationToken);
        var sobrantes = filas.ToDictionary(f => (f.DiaDeLaSemana, f.Inicio));

        foreach (var dia in Dias)
        {
            var diaIso = FilaDeTramoBase.DiaIso(dia);
            foreach (var tramo in agenda.TramosBase(dia))
            {
                if (sobrantes.Remove((diaIso, tramo.Inicio), out var fila))
                {
                    fila.Fin = tramo.Fin;
                }
                else
                {
                    db.TramosBase.Add(new FilaDeTramoBase
                    {
                        Id = Guid.CreateVersion7(),
                        DiaDeLaSemana = diaIso,
                        Inicio = tramo.Inicio,
                        Fin = tramo.Fin,
                    });
                }
            }
        }

        db.TramosBase.RemoveRange(sobrantes.Values);
    }

    // Solo las filas desde el límite: las anteriores no están en el agregado y no se tocan.
    private async Task SincronizarCustomsAsync(AgendaAtencion agenda, DateOnly limite, CancellationToken cancellationToken)
    {
        var filas = await db.AgendasCustom
            .Include(c => c.Tramos)
            .Where(c => c.Fecha >= limite)
            .ToDictionaryAsync(c => c.Fecha, cancellationToken);

        var ahora = reloj.UtcNow;
        foreach (var custom in agenda.Customs)
        {
            if (filas.Remove(custom.Fecha, out var fila))
            {
                if (CambiarFila(fila, custom))
                {
                    fila.ActualizadaEn = ahora;
                }
            }
            else
            {
                db.AgendasCustom.Add(new FilaDeAgendaCustom
                {
                    Fecha = custom.Fecha,
                    Motivo = custom.Motivo,
                    CreadaEn = ahora,
                    ActualizadaEn = ahora,
                    Tramos = [.. custom.Tramos.Select(t => NuevoTramo(custom.Fecha, t))],
                });
            }
        }

        // Borrar la custom borra sus tramos en cascada.
        db.AgendasCustom.RemoveRange(filas.Values);
    }

    // Igual que la base: cada tramo se reconoce por (fecha, inicio). Devuelve si algo cambió.
    private bool CambiarFila(FilaDeAgendaCustom fila, AgendaCustom custom)
    {
        var cambio = false;
        if (fila.Motivo != custom.Motivo)
        {
            fila.Motivo = custom.Motivo;
            cambio = true;
        }

        var sobrantes = fila.Tramos.ToDictionary(t => t.Inicio);
        foreach (var tramo in custom.Tramos)
        {
            if (sobrantes.Remove(tramo.Inicio, out var filaDeTramo))
            {
                cambio |= filaDeTramo.Fin != tramo.Fin;
                filaDeTramo.Fin = tramo.Fin;
            }
            else
            {
                fila.Tramos.Add(NuevoTramo(custom.Fecha, tramo));
                cambio = true;
            }
        }

        foreach (var sobrante in sobrantes.Values)
        {
            fila.Tramos.Remove(sobrante);
            db.TramosCustom.Remove(sobrante);
            cambio = true;
        }

        return cambio;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static FilaDeTramoCustom NuevoTramo(DateOnly fecha, Tramo tramo) => new()
    {
        Id = Guid.CreateVersion7(),
        Fecha = fecha,
        Inicio = tramo.Inicio,
        Fin = tramo.Fin,
    };
}
