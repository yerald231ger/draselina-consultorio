using System.Text.Json;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="IBitacoraDeLaCita"/> sobre <c>agenda.bitacora</c>, sin rastrear, por el índice
/// <c>ix_bitacora_entidad_entidad_id_ocurrido_en</c>. En orden de ocurrencia; a igual instante, por su id, un Guid v7
/// que sigue el orden de inserción. Del detalle (jsonb) lee solo <c>origen</c>, <c>motivo</c>, <c>inicioAnterior</c> e
/// <c>inicioNuevo</c>, con los textos de <see cref="TextoDeEnumeraciones"/>.
/// </summary>
public sealed class BitacoraDeLaCita(AgendaDbContext db) : IBitacoraDeLaCita
{
    public async Task<IReadOnlyList<EntradaLeidaDeLaBitacora>> DeLaCitaAsync(CitaId cita, CancellationToken cancellationToken)
    {
        var entidad = nameof(EntidadDeBitacora.Cita);
        var entidadId = cita.ToString();
        var filas = await db.Set<FilaDeBitacora>()
            .AsNoTracking()
            .Where(f => f.Entidad == entidad && f.EntidadId == entidadId)
            .OrderBy(f => f.OcurridoEn)
            .ThenBy(f => f.Id)
            .Select(f => new { f.OcurridoEn, f.ActorTipo, f.Accion, f.Detalle })
            .ToListAsync(cancellationToken);

        return [.. filas.Select(f => Leida(f.OcurridoEn, f.ActorTipo, f.Accion, f.Detalle))];
    }

    private static EntradaLeidaDeLaBitacora Leida(DateTimeOffset ocurridoEn, string actor, string accion, string? detalle)
    {
        OrigenDeCita? origen = null;
        MotivoDeCancelacion? motivo = null;
        DateTimeOffset? inicioAnterior = null;
        DateTimeOffset? inicioNuevo = null;

        if (detalle is not null)
        {
            using var documento = JsonDocument.Parse(detalle);
            var raiz = documento.RootElement;
            if (Texto(raiz, "origen") is { } textoDeOrigen)
            {
                origen = TextoDeEnumeraciones.Origen(textoDeOrigen);
            }

            if (Texto(raiz, "motivo") is { } textoDeMotivo)
            {
                motivo = TextoDeEnumeraciones.Motivo(textoDeMotivo);
            }

            inicioAnterior = Instante(raiz, "inicioAnterior");
            inicioNuevo = Instante(raiz, "inicioNuevo");
        }

        return new EntradaLeidaDeLaBitacora(
            ocurridoEn.ToUniversalTime(),
            Enum.Parse<Actor>(actor),
            Enum.Parse<AccionDeBitacora>(accion),
            origen,
            motivo,
            inicioAnterior,
            inicioNuevo);
    }

    private static string? Texto(JsonElement raiz, string propiedad) =>
        raiz.ValueKind == JsonValueKind.Object
        && raiz.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static DateTimeOffset? Instante(JsonElement raiz, string propiedad) =>
        raiz.ValueKind == JsonValueKind.Object
        && raiz.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && valor.TryGetDateTimeOffset(out var instante)
            ? instante.ToUniversalTime()
            : null;
}
