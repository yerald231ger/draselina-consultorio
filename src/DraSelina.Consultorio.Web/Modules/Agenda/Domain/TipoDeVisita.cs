using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// La clase de Cita que elige el Paciente; define la duración. En pantalla, "Tipo de consulta". Los dos tipos iniciales
/// se siembran con la migración; el Administrador los cambia, crea y desactiva desde Configuración (entrada 6.7). No se
/// borran ni se reactivan (Gerardo, 2026-11-17).
/// </summary>
public sealed class TipoDeVisita
{
    // Para la reconstitución desde la base (T-6).
    private TipoDeVisita()
    {
        Nombre = null!;
    }

    public TipoDeVisitaId Id { get; private set; }

    public string Nombre { get; private set; }

    /// <summary>Texto que ve el Paciente al elegir; puede no tener.</summary>
    public string? Descripcion { get; private set; }

    /// <summary>En minutos; mayor que cero.</summary>
    public int DuracionMin { get; private set; }

    public bool ReservableEnLinea { get; private set; }

    /// <summary>Orden en pantalla.</summary>
    public int Orden { get; private set; }

    /// <summary>Un tipo con citas no se borra: se desactiva.</summary>
    public bool Activo { get; private set; }

    public TimeSpan Duracion => TimeSpan.FromMinutes(DuracionMin);

    /// <summary>Un tipo de visita nuevo y activo, con su id Guid v7 (T-10, T-15). La duración es mayor que cero.</summary>
    public static TipoDeVisita Crear(string nombre, int duracionMin, bool reservableEnLinea, int orden, string? descripcion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duracionMin);

        return new TipoDeVisita
        {
            Id = TipoDeVisitaId.Nuevo(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion,
            DuracionMin = duracionMin,
            ReservableEnLinea = reservableEnLinea,
            Orden = orden,
            Activo = true,
        };
    }

    /// <summary>
    /// Cambia sus datos. Igual que <see cref="Crear"/>: nombre no vacío y duración mayor que cero. Las Citas ya agendadas
    /// no cambian (RN-8) y los Tramos guardados no se revalidan con la duración nueva (T-6).
    /// </summary>
    public void Cambiar(string nombre, string? descripcion, int duracionMin, bool reservableEnLinea, int orden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duracionMin);

        Nombre = nombre.Trim();
        Descripcion = descripcion;
        DuracionMin = duracionMin;
        ReservableEnLinea = reservableEnLinea;
        Orden = orden;
    }

    /// <summary>
    /// Deja de ofrecerse, en línea y en el consultorio; sus Citas siguen como están. No se reactiva. Quien lo llama cuida
    /// que no sea el último activo (Gerardo, 2026-11-17).
    /// </summary>
    /// <returns>Falso si ya estaba inactivo.</returns>
    public bool Desactivar()
    {
        if (!Activo)
        {
            return false;
        }

        Activo = false;
        return true;
    }

    /// <summary>Si el Paciente puede agendarlo desde el sitio: activo y reservable en línea.</summary>
    public bool SeAgendaEnLinea => Activo && ReservableEnLinea;
}
