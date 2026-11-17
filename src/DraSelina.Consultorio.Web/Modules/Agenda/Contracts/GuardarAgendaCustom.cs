using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador da a una fecha su horario propio, o cambia el que ya tiene (CAP-11, FR-13, UJ-3, UJ-6): reemplaza
/// por completo a la Agenda base ese día (RN-4). Cero Tramos: ese día no se atiende (INV-A3). Siempre con motivo
/// (INV-A4). Desde hoy (fecha local) en adelante, con cualquier anticipación (RN-11). Si con los Tramos nuevos queda
/// fuera alguna Cita Agendada que todavía no empieza, no guarda nada y vuelve <see cref="CodigosDeAgenda.CitasFuera"/>:
/// la elección de cancelar o conservar cada una llega con la entrada 6.2. Solo el Administrador (AD-22). El motivo no va
/// a registros: <see cref="ToString"/> no lo muestra.
/// </summary>
public sealed class GuardarAgendaCustom : ICommand<AgendaCustomGuardada>, IRequiereAdministrador, IValidatableObject
{
    public const string MensajeDeMotivo = "Escribe el motivo, por ejemplo \"cirugía\" o \"sábado abierto\".";
    public const string MensajeDeTramoIncompleto = "Escribe la hora de inicio y la de fin.";
    public const string MensajeDeTramoAlReves = "La hora de inicio debe ser antes que la de fin.";

    /// <summary>Fecha local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta la fecha.")]
    public DateOnly? Fecha { get; set; }

    /// <summary>"cirugía", "vacaciones", "sábado abierto" (INV-A4).</summary>
    [Required(ErrorMessage = MensajeDeMotivo)]
    [StringLength(120, ErrorMessage = "El motivo tiene a lo más 120 caracteres.")]
    public string? Motivo { get; set; }

    /// <summary>Los Tramos de ese día, en hora local; vacío: ese día no se atiende (INV-A3).</summary>
    public List<TramoAGuardar> Tramos { get; set; } = [];

    /// <summary>Nombre de la propiedad de error del Tramo en la posición <paramref name="indice"/>.</summary>
    public static string PropiedadDelTramo(int indice) => $"{nameof(Tramos)}[{indice}]";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        for (var i = 0; i < Tramos.Count; i++)
        {
            var tramo = Tramos[i];
            if (tramo?.Inicio is null || tramo.Fin is null)
            {
                yield return new ValidationResult(MensajeDeTramoIncompleto, [PropiedadDelTramo(i)]);
            }
            else if (tramo.Inicio >= tramo.Fin)
            {
                yield return new ValidationResult(MensajeDeTramoAlReves, [PropiedadDelTramo(i)]);
            }
        }
    }

    public override string ToString() =>
        $"GuardarAgendaCustom {{ Fecha = {Fecha:yyyy-MM-dd}, Motivo = oculto, Tramos = {Tramos.Count} }}";
}

/// <summary>Un Tramo del comando, en hora local, <c>[Inicio, Fin)</c>.</summary>
public sealed class TramoAGuardar
{
    public TimeOnly? Inicio { get; set; }

    public TimeOnly? Fin { get; set; }
}

/// <summary>Lo que quedó al guardar la Agenda custom de una fecha.</summary>
/// <param name="Fecha">La fecha local.</param>
/// <param name="Registrada">Verdadero si la fecha no tenía custom; falso si se cambió la que tenía.</param>
/// <param name="SinCambios">Los Tramos y el motivo eran los mismos: nada se guardó ni se anotó.</param>
public sealed record AgendaCustomGuardada(DateOnly Fecha, bool Registrada, bool SinCambios);
