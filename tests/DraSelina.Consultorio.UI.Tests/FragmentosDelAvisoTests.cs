using Bunit;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.UI.Componentes;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Los tramos del aviso de privacidad: un dato que falta se ve como Pendiente y, cuando Contenido lo tiene, se ve el
/// dato (Gerardo, 2026-11-24).
/// </summary>
public sealed class FragmentosDelAvisoTests : BunitContext
{
    [Fact]
    public void Un_dato_que_falta_se_marca_como_pendiente()
    {
        var tramos = Render<FragmentosDelAviso>(p => p.Add(c => c.Fragmentos, [
            new TextoDelAviso("Escríbenos a "),
            new DatoDelAviso(null, ContenidoApi.CorreoPendiente, EnNegritas: true),
            new TextoDelAviso("."),
        ]));

        Assert.Equal(
            "Escríbenos a <strong><span class=\"dsm-pendiente\">[Pendiente: correo para las solicitudes]</span></strong>.",
            tramos.Markup);
    }

    [Fact]
    public void Un_dato_que_existe_se_muestra_y_el_enlace_se_abre_aparte_si_se_pide()
    {
        var tramos = Render<FragmentosDelAviso>(p => p
            .Add(c => c.Fragmentos, [
                new TextoDelAviso("Escribe a "),
                new DatoDelAviso("privacidad@consultorio.mx", ContenidoApi.CorreoPendiente, EnNegritas: true),
                new TextoDelAviso(". Lee el "),
                new EnlaceDelAviso("Aviso de privacidad completo", ContenidoApi.RutaDelAviso),
            ])
            .Add(c => c.EnOtraPestana, true));

        Assert.Equal(
            "Escribe a <strong>privacidad@consultorio.mx</strong>. Lee el "
            + "<a href=\"/aviso-de-privacidad\" target=\"_blank\" class=\"dsm-enlace-en-texto\">Aviso de privacidad completo</a>",
            tramos.Markup);
    }
}
