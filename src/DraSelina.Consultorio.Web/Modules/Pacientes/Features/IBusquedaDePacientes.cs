using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Pacientes.Features;

/// <summary>
/// Puerto de lectura de la lista de Pacientes (entrada 5.7): sin rastrear y sin transacción. No registra lo que lee
/// (AD-23).
/// </summary>
public interface IBusquedaDePacientes
{
    /// <summary>
    /// Los Pacientes cuyo <c>nombre_busqueda</c> contiene <paramref name="texto"/> en cualquier parte (todos si es
    /// vacío), en orden alfabético de ese nombre, a lo más <paramref name="cuantos"/>.
    /// </summary>
    /// <param name="texto">Ya en la forma de <c>NombreCompleto.Busqueda</c>: minúsculas y sin acentos.</param>
    Task<IReadOnlyList<PacienteLeidoDeLaBusqueda>> BuscarAsync(string texto, int cuantos, CancellationToken cancellationToken);
}

/// <summary>Un Paciente como lo lee <see cref="IBusquedaDePacientes"/>.</summary>
/// <param name="Nombre">Como se guardó.</param>
/// <param name="Telefono">En E.164.</param>
/// <param name="TieneCorreo">Si tiene correo guardado.</param>
public sealed record PacienteLeidoDeLaBusqueda(PatientId Id, string Nombre, string Telefono, bool TieneCorreo)
{
    // Nombre y teléfono nunca van a registros (AD-23).
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append($"Id = {Id}, Nombre = oculto, Telefono = oculto, TieneCorreo = {TieneCorreo}");
        return true;
    }
}
