namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Una fila de <c>agenda.bitacora</c> (RN-24). La escribe <see cref="BitacoraDeAgenda"/>; solo se inserta, nunca se
/// actualiza ni se borra. Apunta a su entidad por <see cref="Entidad"/> y <see cref="EntidadId"/>, sin llave foránea,
/// porque registra varias tablas.
/// </summary>
public sealed class FilaDeBitacora
{
    public const string Tabla = "bitacora";

    /// <summary>Guid v7: ordenable por tiempo.</summary>
    public Guid Id { get; set; }

    public DateTimeOffset OcurridoEn { get; set; }

    /// <summary><c>Paciente</c>, <c>Administrador</c> o <c>Sistema</c> (AD-22).</summary>
    public string ActorTipo { get; set; } = "";

    /// <summary>El id del usuario administrador; nulo para Paciente y Sistema.</summary>
    public string? ActorId { get; set; }

    public string Entidad { get; set; } = "";

    public string EntidadId { get; set; } = "";

    public string Accion { get; set; } = "";

    /// <summary>JSON. Nunca motivo de consulta ni datos de contacto (AD-23).</summary>
    public string? Detalle { get; set; }
}
