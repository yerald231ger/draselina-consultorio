using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Web;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Reintentos con espera creciente, el tope de intentos y el orden por clave. Cada prueba corre en su base, con una
/// web que solo entrega cuando la prueba da la vuelta y un reloj que solo mueve la prueba: ninguna espera tiempo
/// real.
/// </summary>
public sealed class ReintentosYOrdenTests(Arnes arnes)
{
    private static readonly TimeSpan UnSegundo = TimeSpan.FromSeconds(1);

    [Fact]
    public void El_calendario_de_reintentos_es_el_del_modelo_de_datos()
    {
        Assert.Equal(
            [
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
                TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(12),
            ],
            CalendarioDeReintentos.Esperas);
        Assert.Equal(7, CalendarioDeReintentos.IntentosMaximos);
    }

    [Fact]
    public async Task Un_handler_que_falla_la_primera_vez_recibe_el_evento_otra_vez_un_minuto_despues()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var publicadoEn = escenario.Reloj.UtcNow;
        var eventoId = await escenario.PublicarAsync(id => new EventoDePrueba(id, FallasAntesDeEntregar: 1), claveDeOrden: Guid.CreateVersion7());

        // Primera entrega: el handler falla.
        Assert.Equal(1, await escenario.VueltaAsync());

        var fila = await escenario.EventoAsync(eventoId);
        Assert.Equal("Pendiente", fila.Estado);
        Assert.Equal(1, fila.Intentos);
        Assert.Equal(publicadoEn + TimeSpan.FromMinutes(1), fila.ProximoIntentoEn);
        Assert.Null(fila.EntregadoEn);

        // Antes del minuto no se entrega.
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(59));
        Assert.Equal(0, await escenario.VueltaAsync());
        Assert.Single(await escenario.EntregasAsync(eventoId));

        // Pasado el minuto, sí.
        escenario.Reloj.Avanzar(UnSegundo);
        Assert.Equal(1, await escenario.VueltaAsync());

        fila = await escenario.EventoAsync(eventoId);
        Assert.Equal("Entregado", fila.Estado);
        Assert.Equal(1, fila.Intentos);
        Assert.Equal(publicadoEn + TimeSpan.FromMinutes(1), fila.EntregadoEn);

        var rastro = await escenario.EntregasAsync(eventoId);
        Assert.Equal([true, false], rastro.Select(e => e.Fallo));
    }

    [Fact]
    public async Task Un_handler_que_falla_siempre_recibe_siete_intentos_con_las_esperas_del_calendario_y_el_evento_queda_fallido()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var publicadoEn = escenario.Reloj.UtcNow;
        var eventoId = await escenario.PublicarAsync(
            id => new EventoDePrueba(id, FallasAntesDeEntregar: EventoDePrueba.FallaSiempre), claveDeOrden: Guid.CreateVersion7());

        // El primer intento, y después uno al cumplirse cada espera: ni un segundo antes.
        Assert.Equal(1, await escenario.VueltaAsync());

        TimeSpan[] esperas =
        [
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(12),
        ];
        var esperados = new List<DateTimeOffset> { publicadoEn };

        for (var i = 0; i < esperas.Length; i++)
        {
            var fila = await escenario.EventoAsync(eventoId);
            Assert.Equal("Pendiente", fila.Estado);
            Assert.Equal(i + 1, fila.Intentos);
            Assert.Equal(esperados[^1] + esperas[i], fila.ProximoIntentoEn);

            escenario.Reloj.Avanzar(esperas[i] - UnSegundo);
            Assert.Equal(0, await escenario.VueltaAsync());

            escenario.Reloj.Avanzar(UnSegundo);
            Assert.Equal(1, await escenario.VueltaAsync());
            esperados.Add(escenario.Reloj.UtcNow);
        }

        // Falló el séptimo: queda Fallido, sin entregar.
        var fallido = await escenario.EventoAsync(eventoId);
        Assert.Equal("Fallido", fallido.Estado);
        Assert.Equal(7, fallido.Intentos);
        Assert.Null(fallido.EntregadoEn);

        var rastro = await escenario.EntregasAsync(eventoId);
        Assert.Equal(esperados, rastro.Select(e => e.RecibidoEn));
        Assert.All(rastro, e => Assert.True(e.Fallo));

        // Se registró un error que nombra el evento.
        Assert.Contains(Arnes.Registros(escenario.Web), r =>
            r.Nivel == LogLevel.Error && r.Mensaje.Contains(eventoId.ToString()) && r.Mensaje.Contains("Fallido"));

        // Y no se vuelve a intentar, pase el tiempo que pase.
        escenario.Reloj.Avanzar(TimeSpan.FromDays(30));
        Assert.Equal(0, await escenario.VueltaAsync());
        Assert.Equal(7, (await escenario.EntregasAsync(eventoId)).Count);
        Assert.Equal("Fallido", (await escenario.EventoAsync(eventoId)).Estado);
    }

    [Fact]
    public async Task Un_evento_espera_al_anterior_de_su_clave_y_los_de_otras_claves_siguen()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var clave = Guid.CreateVersion7();
        var otraClave = Guid.CreateVersion7();
        var inicio = escenario.Reloj.UtcNow;

        var primero = await escenario.PublicarAsync(id => new EventoDePrueba(id, FallasAntesDeEntregar: 1), clave, inicio);
        var segundo = await escenario.PublicarAsync(id => new EventoDePrueba(id), clave, inicio + UnSegundo);
        var tercero = await escenario.PublicarAsync(id => new EventoDePrueba(id), otraClave, inicio + 2 * UnSegundo);
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(10));

        // El primero falla: el segundo, de su misma clave, espera; el tercero, de otra clave, llega.
        Assert.Equal(2, await escenario.VueltaAsync());

        Assert.Equal("Pendiente", (await escenario.EventoAsync(primero)).Estado);
        Assert.Equal("Pendiente", (await escenario.EventoAsync(segundo)).Estado);
        Assert.Empty(await escenario.EntregasAsync(segundo));
        Assert.Equal("Entregado", (await escenario.EventoAsync(tercero)).Estado);

        // Otra vuelta antes de que el primero pueda reintentarse: el segundo sigue esperando.
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(30));
        Assert.Equal(0, await escenario.VueltaAsync());
        Assert.Empty(await escenario.EntregasAsync(segundo));

        // Pasado el minuto llegan el primero y el segundo, en ese orden.
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(30));
        Assert.Equal(2, await escenario.VueltaAsync());

        Assert.Equal("Entregado", (await escenario.EventoAsync(primero)).Estado);
        Assert.Equal("Entregado", (await escenario.EventoAsync(segundo)).Estado);

        var llegadaDelPrimero = (await escenario.EntregasAsync(primero)).Single(e => !e.Fallo).Id;
        var llegadaDelSegundo = Assert.Single(await escenario.EntregasAsync(segundo)).Id;
        Assert.True(llegadaDelPrimero < llegadaDelSegundo, "El segundo evento de la clave llegó antes que el primero.");
    }

    [Fact]
    public async Task Un_evento_fallido_no_detiene_a_los_siguientes_de_su_clave()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var clave = Guid.CreateVersion7();
        var inicio = escenario.Reloj.UtcNow;

        var primero = await escenario.PublicarAsync(id => new EventoDePrueba(id, FallasAntesDeEntregar: EventoDePrueba.FallaSiempre), clave, inicio);
        var segundo = await escenario.PublicarAsync(id => new EventoDePrueba(id), clave, inicio + UnSegundo);
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(10));

        // Seis intentos fallidos del primero: mientras siga Pendiente, el segundo espera.
        for (var intento = 1; intento < CalendarioDeReintentos.IntentosMaximos; intento++)
        {
            Assert.Equal(1, await escenario.VueltaAsync());
            Assert.Equal("Pendiente", (await escenario.EventoAsync(primero)).Estado);
            Assert.Empty(await escenario.EntregasAsync(segundo));

            escenario.Reloj.Avanzar(CalendarioDeReintentos.Esperas[intento - 1]);
        }

        // La vuelta del séptimo intento deja Fallido al primero y, ya sin nada pendiente delante, entrega el segundo.
        Assert.Equal(2, await escenario.VueltaAsync());

        Assert.Equal("Fallido", (await escenario.EventoAsync(primero)).Estado);
        Assert.Equal("Entregado", (await escenario.EventoAsync(segundo)).Estado);
        Assert.Single(await escenario.EntregasAsync(segundo));
    }
}
