using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.UI.Componentes;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Los errores de un Resultado inválido en el formulario (ErroresDelResultado y Campo): bajo su campo, con sus
/// atributos de accesibilidad, y el foco en el primero en orden de lectura. Una prueba por fila de la matriz del
/// plan, en SSR estático y en interactivo. Los mensajes salen del decorador real de validación.
/// </summary>
public sealed partial class ErroresEnElFormularioTests : BunitContext
{
    private static readonly RendererInfo Estatico = new("Static", isInteractive: false);
    private static readonly RendererInfo Interactivo = new("Server", isInteractive: true);

    public ErroresEnElFormularioTests()
    {
        Services.AddScoped<ICommandHandler<ComandoDePrueba, int>>(
            _ => new ValidacionDeComandos<ComandoDePrueba, int>(new HandlerDePrueba()));
    }

    [Fact]
    public void Sin_resultado_no_hay_mensajes_ni_aria_invalid_ni_autofocus()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>();

        Assert.Empty(formulario.FindAll(".dsm-campo__error"));
        Assert.Empty(formulario.FindAll(".dsm-errores-del-objeto"));
        Assert.Empty(formulario.FindAll("[aria-invalid]"));
        Assert.Empty(formulario.FindAll("[autofocus]"));
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void Invalido_en_SSR_cada_mensaje_bajo_su_campo_y_autofocus_solo_en_el_primero()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>();

        Enviar(formulario, nombre: "", correo: "no-es-correo");

        Assert.Equal([ComandoDePrueba.MensajeNombreRequerido], Mensajes(formulario, "Nombre"));
        Assert.Equal([ComandoDePrueba.MensajeCorreoMalFormado], Mensajes(formulario, "Correo"));

        var nombre = formulario.Find("#Nombre");
        var correo = formulario.Find("#Correo");
        Assert.Equal("true", nombre.GetAttribute("aria-invalid"));
        Assert.Equal("true", correo.GetAttribute("aria-invalid"));
        Assert.Equal("Nombre-error-1", nombre.GetAttribute("aria-describedby"));
        Assert.Equal("Correo-ayuda Correo-error-1", correo.GetAttribute("aria-describedby"));

        var conAutofocus = formulario.FindAll("[autofocus]");
        Assert.Equal("Nombre", Assert.Single(conAutofocus).Id);

        // En SSR estático no se llama a JS: el navegador aplica autofocus al cargar la página.
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void Un_campo_con_dos_errores_muestra_los_dos_mensajes_bajo_el()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>();

        Enviar(formulario, nombre: "Ana", correo: "xy");

        Assert.Equal(
            [ComandoDePrueba.MensajeCorreoMalFormado, ComandoDePrueba.MensajeCorreoLargo],
            Mensajes(formulario, "Correo"));
        Assert.Empty(Mensajes(formulario, "Nombre"));
        Assert.Equal(
            "Correo-ayuda Correo-error-1 Correo-error-2",
            formulario.Find("#Correo").GetAttribute("aria-describedby"));
        Assert.Equal("Correo", Assert.Single(formulario.FindAll("[autofocus]")).Id);
    }

    [Fact]
    public void El_error_del_objeto_va_arriba_y_lleva_el_foco()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>();

        Enviar(formulario, nombre: "Ana", correo: "ana" + ComandoDePrueba.DominioSinEntrega);

        var delObjeto = formulario.Find(".dsm-errores-del-objeto");
        Assert.Equal(ComandoDePrueba.MensajeSinEntrega, delObjeto.TextContent.Trim());
        Assert.Equal("-1", delObjeto.GetAttribute("tabindex"));
        Assert.True(delObjeto.HasAttribute("autofocus"));

        // Arriba de los campos, en orden de lectura.
        Assert.True(Antes(formulario, "#errores-del-objeto", ".dsm-campo"), "El error del objeto no va arriba de los campos.");

        Assert.Equal([ComandoDePrueba.MensajeRevisaElNombre], Mensajes(formulario, "Nombre"));
        Assert.Equal("true", formulario.Find("#Nombre").GetAttribute("aria-invalid"));
        Assert.False(formulario.Find("#Nombre").HasAttribute("autofocus"));
        Assert.Single(formulario.FindAll("[autofocus]"));
    }

    [Fact]
    public void Invalido_interactivo_enfoca_el_primer_campo_con_error_una_vez()
    {
        SetRendererInfo(Interactivo);
        var formulario = Render<FormularioDePrueba>();

        Enviar(formulario, nombre: "", correo: "no-es-correo");

        Assert.Equal([ComandoDePrueba.MensajeNombreRequerido], Mensajes(formulario, "Nombre"));
        Assert.Equal([ComandoDePrueba.MensajeCorreoMalFormado], Mensajes(formulario, "Correo"));
        Assert.Empty(formulario.FindAll("[autofocus]"));

        var enfoque = JSInterop.VerifyFocusAsyncInvoke(calledTimes: 1).Single();
        var controlDeNombre = formulario.FindComponents<InputText>()
            .Single(c => Equals(c.Instance.AdditionalAttributes?["id"], "Nombre"));
        Assert.Equal(controlDeNombre.Instance.Element, (ElementReference)enfoque.Arguments[0]!);

        // Volver a pintar con el mismo Resultado no enfoca otra vez.
        formulario.Find("#Correo").Change("otro-intento");
        formulario.Render();
        JSInterop.VerifyFocusAsyncInvoke(calledTimes: 1);
    }

    [Fact]
    public void Corregido_los_mensajes_y_aria_invalid_desaparecen()
    {
        SetRendererInfo(Interactivo);
        var formulario = Render<FormularioDePrueba>();
        Enviar(formulario, nombre: "", correo: "no-es-correo");
        Assert.NotEmpty(formulario.FindAll(".dsm-campo__error"));

        Enviar(formulario, nombre: "Ana López", correo: "ana@ejemplo.com");

        Assert.Empty(formulario.FindAll(".dsm-campo__error"));
        Assert.Empty(formulario.FindAll(".dsm-errores-del-objeto"));
        Assert.Empty(formulario.FindAll("[aria-invalid]"));
        Assert.Equal("Correo-ayuda", formulario.Find("#Correo").GetAttribute("aria-describedby"));
        Assert.Null(formulario.Find("#Nombre").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Con_errores_el_marcado_no_trae_script_ni_color_y_cada_mensaje_va_en_su_campo_tras_el_control()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>();

        Enviar(formulario, nombre: "", correo: "xy");

        var html = formulario.Markup;
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(ColorLiteral(), html);

        var errores = formulario.FindAll(".dsm-campo__error");
        Assert.Equal(3, errores.Count);
        foreach (var error in errores)
        {
            var campo = error.Closest(".dsm-campo");
            Assert.NotNull(campo);
            var control = campo.QuerySelector("input");
            Assert.NotNull(control);
            Assert.True(Antes(formulario, $"#{control.Id}", $"#{error.Id}"), $"El mensaje '{error.TextContent}' no va después de su control.");
            Assert.Contains(error.Id!, control.GetAttribute("aria-describedby")!.Split(' '));
        }
    }

    [Fact]
    public void Campo_pinta_su_etiqueta_tipo_ayuda_y_atributos_adicionales()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>();

        var etiqueta = formulario.Find("label[for=Correo]");
        Assert.Equal("Correo", etiqueta.TextContent.Trim());
        Assert.Equal("email", formulario.Find("#Correo").GetAttribute("type"));
        Assert.Equal("text", formulario.Find("#Nombre").GetAttribute("type"));
        Assert.Equal("name", formulario.Find("#Nombre").GetAttribute("autocomplete"));
        Assert.Equal("Te enviamos la confirmación.", formulario.Find("#Correo-ayuda").TextContent.Trim());
    }

    [Fact]
    public void Los_atributos_de_quien_llama_no_reemplazan_class_ni_id_del_control()
    {
        SetRendererInfo(Estatico);
        var formulario = Render<FormularioDePrueba>(parametros => parametros
            .Add(f => f.AtributosDelNombre, new Dictionary<string, object> { ["class"] = "otra", ["id"] = "otro" }));

        var control = formulario.Find("label[for=Nombre] + input");
        Assert.Equal("Nombre", control.Id);
        Assert.Contains("dsm-campo__control", control.ClassList);
    }

    [Fact]
    public void Un_envio_que_deja_el_mismo_Resultado_conserva_aria_invalid()
    {
        SetRendererInfo(Interactivo);
        var formulario = Render<FormularioDePrueba>(parametros => parametros
            .Add(f => f.ConservarElPrimerResultado, true));
        Enviar(formulario, nombre: "", correo: "no-es-correo");
        Assert.Equal("true", formulario.Find("#Nombre").GetAttribute("aria-invalid"));

        Enviar(formulario, nombre: "", correo: "no-es-correo");

        Assert.Equal([ComandoDePrueba.MensajeNombreRequerido], Mensajes(formulario, "Nombre"));
        Assert.Equal("true", formulario.Find("#Nombre").GetAttribute("aria-invalid"));
        Assert.Equal("true", formulario.Find("#Correo").GetAttribute("aria-invalid"));
    }

    [Fact]
    public void Campo_fuera_de_ErroresDelResultado_falla_con_un_mensaje_que_lo_dice()
    {
        SetRendererInfo(Estatico);
        var comando = new ComandoDePrueba();

        var error = Assert.Throws<InvalidOperationException>(() => Render<Campo>(parametros => parametros
            .Add(c => c.Para, () => comando.Nombre)
            .Add(c => c.Etiqueta, "Nombre")));

        Assert.Contains(nameof(ErroresDelResultado), error.Message, StringComparison.Ordinal);
    }

    private static void Enviar(IRenderedComponent<FormularioDePrueba> formulario, string nombre, string correo)
    {
        formulario.Find("#Nombre").Change(nombre);
        formulario.Find("#Correo").Change(correo);
        formulario.Find("form").Submit();
    }

    /// <summary>Verdadero si el primer elemento que cumple <paramref name="primero"/> va antes que el primero que cumple <paramref name="despues"/>, en el orden del documento.</summary>
    private static bool Antes(IRenderedComponent<FormularioDePrueba> formulario, string primero, string despues)
    {
        var enOrden = formulario.FindAll("*").ToList();
        var a = enOrden.FindIndex(e => e.Matches(primero));
        var b = enOrden.FindIndex(e => e.Matches(despues));
        Assert.True(a >= 0 && b >= 0, "Los dos elementos deben estar en el formulario.");
        return a < b;
    }

    /// <summary>Los mensajes que el campo de esa propiedad muestra bajo su control, en orden.</summary>
    private static List<string> Mensajes(IRenderedComponent<FormularioDePrueba> formulario, string propiedad) =>
        formulario.Find($"#{propiedad}").Closest(".dsm-campo")!
            .QuerySelectorAll(".dsm-campo__error")
            .Select(e => e.TextContent.Trim())
            .ToList();

    [GeneratedRegex("#[0-9A-Fa-f]{3,8}\\b|rgb\\(|hsl\\(")]
    private static partial Regex ColorLiteral();
}
