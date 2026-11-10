using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Si una Cita del consultorio de ese tipo, en esa fecha y hora locales (AD-17), caería fuera de los Tramos vigentes de
/// su fecha (INV-C6), con la misma regla con la que <see cref="AgendarCitaPorConsultorio"/> pide la nota. Es para avisar
/// mientras se captura (entrada 5.4): solo informa; quien decide al guardar es el comando. Solo el Administrador
/// (AD-22). Solo lee. Un tipo que no existe vuelve <see cref="CodigosDeAgenda.TipoNoReservable"/>.
/// </summary>
/// <param name="TipoDeVisitaId">El tipo: fija la duración.</param>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local del consultorio.</param>
public sealed record HorarioDeLaCitaDelConsultorio(TipoDeVisitaId TipoDeVisitaId, DateOnly Fecha, TimeOnly Hora)
    : IQuery<HorarioDeLaCita>, IRequiereAdministrador;

/// <summary>Dónde caería la Cita respecto a los Tramos vigentes de su fecha.</summary>
/// <param name="FueraDeHorario">No cabe completa en ningún Tramo vigente (INV-C6): la nota es obligatoria.</param>
/// <param name="DiaSinAtencion">Ese día no tiene Tramos vigentes: no se atiende. Siempre fuera de horario.</param>
public sealed record HorarioDeLaCita(bool FueraDeHorario, bool DiaSinAtencion);
