namespace DraSelina.Consultorio.UI.Pages.Administracion;

/// <summary>
/// Cada cuánto la Agenda vuelve a consultar mientras el circuito está abierto (entrada 5.2, `[ASSUMPTION de AD-11]`: sin
/// canal en tiempo real). Se registra en <c>Program.cs</c>; las pruebas registran uno corto.
/// </summary>
/// <param name="Intervalo">Mayor que cero.</param>
public sealed record RefrescoDeLaAgenda(TimeSpan Intervalo)
{
    /// <summary>30 segundos: una Cita nueva en línea aparece en menos de eso.</summary>
    public static RefrescoDeLaAgenda PorOmision { get; } = new(TimeSpan.FromSeconds(30));
}
