using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Una Cita como la ve el Administrador en su Detalle (CAP-12, CAP-14, CAP-17, FR-16, FR-17, FR-20; entrada 6): sus
/// datos, el contacto de su Paciente (lo da Pacientes, RN-29), su estado como en la Agenda, las acciones que proceden
/// ahora (RN-31) y su bitácora. Lo reúne Agenda; la página no compone módulos ni decide reglas (AD-10). Solo el
/// Administrador (AD-22). Solo lee. Una Cita que no existe vuelve <see cref="CodigosDeAgenda.CitaNoEncontrada"/>.
/// </summary>
/// <param name="CitaId">La Cita: lo único que viaja en la dirección (AD-11, AD-23).</param>
public sealed record DetalleDeCita(CitaId CitaId) : IQuery<CitaEnDetalle>, IRequiereAdministrador;

/// <summary>
/// El Detalle de una Cita. Nombre, teléfono, correo, nota y motivo nunca van a registros (AD-23): <see cref="ToString"/>
/// no los muestra.
/// </summary>
/// <param name="CitaId">La Cita.</param>
/// <param name="Paciente">El contacto del Paciente tal como se guardó; nulo si Pacientes no lo conoce.</param>
/// <param name="TipoDeVisita">El nombre de su tipo de consulta; nulo si el tipo ya no existe.</param>
/// <param name="Fecha">Fecha local de inicio.</param>
/// <param name="Inicio">Hora local de inicio.</param>
/// <param name="Fin">Hora local de fin.</param>
/// <param name="Origen">Quién la creó.</param>
/// <param name="Nota">La nota del Administrador; nula si no tiene.</param>
/// <param name="MotivoDeConsulta">Nulo si la Cita (del Consultorio) no lo tiene.</param>
/// <param name="Consentimiento">El Consentimiento dado al crearla (AD-24).</param>
/// <param name="Estado">Cómo se ve en la Agenda: Atendida se deriva (RN-19, RN-31).</param>
/// <param name="Cancelacion">Por qué se canceló; solo en <see cref="EstadoEnLaAgenda.Cancelada"/>.</param>
/// <param name="SinCorreo">
/// El Paciente no tiene correo guardado (RN-30): hay que avisarle a mano. También si Pacientes no lo conoce, como en la
/// Agenda.
/// </param>
/// <param name="PuedeMarcarNoSePresento">Agendada y su inicio ya llegó (RN-31): la regla de la Cita.</param>
/// <param name="PuedeCancelar">Agendada y su inicio no ha llegado (RN-31): la regla de la Cita.</param>
/// <param name="Bitacora">Todas sus entradas, en orden de ocurrencia (RN-24).</param>
public sealed record CitaEnDetalle(
    CitaId CitaId,
    ContactoDelPaciente? Paciente,
    string? TipoDeVisita,
    DateOnly Fecha,
    TimeOnly Inicio,
    TimeOnly Fin,
    OrigenEnLaAgenda Origen,
    string? Nota,
    string? MotivoDeConsulta,
    ConsentimientoEnDetalle Consentimiento,
    EstadoEnLaAgenda Estado,
    CancelacionEnLaAgenda? Cancelacion,
    bool SinCorreo,
    bool PuedeMarcarNoSePresento,
    bool PuedeCancelar,
    IReadOnlyList<EntradaDeLaBitacora> Bitacora)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"CitaId = {CitaId}, Paciente = {(Paciente is null ? "ninguno" : "oculto")}, Fecha = {Fecha:yyyy-MM-dd}, "
            + $"Inicio = {Inicio:HH:mm}, Fin = {Fin:HH:mm}, Origen = {Origen}, Nota = oculta, MotivoDeConsulta = oculto, "
            + $"Estado = {Estado}, Cancelacion = {Cancelacion}, SinCorreo = {SinCorreo}, "
            + $"PuedeMarcarNoSePresento = {PuedeMarcarNoSePresento}, PuedeCancelar = {PuedeCancelar}, Bitacora = {Bitacora.Count}");
        return true;
    }
}

/// <summary>A quién y adónde se le escribe al Paciente. Nunca va a registros (AD-23).</summary>
/// <param name="Nombre">Como se guardó.</param>
/// <param name="Telefono">En E.164, como <c>+524441234567</c>.</param>
/// <param name="Correo">Nulo si no dio correo.</param>
public sealed record ContactoDelPaciente(string Nombre, string Telefono, string? Correo)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append($"Nombre = oculto, Telefono = oculto, Correo = {(Correo is null ? "ninguno" : "oculto")}");
        return true;
    }
}

/// <summary>Quién creó la Cita, como lo ve el Administrador.</summary>
public enum OrigenEnLaAgenda
{
    /// <summary>"En línea": la reservó el Paciente desde el sitio.</summary>
    EnLinea = 1,

    /// <summary>"Del consultorio": la registró el Administrador.</summary>
    DelConsultorio = 2,
}

/// <summary>El Consentimiento de la Cita (AD-24, RN-23).</summary>
/// <param name="Canal">Por dónde llegó.</param>
/// <param name="Fecha">Fecha local en que se dio.</param>
/// <param name="Hora">Hora local en que se dio.</param>
/// <param name="VersionDelAviso">La versión del aviso de privacidad aceptada.</param>
public sealed record ConsentimientoEnDetalle(CanalEnDetalle Canal, DateOnly Fecha, TimeOnly Hora, string VersionDelAviso);

/// <summary>Por dónde llegó el Consentimiento.</summary>
public enum CanalEnDetalle
{
    /// <summary>El Paciente marcó la casilla al reservar en el sitio.</summary>
    EnLinea = 1,

    /// <summary>Lo obtuvo el consultorio por fuera del sitio.</summary>
    Externo = 2,
}

/// <summary>
/// Una entrada de la bitácora de la Cita (RN-24, CAP-17): cuándo, quién y qué, con lo que su detalle guarda. Nunca
/// motivo, nota ni contacto (AD-23).
/// </summary>
/// <param name="Fecha">Fecha local en que ocurrió.</param>
/// <param name="Hora">Hora local en que ocurrió.</param>
/// <param name="Quien">Paciente, Administrador o Sistema (AD-22).</param>
/// <param name="Accion">Qué se le hizo a la Cita.</param>
/// <param name="Origen">Solo en <see cref="AccionEnLaBitacora.Agendada"/>: en línea o del consultorio.</param>
/// <param name="Cancelacion">Solo en <see cref="AccionEnLaBitacora.Cancelada"/>: por el Paciente, tardía o por el Consultorio.</param>
/// <param name="De">Solo en <see cref="AccionEnLaBitacora.Reprogramada"/>: el inicio anterior, local.</param>
/// <param name="A">Solo en <see cref="AccionEnLaBitacora.Reprogramada"/>: el inicio nuevo, local.</param>
public sealed record EntradaDeLaBitacora(
    DateOnly Fecha,
    TimeOnly Hora,
    Actor Quien,
    AccionEnLaBitacora Accion,
    OrigenEnLaAgenda? Origen,
    CancelacionEnLaAgenda? Cancelacion,
    MomentoLocal? De,
    MomentoLocal? A);

/// <summary>Lo que se le hizo a una Cita, como lo muestra su bitácora.</summary>
public enum AccionEnLaBitacora
{
    Agendada = 1,
    Reprogramada = 2,
    Cancelada = 3,
    NoSePresento = 4,
}

/// <summary>Una fecha y una hora locales del consultorio (AD-17).</summary>
public sealed record MomentoLocal(DateOnly Fecha, TimeOnly Hora);
