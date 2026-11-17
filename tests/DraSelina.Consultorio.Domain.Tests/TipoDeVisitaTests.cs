using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary><see cref="TipoDeVisita.Cambiar"/> y <see cref="TipoDeVisita.Desactivar"/> (entrada 6.7).</summary>
public sealed class TipoDeVisitaTests
{
    [Fact]
    public void Cambiar_reemplaza_sus_datos_y_sigue_activo()
    {
        var tipo = TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

        tipo.Cambiar(" Primera consulta larga ", "Valoración completa", 45, reservableEnLinea: false, orden: 3);

        Assert.Equal(("Primera consulta larga", "Valoración completa", 45, false, 3, true),
            (tipo.Nombre, tipo.Descripcion, tipo.DuracionMin, tipo.ReservableEnLinea, tipo.Orden, tipo.Activo));
        Assert.False(tipo.SeAgendaEnLinea);
    }

    [Fact]
    public void Cambiar_rechaza_nombre_vacio_o_duracion_no_positiva()
    {
        var tipo = TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

        Assert.ThrowsAny<ArgumentException>(() => tipo.Cambiar("  ", null, 30, true, 1));
        Assert.ThrowsAny<ArgumentException>(() => tipo.Cambiar("Primera", null, 0, true, 1));
        Assert.Equal(30, tipo.DuracionMin);
    }

    [Fact]
    public void Desactivar_lo_deja_inactivo_una_sola_vez()
    {
        var tipo = TipoDeVisita.Crear("Consulta de seguimiento", 30, reservableEnLinea: true, orden: 2);

        Assert.True(tipo.Desactivar());
        Assert.False(tipo.Desactivar());
        Assert.False(tipo.Activo);
        Assert.False(tipo.SeAgendaEnLinea);
    }
}
