using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El archivo de calendario por Enlace de gestión. Una consulta: no abre transacción ni cambia estado. Busca por el
/// hash del token y arma el archivo con la misma regla y el mismo generador que <see cref="AgendaApi"/>.
/// </summary>
public sealed class ArchivoDeCalendarioPorEnlaceHandler(ICitasDelCalendario citas, AgendaApi agenda)
    : IQueryHandler<ArchivoDeCalendarioPorEnlace, ArchivoDeCalendario>
{
    public async Task<Resultado<ArchivoDeCalendario>> HandleAsync(
        ArchivoDeCalendarioPorEnlace consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // Un token mal formado no es de ninguna Cita: ni se consulta la base.
        var archivo = EnlaceDeGestion.HashDelToken(consulta.Token) is { } hash
            ? agenda.Archivo(await citas.PorTokenHashAsync(hash, cancellationToken))
            : null;

        return archivo is null
            ? Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<ArchivoDeCalendario>()
            : Resultado.Exito(archivo);
    }
}
