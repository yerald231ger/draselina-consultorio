using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using DraSelina.Consultorio.Pacientes.Domain;

namespace DraSelina.Consultorio.Pacientes.Features;

/// <summary>
/// La lista de Pacientes del Administrador (entrada 5.7). Una consulta: no abre transacción ni cambia estado. El texto se
/// normaliza como el nombre guardado para buscar (<see cref="NombreCompleto.ParaBuscar"/>, AD-18): "  LÓPEZ " busca
/// "lopez". Se lee uno más del máximo para saber si hay más. Ni este handler ni su puerto registran el texto, nombres
/// ni teléfonos (AD-23).
/// </summary>
public sealed class BuscarPacientesHandler(IBusquedaDePacientes busqueda) : IQueryHandler<BuscarPacientes, PacientesEncontrados>
{
    public async Task<Resultado<PacientesEncontrados>> HandleAsync(BuscarPacientes consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var leidos = await busqueda.BuscarAsync(
            NombreCompleto.ParaBuscar(consulta.Texto), BuscarPacientes.Maximo + 1, cancellationToken);

        return Resultado.Exito(new PacientesEncontrados(
            [.. leidos.Take(BuscarPacientes.Maximo).Select(p => new PacienteEncontrado(p.Id, p.Nombre, p.Telefono, p.TieneCorreo))],
            HayMas: leidos.Count > BuscarPacientes.Maximo));
    }
}
