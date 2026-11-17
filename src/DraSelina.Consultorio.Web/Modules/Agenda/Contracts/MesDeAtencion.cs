using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los días de un mes del Calendario de atención (CAP-11, FR-13), en fechas locales del consultorio (AD-17): por cada
/// día, cómo se distingue (con la misma clasificación que la Agenda), el motivo de su Agenda custom y sus Tramos
/// vigentes. Solo el Administrador (AD-22). Solo lee.
/// </summary>
/// <param name="Anio">El año, de 2000 a 9999.</param>
/// <param name="Mes">El mes, de 1 a 12.</param>
public sealed record MesDeAtencion(int Anio, int Mes) : IQuery<IReadOnlyList<DiaDeAtencion>>, IRequiereAdministrador;

/// <summary>Un día del Calendario de atención. El motivo de la Agenda custom nunca va a registros.</summary>
/// <param name="Fecha">La fecha local.</param>
/// <param name="Tipo">Cómo se distingue el día.</param>
/// <param name="MotivoDeCustom">El motivo de su Agenda custom (INV-A4); nulo si no tiene.</param>
/// <param name="Tramos">Los Tramos vigentes ese día (INV-V1), en orden; vacío si no se atiende.</param>
public sealed record DiaDeAtencion(DateOnly Fecha, TipoDeDia Tipo, string? MotivoDeCustom, IReadOnlyList<TramoDeLaAgenda> Tramos)
{
    /// <summary>Si la fecha tiene Agenda custom.</summary>
    public bool ConCustom => Tipo is TipoDeDia.CustomSinAtencion or TipoDeDia.DiaAbierto or TipoDeDia.HorarioEspecial;

    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"Fecha = {Fecha:yyyy-MM-dd}, Tipo = {Tipo}, MotivoDeCustom = {(MotivoDeCustom is null ? "ninguno" : "oculto")}, "
            + $"Tramos = {Tramos.Count}");
        return true;
    }
}
