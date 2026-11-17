using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador cambia las reglas de reserva sin desplegar (CAP-16, FR-19): surten efecto en la siguiente reserva,
/// reprogramación o cancelación. Los límites los decide <c>ReglasDeReserva</c>: anticipación y umbral no negativos,
/// ventana y tope mayores que cero; si alguno no vale, nada cambia y el error va bajo su propiedad. Solo el
/// Administrador (AD-22). Devuelve los valores guardados.
/// </summary>
public sealed class CambiarReglasDeReserva : ICommand<ReglasDeReservaValores>, IRequiereAdministrador
{
    public const string MensajeDeAnticipacion = "La anticipación no puede ser negativa; 0 permite reservar el mismo día.";
    public const string MensajeDeVentana = "La ventana debe ser de al menos 1 mes.";
    public const string MensajeDeUmbral = "El umbral no puede ser negativo; 0 hace que ninguna cancelación sea tardía.";
    public const string MensajeDeTope = "El tope debe ser de al menos 1 cita por semana.";
    public const string MensajeDeMaximo = "El número es demasiado grande.";

    /// <summary>Días calendario desde hoy antes de los cuales no se reserva en línea.</summary>
    [Required(ErrorMessage = "Escribe la anticipación mínima.")]
    [Range(int.MinValue, short.MaxValue, ErrorMessage = MensajeDeMaximo)]
    public int? AnticipacionMinimaDias { get; set; }

    /// <summary>Meses hacia adelante hasta los que se reserva en línea.</summary>
    [Required(ErrorMessage = "Escribe la ventana de reserva.")]
    [Range(int.MinValue, short.MaxValue, ErrorMessage = MensajeDeMaximo)]
    public int? VentanaMeses { get; set; }

    /// <summary>Minutos antes del inicio desde los que una cancelación es tardía y ya no se reprograma (RN-17).</summary>
    [Required(ErrorMessage = "Escribe el umbral de cancelación tardía.")]
    [Range(int.MinValue, short.MaxValue, ErrorMessage = MensajeDeMaximo)]
    public int? UmbralCancelacionTardiaMin { get; set; }

    /// <summary>Citas en línea por teléfono en una semana (RN-18).</summary>
    [Required(ErrorMessage = "Escribe el tope semanal por teléfono.")]
    [Range(int.MinValue, short.MaxValue, ErrorMessage = MensajeDeMaximo)]
    public int? TopeSemanalPorTelefono { get; set; }
}
