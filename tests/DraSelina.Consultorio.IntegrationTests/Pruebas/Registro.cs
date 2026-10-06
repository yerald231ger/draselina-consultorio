using DraSelina.Consultorio.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Da de alta los tipos de prueba como se da de alta un módulo: su contexto (con las opciones como singleton,
/// igual que los de producción), su puerto y su renglón en la política de transacción del host.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddPruebas(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<PruebasDbContext>(
                opciones => opciones.UseNpgsql(cadenaDeConexion),
                optionsLifetime: ServiceLifetime.Singleton)
            .AddScoped<IAnotaciones, Anotaciones>()
            .AddScoped<ServicioDeOtroModulo>()
            .AddTransaccionDeModulo<PruebasDbContext>(typeof(AnotarHandler).Namespace!);
}
