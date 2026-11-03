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

    /// <summary>Los ids de los Pacientes con ese teléfono, con cualquier nombre (INV-P3). Sin rastrear.</summary>
    Task<IReadOnlyList<PatientId>> ConTelefonoAsync(Telefono telefono, CancellationToken cancellationToken);

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
