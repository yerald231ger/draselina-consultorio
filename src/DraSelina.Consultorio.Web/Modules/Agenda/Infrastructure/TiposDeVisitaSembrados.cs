using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Los tipos de visita iniciales (modelo de datos, Datos iniciales; AD-25): 30 min y reservables en línea. Sus ids son
/// fijos para que la migración los siembre una sola vez y las pruebas los nombren.
/// </summary>
public static class TiposDeVisitaSembrados
{
    public static readonly TipoDeVisitaId PrimeraConsulta = new(Guid.Parse("01a19e61-08c5-7c92-a7e1-3b97e6858869"));

    public static readonly TipoDeVisitaId ConsultaDeSeguimiento = new(Guid.Parse("01a19e61-08d2-771e-8fa6-a54172742655"));

    /// <summary>Las filas para <c>HasData</c>, con las propiedades del agregado.</summary>
    internal static object[] Filas =>
    [
        new
        {
            Id = PrimeraConsulta,
            Nombre = "Primera consulta",
            Descripcion = (string?)null,
            DuracionMin = 30,
            ReservableEnLinea = true,
            Orden = 1,
            Activo = true,
        },
        new
        {
            Id = ConsultaDeSeguimiento,
            Nombre = "Consulta de seguimiento",
            Descripcion = (string?)null,
            DuracionMin = 30,
            ReservableEnLinea = true,
            Orden = 2,
            Activo = true,
        },
    ];
}
