using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>Lo que la bitácora guarda de un Tipo de visita, en camelCase como el resto.</summary>
internal sealed record DetalleDeTipo(string nombre, string? descripcion, int duracionMin, bool reservableEnLinea, int orden, bool activo)
{
    public static DetalleDeTipo De(TipoDeVisita tipo) =>
        new(tipo.Nombre, tipo.Descripcion, tipo.DuracionMin, tipo.ReservableEnLinea, tipo.Orden, tipo.Activo);
}
