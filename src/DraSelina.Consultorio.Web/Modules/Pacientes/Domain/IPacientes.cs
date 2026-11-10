using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Pacientes.Domain;

/// <summary>
/// El puerto de escritura de <see cref="Paciente"/> (T-16). Guarda de forma explícita dentro de la transacción del
/// llamador.
/// </summary>
public interface IPacientes
{
    /// <summary>
    /// El Paciente con ese teléfono y ese nombre, según la igualdad de la base: sin distinguir mayúsculas, acentos ni
    /// espacios de más (RN-25, RN-26). Nulo si no existe.
    /// </summary>
    Task<Paciente?> BuscarAsync(Telefono telefono, NombreCompleto nombre, CancellationToken cancellationToken);

    /// <summary>
    /// El id del Paciente con ese teléfono y ese nombre, con la misma igualdad de <see cref="BuscarAsync"/>; nulo si no
    /// existe. Sin rastrear: en un ámbito largo (el circuito de administración) no deja un Paciente en caché.
    /// </summary>
    Task<PatientId?> IdAsync(Telefono telefono, NombreCompleto nombre, CancellationToken cancellationToken);

    /// <summary>Los ids de los Pacientes con ese teléfono, con cualquier nombre (INV-P3). Sin rastrear.</summary>
    Task<IReadOnlyList<PatientId>> ConTelefonoAsync(Telefono telefono, CancellationToken cancellationToken);

    /// <summary>Si el Paciente tiene correo guardado; falso si no existe. Sin rastrear.</summary>
    Task<bool> TieneCorreoAsync(PatientId paciente, CancellationToken cancellationToken);

    /// <summary>El nombre del Paciente como lo escribió (<see cref="NombreCompleto.Original"/>); nulo si no existe. Sin rastrear.</summary>
    Task<string?> NombreAsync(PatientId paciente, CancellationToken cancellationToken);

    /// <summary>
    /// El nombre como lo escribió y el correo guardado (<see cref="CorreoElectronico.Valor"/>, nulo si no tiene); nulo
    /// si el Paciente no existe. Sin rastrear.
    /// </summary>
    Task<(string Nombre, string? Correo)?> ContactosAsync(PatientId paciente, CancellationToken cancellationToken);

    /// <summary>
    /// Lo mismo que <see cref="ContactosAsync(PatientId, CancellationToken)"/> para varios Pacientes, en una sola
    /// consulta. El que no existe no aparece. Sin rastrear.
    /// </summary>
    Task<IReadOnlyDictionary<PatientId, (string Nombre, string? Correo)>> ContactosAsync(
        IReadOnlyCollection<PatientId> ids, CancellationToken cancellationToken);

    /// <summary>
    /// El nombre como lo escribió, el teléfono en E.164 (<see cref="Telefono.Valor"/>) y el correo guardado (nulo si no
    /// tiene); nulo si el Paciente no existe. Sin rastrear.
    /// </summary>
    Task<(string Nombre, string Telefono, string? Correo)?> ContactoConTelefonoAsync(
        PatientId paciente, CancellationToken cancellationToken);

    void Agregar(Paciente paciente);

    /// <summary>
    /// Guarda lo agregado y lo cambiado. Si un Paciente agregado choca con el índice único de identidad (otro lo
    /// registró antes), no lanza: devuelve <see cref="GuardadoDePaciente.IdentidadRepetida"/> y la transacción queda
    /// abortada; quien llama la deshace.
    /// </summary>
    Task<GuardadoDePaciente> GuardarAsync(CancellationToken cancellationToken);
}

/// <summary>Cómo terminó <see cref="IPacientes.GuardarAsync"/>.</summary>
public enum GuardadoDePaciente
{
    Guardado = 1,
    IdentidadRepetida = 2,
}
