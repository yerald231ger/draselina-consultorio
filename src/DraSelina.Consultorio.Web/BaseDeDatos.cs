using Microsoft.Extensions.Configuration;

namespace DraSelina.Consultorio.Web;

/// <summary>La única base del sistema. La cadena de conexión la genera y la entrega el AppHost de Aspire.</summary>
public static class BaseDeDatos
{
    /// <summary>Nombre del recurso de base de datos en el AppHost y de su cadena de conexión.</summary>
    public const string Nombre = "consultorio";

    public static string CadenaDeConexion(IConfiguration configuracion) =>
        configuracion.GetConnectionString(Nombre)
        ?? throw new InvalidOperationException(
            $"Falta la cadena de conexión '{Nombre}'. La entrega el AppHost: ./dotnet.sh run --project src/DraSelina.Consultorio.AppHost");
}
