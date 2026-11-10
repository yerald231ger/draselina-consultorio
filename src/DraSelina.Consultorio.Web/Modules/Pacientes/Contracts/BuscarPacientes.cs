using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Pacientes.Contracts;

/// <summary>
/// Los Pacientes cuyo nombre contiene <paramref name="Texto"/>, para la lista de Pacientes del Administrador (CAP-15,
/// FR-18; entrada 5.7). Busca sobre el nombre en minúsculas y sin acentos (AD-18), con el texto normalizado igual:
/// "lop" encuentra a "Mariana López" y a "Rosa Lopera". Sin texto, todos. En orden alfabético y a lo más
/// <see cref="Maximo"/>. Pacientes solo contesta quiénes son: nada de Citas (RN-29). Solo el Administrador (AD-22). Solo
/// lee. El texto es parte de un nombre: <see cref="ToString"/> no lo muestra (AD-23).
/// </summary>
/// <param name="Texto">Lo que escribió el Administrador; nulo o vacío para ver todos.</param>
public sealed record BuscarPacientes(string? Texto) : IQuery<PacientesEncontrados>, IRequiereAdministrador
{
    /// <summary>Lo más que devuelve una búsqueda.</summary>
    public const int Maximo = 50;

    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append($"Texto = {(string.IsNullOrWhiteSpace(Texto) ? "ninguno" : "oculto")}");
        return true;
    }
}

/// <summary>Lo que encontró <see cref="BuscarPacientes"/>.</summary>
/// <param name="Pacientes">A lo más <see cref="BuscarPacientes.Maximo"/>, en orden alfabético.</param>
/// <param name="HayMas">Hay más Pacientes que coinciden de los que caben.</param>
public sealed record PacientesEncontrados(IReadOnlyList<PacienteEncontrado> Pacientes, bool HayMas)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append($"Pacientes = {Pacientes.Count}, HayMas = {HayMas}");
        return true;
    }
}

/// <summary>
/// Un Paciente de la lista. Nombre y teléfono nunca van a registros (AD-23): <see cref="ToString"/> no los muestra.
/// </summary>
/// <param name="PatientId">El Paciente: lo único que viaja en la dirección de su historial (AD-11, AD-23).</param>
/// <param name="Nombre">Como se guardó.</param>
/// <param name="Telefono">En E.164, como <c>+524441234567</c>.</param>
/// <param name="TieneCorreo">Si tiene correo guardado (RN-30); el correo no sale aquí.</param>
public sealed record PacienteEncontrado(PatientId PatientId, string Nombre, string Telefono, bool TieneCorreo)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append($"PatientId = {PatientId}, Nombre = oculto, Telefono = oculto, TieneCorreo = {TieneCorreo}");
        return true;
    }
}
