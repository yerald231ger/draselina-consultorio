using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Pacientes.Contracts;

/// <summary>Lo que Pacientes ofrece a otros módulos (AD-3, E-13). Contesta sobre identidad y contacto, nada más (RN-29).</summary>
public interface IPacientesApi
{
    /// <summary>
    /// El Paciente con ese teléfono y ese nombre, o uno nuevo si no existe (RN-25 a RN-28, AD-28). Confirma en su
    /// propia transacción antes de devolver: un Paciente sin Cita es un resultado aceptable. Es idempotente: dos
    /// llamadas con los mismos datos, aun simultáneas, dan el mismo <see cref="PatientId"/>. Si el Paciente existe y
    /// llega otro correo, el nuevo reemplaza al guardado (RN-30); sin correo, el guardado se conserva.
    /// </summary>
    /// <param name="nombre">Como lo escribió la persona.</param>
    /// <param name="telefono">Celular de México: 10 dígitos, con o sin <c>+52</c>, espacios o guiones.</param>
    /// <param name="correo">Opcional.</param>
    /// <returns>
    /// El <see cref="PatientId"/>; o un fallo con un código de <see cref="CodigosDePacientes"/> si algún dato no es
    /// válido, sin registrar nada.
    /// </returns>
    Task<Resultado<PatientId>> BuscarOCrearAsync(
        string nombre,
        string telefono,
        string? correo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Los Pacientes que tienen ese teléfono, con cualquier nombre (RN-25, INV-P3). Es para el tope semanal de Agenda
    /// (AD-13, RN-18): Pacientes solo contesta quiénes son; nunca cuenta Citas (RN-29). No registra nada.
    /// </summary>
    /// <param name="telefono">Celular de México, en la misma forma que acepta <see cref="BuscarOCrearAsync"/>.</param>
    /// <returns>Sus <see cref="PatientId"/>; vacía si no hay ninguno o si el teléfono no es válido.</returns>
    Task<IReadOnlyList<PatientId>> ConTelefonoAsync(string telefono, CancellationToken cancellationToken);

    /// <summary>
    /// Si el Paciente tiene un correo guardado (RN-30): a él le llegarán los avisos. Solo contesta sí o no; el correo
    /// no sale de Pacientes (RN-29). No registra nada.
    /// </summary>
    /// <returns>Falso si no tiene correo o si no existe.</returns>
    Task<bool> TieneCorreoAsync(PatientId paciente, CancellationToken cancellationToken);

    /// <summary>
    /// El nombre del Paciente tal como se guardó: el que se muestra al propio Paciente en Tu cita (RN-29). Quien lo
    /// recibe no lo escribe en registros (AD-23). No registra nada.
    /// </summary>
    /// <returns>Nulo si el Paciente no existe.</returns>
    Task<string?> NombreAsync(PatientId paciente, CancellationToken cancellationToken);
}

/// <summary>El conjunto cerrado de códigos de fallo esperado de Pacientes.</summary>
public static class CodigosDePacientes
{
    /// <summary>El teléfono no es un número válido en México.</summary>
    public const string TelefonoInvalido = "pacientes.telefono_invalido";

    /// <summary>El nombre está vacío, no tiene letras o es demasiado largo.</summary>
    public const string NombreInvalido = "pacientes.nombre_invalido";

    /// <summary>El correo no tiene forma de correo.</summary>
    public const string CorreoInvalido = "pacientes.correo_invalido";
}
