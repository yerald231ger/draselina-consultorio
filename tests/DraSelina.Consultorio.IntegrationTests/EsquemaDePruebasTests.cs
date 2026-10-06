using DraSelina.Consultorio.IntegrationTests.Pruebas;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>Los tipos de prueba tienen su lugar: el esquema <c>pruebas</c>, que crea el arnés.</summary>
public sealed class EsquemaDePruebasTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_tipo_de_prueba_se_guarda_y_se_lee_de_vuelta()
    {
        var apunte = new Apunte { Id = Guid.CreateVersion7(), Texto = $"apunte {Guid.NewGuid():N}" };

        await using (var escritura = arnes.CrearContextoDePruebas())
        {
            escritura.Apuntes.Add(apunte);
            await escritura.SaveChangesAsync(Cancelacion);
        }

        await using var lectura = arnes.CrearContextoDePruebas();
        var leido = await lectura.Apuntes.AsNoTracking().SingleAsync(a => a.Id == apunte.Id, Cancelacion);

        Assert.Equal(apunte.Texto, leido.Texto);
    }

    [Fact]
    public async Task La_tabla_de_un_tipo_de_prueba_esta_solo_en_el_esquema_de_pruebas()
    {
        await using var contexto = arnes.CrearContextoDePruebas();
        var tabla = contexto.Model.FindEntityType(typeof(Apunte))!.GetTableName()!;

        var esquemas = await Consultas.EsquemasConTablaAsync(arnes.CadenaDeConexion, tabla, Cancelacion);

        Assert.Equal([PruebasDbContext.Esquema], esquemas);
    }
}
