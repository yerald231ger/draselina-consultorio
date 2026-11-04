using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary><see cref="IAgendaApi"/>. El enlace se deriva cada vez; no se guarda ni se registra (AD-19, AD-23).</summary>
public sealed class AgendaApi(IOptions<OpcionesDeEnlaceDeGestion> opciones) : IAgendaApi
{
    private readonly ClaveDeGestion _clave = opciones.Value.ClaveDeGestion();
    private readonly string _urlBase = opciones.Value.UrlBase!.TrimEnd('/');

    public string ObtenerEnlaceDeGestion(CitaId citaId) =>
        $"{_urlBase}/c/{EnlaceDeGestion.Derivar(_clave, citaId).Token}";
}
