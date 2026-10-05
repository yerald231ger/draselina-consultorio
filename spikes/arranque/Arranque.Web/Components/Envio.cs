using Arranque.Contracts;
using Microsoft.AspNetCore.Components.Forms;
using Wolverine;

namespace Arranque.Web.Components;

/// <summary>Lo que hacen las tres páginas al enviar: despachar el comando y pasar el Resultado al formulario.</summary>
public sealed class Envio
{
    /// <summary>"guardada", "invalida" o "excepcion"; vacío antes del envío.</summary>
    public string Estado { get; private set; } = "";

    /// <summary>Tipo de la excepción que recibió quien invoca, si la hubo.</summary>
    public string Excepcion { get; private set; } = "";

    public async Task Enviar(IMessageBus bus, object comando, EditContext contexto, ValidationMessageStore mensajes)
    {
        Resultado? resultado;
        try
        {
            resultado = await bus.InvokeAsync<Resultado>(comando);
        }
        catch (Exception e)
        {
            // Se atrapa solo para dejar a la vista QUÉ recibió quien invoca (pregunta 2).
            Estado = "excepcion";
            Excepcion = e.GetType().FullName ?? e.GetType().Name;
            return;
        }

        if (resultado is null)
        {
            Estado = "sin-resultado";
            return;
        }

        if (resultado.EsValido)
        {
            Estado = "guardada";
            return;
        }

        Estado = "invalida";
        foreach (var error in resultado.Errores)
        {
            mensajes.Add(contexto.Field(error.Campo), error.Mensaje);
        }
        contexto.NotifyValidationStateChanged();
    }
}
