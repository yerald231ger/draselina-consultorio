namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// Un periodo continuo de atención dentro de un día, con hora local de Ciudad de México de inicio y de fin (AD-17).
/// Es semiabierto, <c>[Inicio, Fin)</c>, y el inicio siempre es anterior al fin (INV-B1, INV-A2): no existe un Tramo
/// inválido (T-8).
/// </summary>
public sealed record Tramo
{
    private Tramo(TimeOnly inicio, TimeOnly fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    /// <summary>Hora local de inicio.</summary>
    public TimeOnly Inicio { get; }

    /// <summary>Hora local de fin.</summary>
    public TimeOnly Fin { get; }

    public TimeSpan Duracion => Fin - Inicio;

    /// <summary>El Tramo. Lanza si el inicio no es anterior al fin (INV-B1, INV-A2).</summary>
    public static Tramo Crear(TimeOnly inicio, TimeOnly fin)
    {
        if (inicio >= fin)
        {
            throw new ArgumentException($"El inicio ({inicio:HH:mm}) debe ser anterior al fin ({fin:HH:mm}).", nameof(fin));
        }

        return new Tramo(inicio, fin);
    }

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static Tramo Reconstituir(TimeOnly inicio, TimeOnly fin) => new(inicio, fin);

    /// <summary>Dos Tramos se enciman si cada uno empieza antes de que el otro termine. Los contiguos no se enciman.</summary>
    public bool SeEncimaCon(Tramo otro)
    {
        ArgumentNullException.ThrowIfNull(otro);
        return Inicio < otro.Fin && otro.Inicio < Fin;
    }

    /// <summary>Si el periodo <c>[inicio, fin)</c> cabe completo dentro de este Tramo.</summary>
    public bool Contiene(TimeOnly inicio, TimeOnly fin) => inicio < fin && Inicio <= inicio && fin <= Fin;

    public override string ToString() => $"{Inicio:HH:mm}–{Fin:HH:mm}";
}
