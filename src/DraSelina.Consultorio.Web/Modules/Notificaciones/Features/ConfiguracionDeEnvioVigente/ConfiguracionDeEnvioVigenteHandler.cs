using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>Los valores de <c>configuracion_de_envio</c>. Una consulta: no abre transacción ni cambia nada.</summary>
public sealed class ConfiguracionDeEnvioVigenteHandler(IConfiguracionDeEnvio configuracion)
    : IQueryHandler<ConfiguracionDeEnvioVigente, ConfiguracionDeEnvioValores>
{
    public async Task<Resultado<ConfiguracionDeEnvioValores>> HandleAsync(
        ConfiguracionDeEnvioVigente consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await configuracion.LeerAsync(cancellationToken));
    }
}
