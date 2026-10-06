using JasperFx;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// El almacenamiento de mensajes de Wolverine, definido una sola vez: misma base y mismo esquema para
/// el MigrationService, que lo crea, y para la web, que lo usa sin crearlo (AD-2, AD-25). Si no existe, la web
/// no arranca.
/// </summary>
public static class AlmacenamientoDeWolverine
{
    public const string Esquema = "wolverine";

    /// <summary>
    /// Apunta Wolverine a su esquema y le prohíbe crear o cambiar tablas al arrancar:
    /// quien llame a esto nunca migra por accidente.
    /// </summary>
    public static void Configurar(WolverineOptions opciones, string cadenaDeConexion)
    {
        opciones.PersistMessagesWithPostgresql(cadenaDeConexion, Esquema);
        opciones.AutoBuildMessageStorageOnStartup = AutoCreate.None;
    }

    /// <summary>Crea o actualiza las tablas del esquema. Solo lo llama el MigrationService.</summary>
    public static Task CrearAsync(IServiceProvider servicios) =>
        servicios.GetRequiredService<IMessageStore>().Admin.MigrateAsync();
}
