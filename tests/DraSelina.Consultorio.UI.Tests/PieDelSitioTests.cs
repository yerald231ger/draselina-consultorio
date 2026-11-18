using Bunit;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.UI.Componentes;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// El pie de las páginas públicas (entrada 7.3) toma nombre, especialidad y cédulas de Contenido, sin texto fijo.
/// </summary>
public sealed class PieDelSitioTests : BunitContext
{
    [Fact]
    public void Muestra_las_cedulas_que_tenga_Contenido()
    {
        var doctora = Contenido.ContenidoApi.Doctora with { CedulaProfesional = "99999999", CedulaDeEspecialidad = "88888888" };
        Services.AddSingleton<IContenidoApi>(new ContenidoDePrueba { DoctoraDePrueba = doctora });

        var pie = Render<PieDelSitio>();

        Assert.Equal(
            "Dra. Selina Medina Castillo · Traumatología y Ortopedia · Céd. Prof. 99999999 · Céd. Esp. 88888888",
            pie.Find(".dsm-pie__fila span").TextContent);
        Assert.Equal("/aviso-de-privacidad", pie.Find("a.dsm-pie__enlace").GetAttribute("href"));
    }
}
