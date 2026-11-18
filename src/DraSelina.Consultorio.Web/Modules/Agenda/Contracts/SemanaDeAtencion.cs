using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La semana de atención habitual para el sitio (CAP-21; entrada 7.1): la semana tipo, de lunes a domingo, cada día
/// con sus Tramos en orden (vacío: no se atiende). Pública, sin sesión, y solo lee. No incluye Agendas custom: el sitio
/// muestra el horario habitual y los cambios de una fecha se ven al reservar. <see cref="SemanaTipo"/> es la misma
/// lectura para Configuración, solo del Administrador.
/// </summary>
public sealed record SemanaDeAtencion : IQuery<IReadOnlyList<DiaDeLaBase>>;
