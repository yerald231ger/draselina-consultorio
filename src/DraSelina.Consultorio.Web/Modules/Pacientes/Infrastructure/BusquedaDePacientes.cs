using DraSelina.Consultorio.Pacientes.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>
/// <see cref="IBusquedaDePacientes"/> sobre <see cref="PacientesDbContext"/>: proyecta, sin rastrear. "Contiene" sobre
/// <c>nombre_busqueda</c>, que no lleva la collation no determinista y por eso admite <c>LIKE</c>; Npgsql escapa
/// <c>%</c> y <c>_</c> del texto.
/// </summary>
public sealed class BusquedaDePacientes(PacientesDbContext db) : IBusquedaDePacientes
{
    public async Task<IReadOnlyList<PacienteLeidoDeLaBusqueda>> BuscarAsync(
        string texto, int cuantos, CancellationToken cancellationToken)
    {
        var pacientes = db.Pacientes.AsNoTracking();
        if (texto.Length > 0)
        {
            pacientes = pacientes.Where(p => p.Nombre.Busqueda.Contains(texto));
        }

        // Alfabético sin distinguir mayúsculas ni acentos; el id desempata a dos con el mismo nombre.
        var leidos = await pacientes
            .OrderBy(p => p.Nombre.Busqueda)
            .ThenBy(p => p.Id)
            .Take(cuantos)
            .Select(p => new { p.Id, p.Nombre.Original, p.Telefono, TieneCorreo = p.Correo != null })
            .ToListAsync(cancellationToken);

        return [.. leidos.Select(p => new PacienteLeidoDeLaBusqueda(p.Id, p.Original, p.Telefono.Valor, p.TieneCorreo))];
    }
}
