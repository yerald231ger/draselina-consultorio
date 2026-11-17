using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La semana tipo guardada (CAP-10; entrada 6.5): los siete días, de lunes a domingo, cada uno con sus Tramos en orden
/// (vacío: no se atiende). Para llenar la sección de Configuración. Solo el Administrador (AD-22). Solo lee.
/// </summary>
public sealed record SemanaTipo : IQuery<IReadOnlyList<DiaDeLaBase>>, IRequiereAdministrador;
