using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador registra una Cita del consultorio: la que se pactó por teléfono o WhatsApp, o la que Selina aceptó
/// fuera de horario (CAP-13, FR-15, UJ-4, UJ-6). No aplica Agenda base ni custom, rejilla, ventana ni tope semanal
/// (RN-13, AD-7); la impiden el pasado y otra Cita en ese horario. Fuera de los Tramos vigentes de su fecha, la nota es
/// obligatoria (INV-C6). Cualquier tipo de visita activo. Los datos de contacto pasan a Pacientes (E-12); el correo, si
/// llega, reemplaza al guardado (RN-30). Solo el Administrador (AD-22). Nombre, teléfono, correo, motivo y nota nunca
/// van a registros (AD-23): <see cref="ToString"/> no los muestra.
/// </summary>
public sealed class AgendarCitaPorConsultorio : ICommand<CitaAgendadaPorConsultorio>, IRequiereAdministrador
{
    public const string MensajeDeTelefono = "Revisa el número: deben ser 10 dígitos.";
    public const string MensajeDeTelefonoDeMexico = "Revisa el número: no corresponde a un celular de México.";
    public const string MensajeDeNombre = "Escribe el nombre completo del paciente.";
    public const string MensajeDeCorreo = "Revisa el correo: debe ser como nombre@correo.com.";
    public const string MensajeDeConsentimiento = "Confirma que el paciente aceptó el Aviso de privacidad.";

    [Required(ErrorMessage = "Falta el tipo de consulta.")]
    public TipoDeVisitaId? TipoDeVisitaId { get; set; }

    /// <summary>Fecha local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta el día de la cita.")]
    public DateOnly? Fecha { get; set; }

    /// <summary>Hora local del consultorio (AD-17); libre, fuera de la rejilla incluso.</summary>
    [Required(ErrorMessage = "Falta la hora de la cita.")]
    public TimeOnly? Hora { get; set; }

    [Required(ErrorMessage = MensajeDeNombre)]
    [StringLength(120, ErrorMessage = "El nombre tiene a lo más 120 caracteres.")]
    public string? Nombre { get; set; }

    /// <summary>Celular de México: 10 dígitos, con o sin <c>+52</c>.</summary>
    [Required(ErrorMessage = "Escribe el celular del paciente.")]
    [TelefonoDeDiezDigitos(ErrorMessage = MensajeDeTelefono)]
    public string? Telefono { get; set; }

    /// <summary>Opcional. Vacío o con solo espacios cuenta como no escrito.</summary>
    [EmailAddress(ErrorMessage = MensajeDeCorreo)]
    [StringLength(254, ErrorMessage = "El correo tiene a lo más 254 caracteres.")]
    public string? Correo
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Motivo de consulta, opcional: dato de salud (AD-23). Vacío cuenta como no escrito.</summary>
    [StringLength(500, ErrorMessage = "El motivo tiene a lo más 500 caracteres.")]
    public string? Motivo { get; set; }

    /// <summary>
    /// La nota del Administrador. Opcional dentro de los Tramos vigentes; obligatoria fuera de ellos (INV-C6), y eso lo
    /// decide Agenda con los Tramos: sin ella vuelve <see cref="CodigosDeAgenda.NotaObligatoria"/>.
    /// </summary>
    [StringLength(500, ErrorMessage = "La nota tiene a lo más 500 caracteres.")]
    public string? Nota { get; set; }

    /// <summary>
    /// El Administrador confirma que el Paciente aceptó el aviso de privacidad por fuera del sitio (AD-24): se guarda con
    /// canal externo y la versión vigente del aviso.
    /// </summary>
    [AllowedValues(true, ErrorMessage = MensajeDeConsentimiento)]
    public bool Consentimiento { get; set; }

    public override string ToString() =>
        $"AgendarCitaPorConsultorio(TipoDeVisitaId = {TipoDeVisitaId}, Fecha = {Fecha:yyyy-MM-dd}, Hora = {Hora:HH:mm}, "
        + $"Nombre = oculto, Telefono = oculto, Correo = oculto, Motivo = oculto, Nota = oculta, Consentimiento = {Consentimiento})";
}

/// <summary>La Cita del consultorio recién agendada.</summary>
/// <param name="CitaId">El id de la Cita.</param>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local del consultorio.</param>
/// <param name="FueraDeHorario">No cabe completa en ningún Tramo vigente de su fecha (INV-C6): lleva nota.</param>
public sealed record CitaAgendadaPorConsultorio(CitaId CitaId, DateOnly Fecha, TimeOnly Hora, bool FueraDeHorario);
