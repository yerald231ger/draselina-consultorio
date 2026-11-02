namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// Un rango semiabierto <c>[Inicio, Fin)</c> de instantes UTC (AD-8, INV-C2). El inicio siempre es anterior al fin.
/// </summary>
public sealed record RangoDeTiempo
{
    private RangoDeTiempo(DateTimeOffset inicio, DateTimeOffset fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    public DateTimeOffset Inicio { get; }

    public DateTimeOffset Fin { get; }

    /// <summary>El rango, en UTC. Lanza si el inicio no es anterior al fin.</summary>
    public static RangoDeTiempo Crear(DateTimeOffset inicio, DateTimeOffset fin)
    {
        if (inicio >= fin)
        {
            throw new ArgumentException($"El inicio ({inicio:O}) debe ser anterior al fin ({fin:O}).", nameof(fin));
        }

        return new RangoDeTiempo(inicio.ToUniversalTime(), fin.ToUniversalTime());
    }

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static RangoDeTiempo Reconstituir(DateTimeOffset inicio, DateTimeOffset fin) => new(inicio, fin);

    /// <summary>Dos rangos semiabiertos se traslapan si cada uno empieza antes de que el otro termine.</summary>
    public bool Traslapa(RangoDeTiempo otro)
    {
        ArgumentNullException.ThrowIfNull(otro);
        return Inicio < otro.Fin && otro.Inicio < Fin;
    }
}
