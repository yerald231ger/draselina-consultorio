using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los datos que el Administrador edita de un Tipo de visita en Configuración (CAP-16; entrada 6.7). En pantalla, "Tipo
/// de consulta". Límites de forma `[ASSUMPTION]`: nombre hasta 80 caracteres, descripción hasta 300, duración de 5 a
/// 480 minutos.
/// </summary>
public abstract class DatosDeTipoDeVisita
{
    public const string MensajeDeNombre = "Escribe el nombre del tipo de consulta.";
    public const string MensajeDeDuracion = "La duración va de 5 a 480 minutos.";

    [Required(ErrorMessage = MensajeDeNombre)]
    [StringLength(80, ErrorMessage = "El nombre tiene a lo más 80 caracteres.")]
    public string? Nombre { get; set; }

    /// <summary>Lo que ve el Paciente al elegir; opcional. Vacío o con solo espacios cuenta como no escrito.</summary>
    [StringLength(300, ErrorMessage = "La descripción tiene a lo más 300 caracteres.")]
    public string? Descripcion
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>En minutos.</summary>
    [Required(ErrorMessage = MensajeDeDuracion)]
    [Range(5, 480, ErrorMessage = MensajeDeDuracion)]
    public int? DuracionMin { get; set; }

    /// <summary>Si el Paciente también lo agenda en el sitio.</summary>
    public bool ReservableEnLinea { get; set; }

    /// <summary>Orden en pantalla, de menor a mayor.</summary>
    [Required(ErrorMessage = "Escribe el orden.")]
    [Range(0, short.MaxValue, ErrorMessage = "El orden va de 0 en adelante.")]
    public int? Orden { get; set; }
}

/// <summary>El Administrador crea un Tipo de visita, activo (CAP-16). Solo el Administrador (AD-22).</summary>
public sealed class CrearTipoDeVisita : DatosDeTipoDeVisita, ICommand<TipoDeVisitaGuardado>, IRequiereAdministrador;

/// <summary>
/// El Administrador cambia un Tipo de visita activo (CAP-16): vale desde la siguiente reserva; las Citas ya agendadas no
/// cambian. Uno inexistente o inactivo vuelve <see cref="CodigosDeAgenda.TipoNoEncontrado"/>. Solo el Administrador.
/// </summary>
public sealed class CambiarTipoDeVisita : DatosDeTipoDeVisita, ICommand<TipoDeVisitaGuardado>, IRequiereAdministrador
{
    [Required(ErrorMessage = "Falta el tipo de consulta.")]
    public TipoDeVisitaId? Id { get; set; }
}

/// <summary>
/// El Administrador desactiva un Tipo de visita (Gerardo, 2026-11-17): deja de ofrecerse en el sitio y en Agendar cita;
/// sus Citas siguen. No se reactiva. El último activo no se desactiva: <see cref="CodigosDeAgenda.UltimoTipoActivo"/>.
/// Uno inexistente o ya inactivo vuelve <see cref="CodigosDeAgenda.TipoNoEncontrado"/>. Solo el Administrador.
/// </summary>
public sealed class DesactivarTipoDeVisita : ICommand<TipoDeVisitaGuardado>, IRequiereAdministrador
{
    [Required(ErrorMessage = "Falta el tipo de consulta.")]
    public TipoDeVisitaId? Id { get; set; }
}

/// <summary>El tipo creado, cambiado o desactivado.</summary>
public sealed record TipoDeVisitaGuardado(TipoDeVisitaId Id);

/// <summary>Los Tipos de visita activos con todo lo que se edita, en su orden. Solo el Administrador. Solo lee.</summary>
public sealed record TiposDeVisitaEnConfiguracion : IQuery<IReadOnlyList<TipoDeVisitaEditable>>, IRequiereAdministrador;

/// <summary>Un Tipo de visita activo como se edita en Configuración.</summary>
public sealed record TipoDeVisitaEditable(
    TipoDeVisitaId Id, string Nombre, string? Descripcion, int DuracionMin, bool ReservableEnLinea, int Orden);
