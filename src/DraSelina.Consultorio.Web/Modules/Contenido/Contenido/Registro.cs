using DraSelina.Consultorio.Contenido.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Contenido;

/// <summary>Lo que el host registra de Contenido: su API. No tiene contexto ni esquema (AD-2).</summary>
public static class Registro
{
    public static IServiceCollection AddContenido(this IServiceCollection servicios) =>
        servicios
            .AddSingleton<IContenidoApi, ContenidoApi>();
}
