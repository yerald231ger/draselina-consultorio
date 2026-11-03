using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Paciente agenda una Cita desde el sitio (AD-7). Tipo, fecha y hora llegan de la cadena de consulta de Tus datos
/// (<c>?tipo=&lt;TipoDeVisitaId&gt;&amp;fecha=AAAA-MM-DD&amp;hora=HH:mm</c>); nombre, teléfono, correo y motivo, solo del
/// cuerpo del formulario (AD-11). Los datos de contacto no se guardan en Agenda: pasan a Pacientes (E-12).
/// </summary>
public sealed class AgendarCitaEnLinea : ICommand<CitaAgendadaEnLinea>
{
    public const string MensajeDeTelefono = "Revisa tu número: deben ser 10 dígitos.";
    public const string MensajeDeTelefonoDeMexico = "Revisa tu número: no corresponde a un celular de México.";
    public const string MensajeDeNombre = "Escribe tu nombre completo.";
    public const string MensajeDeCorreo = "Revisa tu correo: debe ser como nombre@correo.com.";

    [Required(ErrorMessage = "Falta el tipo de consulta.")]
    public TipoDeVisitaId? TipoDeVisitaId { get; set; }

    /// <summary>Fecha local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta el día de la cita.")]
    public DateOnly? Fecha { get; set; }

    /// <summary>Hora local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta la hora de la cita.")]
    public TimeOnly? Hora { get; set; }

    [Required(ErrorMessage = MensajeDeNombre)]
    [StringLength(120, ErrorMessage = "Tu nombre tiene a lo más 120 caracteres.")]
    public string? Nombre { get; set; }

    /// <summary>Celular de México: 10 dígitos, con o sin <c>+52</c>.</summary>
    [Required(ErrorMessage = "Escribe tu celular.")]
    [TelefonoDeDiezDigitos(ErrorMessage = MensajeDeTelefono)]
    public string? Telefono { get; set; }

    /// <summary>Opcional. Vacío o con solo espacios cuenta como no escrito.</summary>
    [EmailAddress(ErrorMessage = MensajeDeCorreo)]
    [StringLength(254, ErrorMessage = "Tu correo tiene a lo más 254 caracteres.")]
    public string? Correo
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Motivo de consulta: dato de salud (AD-23).</summary>
    [Required(ErrorMessage = "Cuéntanos en una o dos líneas el motivo de tu consulta.")]
    [StringLength(500, ErrorMessage = "El motivo tiene a lo más 500 caracteres.")]
    public string? Motivo { get; set; }

    /// <summary>Aceptó el aviso de privacidad (AD-24).</summary>
    [AllowedValues(true, ErrorMessage = "Para agendar tu cita necesitas aceptar el Aviso de privacidad.")]
    public bool Consentimiento { get; set; }
}

/// <summary>La Cita recién agendada, con lo que muestra Cita confirmada.</summary>
/// <param name="CitaId">El id de la Cita.</param>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local del consultorio.</param>
/// <param name="TipoDeVisita">Nombre del tipo: "Primera consulta".</param>
/// <param name="DuracionMin">Duración en minutos.</param>
/// <param name="Lugar">Hospital y consultorio, de Contenido.</param>
/// <param name="Direccion">Calle, número, colonia y ciudad, de Contenido.</param>
public sealed record CitaAgendadaEnLinea(
    CitaId CitaId,
    DateOnly Fecha,
    TimeOnly Hora,
    string TipoDeVisita,
    int DuracionMin,
    string Lugar,
    string Direccion);

/// <summary>El conjunto cerrado de códigos de fallo esperado de Agenda.</summary>
public static class CodigosDeAgenda
{
    /// <summary>Otra Cita ya ocupa ese horario (RN-1, AD-8).</summary>
    public const string HorarioOcupado = "agenda.horario_ocupado";

    /// <summary>El tipo de visita no existe, no está activo o no se agenda en línea.</summary>
    public const string TipoNoReservable = "agenda.tipo_no_reservable";

    /// <summary>El horario ya pasó (INV-C4).</summary>
    public const string EnElPasado = "agenda.en_el_pasado";

    /// <summary>No cabe en un tramo vigente de su fecha o no empieza en la rejilla (INV-C5).</summary>
    public const string FueraDeHorario = "agenda.fuera_de_horario";

    /// <summary>Antes de la anticipación mínima o después de la ventana de reserva (INV-C5).</summary>
    public const string FueraDeVentana = "agenda.fuera_de_ventana";
}
