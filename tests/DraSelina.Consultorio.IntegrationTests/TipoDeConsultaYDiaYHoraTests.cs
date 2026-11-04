using System.Net;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Infrastructure;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Los pasos 1 y 2 de la reserva y el Aviso de privacidad en SSR estático, por HTTP contra la web real y una base
/// migrada propia, con el reloj en el lunes 2 de noviembre de 2026 al mediodía de Ciudad de México
/// (<see cref="ReservaEnLineaTests.Ahora"/>). Una prueba por fila de la matriz del plan.
/// </summary>
public sealed partial class TipoDeConsultaYDiaYHoraTests(Arnes arnes)
{
    private static readonly string Primera = TiposDeVisitaSembrados.PrimeraConsulta.ToString();

    private static readonly string Seguimiento = TiposDeVisitaSembrados.ConsultaDeSeguimiento.ToString();

    private static readonly string[] TardeCompleta = ["3:30 pm", "4:00 pm", "4:30 pm", "5:00 pm", "5:30 pm"];

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Agendar_una_cita_de_Inicio_lleva_a_Tipo_de_consulta_con_Primera_consulta_elegida()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var inicio = await PedirAsync(cliente, "/");
        var destino = Assert.Single(EnlaceAgendar().Matches(inicio)).Groups["href"].Value;
        Assert.Equal("/reservar", destino);

        var html = await PedirAsync(cliente, destino);

        Assert.Contains("¿Qué tipo de consulta necesitas?", html, StringComparison.Ordinal);
        Assert.Contains("Paso 1 de 3", html, StringComparison.Ordinal);
        Assert.Matches(FormularioGetAHorario(), html);

        // Los dos tipos sembrados, en su orden, con su descripción y su duración, y sin precios (FR-1).
        var radios = Radios().Matches(html).ToList();
        Assert.Equal([Primera, Seguimiento], radios.Select(r => r.Groups["valor"].Value));
        Assert.Contains("checked", radios[0].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("checked", radios[1].Value, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("Primera consulta", StringComparison.Ordinal) < html.IndexOf("Consulta de seguimiento", StringComparison.Ordinal));
        Assert.Contains("Si es tu primera vez con la doctora.", html, StringComparison.Ordinal);
        Assert.Contains("Si ya te atendió y vienes a revisión.", html, StringComparison.Ordinal);
        Assert.Contains("30 min", html, StringComparison.Ordinal);
        Assert.DoesNotContain("$", html, StringComparison.Ordinal);
        Assert.Contains("Ver horarios disponibles", html, StringComparison.Ordinal);

        // Sin el horario escrito del encabezado del mockup: lo resuelve la épica del sitio.
        Assert.DoesNotContain("3:30–6:00", html, StringComparison.Ordinal);
        SinScripts(html);
    }

    [Fact]
    public async Task Tipo_de_consulta_solo_lista_los_tipos_activos_y_reservables_en_linea()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync($"update agenda.tipo_de_visita set activo = false where id = '{Primera}'");
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, "/reservar");

        var radio = Assert.Single(Radios().Matches(html));
        Assert.Equal(Seguimiento, radio.Groups["valor"].Value);
        Assert.Contains("checked", radio.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Regresar_desde_Dia_y_hora_conserva_el_tipo_elegido()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var horario = await PedirAsync(cliente, $"/reservar/horario?tipo={Seguimiento}");
        var regreso = Assert.Single(EnlaceRegresar().Matches(horario)).Groups["href"].Value;
        Assert.Equal($"/reservar?tipo={Seguimiento}", regreso);

        var html = await PedirAsync(cliente, regreso);

        var radios = Radios().Matches(html).ToList();
        Assert.DoesNotContain("checked", radios[0].Value, StringComparison.Ordinal);
        Assert.Contains("checked", radios[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Elegir_Consulta_de_seguimiento_y_ver_horarios_lleva_a_Dia_y_hora_de_ese_tipo()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        // El navegador envía el formulario GET con el radio elegido: la dirección solo lleva tipo.
        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Seguimiento}");

        Assert.Contains("Consulta de seguimiento · 30 min", html, StringComparison.Ordinal);
        Assert.Contains("Elige día y hora", html, StringComparison.Ordinal);
        Assert.Contains("Paso 2 de 3", html, StringComparison.Ordinal);
        Assert.All(Dias(html), d => Assert.Contains($"tipo={Seguimiento}&", d.Href, StringComparison.Ordinal));
        SinScripts(html);
    }

    [Fact]
    public async Task Con_la_base_sembrada_salen_los_dias_habiles_de_la_ventana_y_al_elegir_uno_sus_horas()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");

        // De mañana (martes 3 de noviembre) al último día hábil de la ventana de 2 meses (viernes 1 de enero).
        var dias = Dias(html);
        var esperados = DiasHabiles(new DateOnly(2026, 11, 3), new DateOnly(2027, 1, 1));
        Assert.Equal(esperados.Select(f => $"fecha={f:yyyy-MM-dd}"), dias.Select(d => d.Href[d.Href.IndexOf("fecha=", StringComparison.Ordinal)..]));
        Assert.All(dias, d => Assert.Contains("5 libres", d.Html, StringComparison.Ordinal));
        Assert.Contains("aria-label=\"Martes 3 de noviembre, 5 horarios libres\"", html, StringComparison.Ordinal);
        Assert.Contains("Noviembre 2026", html, StringComparison.Ordinal);
        Assert.Contains("Hasta el 1 de enero", html, StringComparison.Ordinal);

        // Sin fecha en la dirección, el primer día con horas viene elegido; todavía sin hora.
        Assert.Contains("aria-current=\"true\"", dias[0].Html, StringComparison.Ordinal);
        Assert.Equal(TardeCompleta, Horas(html).Select(h => h.Texto));
        Assert.Contains("Elige una hora", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/reservar/datos", html, StringComparison.Ordinal);
        Assert.Contains("Solo ves horarios libres. Hora del centro de México.", html, StringComparison.Ordinal);

        // Elegir el jueves es un enlace: recarga con su fecha y muestra sus horas.
        var jueves = Assert.Single(dias, d => d.Href.EndsWith("fecha=2026-11-05", StringComparison.Ordinal));
        var delJueves = await PedirAsync(cliente, jueves.Href);
        Assert.Contains("Jueves 5 de noviembre", delJueves, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"true\"", Assert.Single(Dias(delJueves), d => d.Href == jueves.Href).Html, StringComparison.Ordinal);
        var horas = Horas(delJueves);
        Assert.Equal(TardeCompleta, horas.Select(h => h.Texto));
        Assert.Equal($"/reservar/horario?tipo={Primera}&fecha=2026-11-05&hora=15:30", horas[0].Href);
    }

    [Fact]
    public async Task Cada_chip_lleva_su_id_y_el_mes_corto_va_en_el_primero_de_la_fila_y_de_cada_mes()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");
        var dias = Dias(html);

        // El enlace lleva el id de su chip como fragmento (lo exige el patrón de Dias) y el chip tiene ese id.
        Assert.All(dias, d => Assert.Contains(
            $"id=\"dia-{d.Href[(d.Href.IndexOf("fecha=", StringComparison.Ordinal) + 6)..]}\"", d.Html, StringComparison.Ordinal));

        Assert.Contains("<span class=\"dsm-dia__mes\">nov</span>", dias[0].Html, StringComparison.Ordinal);
        Assert.DoesNotContain("dsm-dia__mes", dias[1].Html, StringComparison.Ordinal);
        var primeroDeDiciembre = Assert.Single(dias, d => d.Href.EndsWith("fecha=2026-12-01", StringComparison.Ordinal));
        Assert.Contains("<span class=\"dsm-dia__mes\">dic</span>", primeroDeDiciembre.Html, StringComparison.Ordinal);
        var dosDeDiciembre = Assert.Single(dias, d => d.Href.EndsWith("fecha=2026-12-02", StringComparison.Ordinal));
        Assert.DoesNotContain("dsm-dia__mes", dosDeDiciembre.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_dia_con_todas_sus_horas_ocupadas_se_ve_Lleno_y_no_se_puede_elegir()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var martes = new DateOnly(2026, 11, 3);
        // Cada hora con otro teléfono, para no topar con el tope semanal (RN-18).
        TimeOnly[] horas = [new(15, 30), new(16, 0), new(16, 30), new(17, 0), new(17, 30)];
        for (var i = 0; i < horas.Length; i++)
        {
            var comando = ReservaEnLineaTests.Comando(martes, horas[i]);
            comando.Telefono = $"444 555 {1000 + i}";
            Assert.True((await escenario.AgendarAsync(comando)).EsExito);
        }

        using var cliente = escenario.Web.CreateClient();

        // Aunque la dirección pida el martes, no se elige: viene elegido el miércoles, el primero con horas.
        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-03&hora=16:00");

        var lleno = Assert.Single(DiasLlenos().Matches(html));
        Assert.Contains("Lleno", lleno.Value, StringComparison.Ordinal);
        // Lo que se anuncia es lo que se ve: la fecha completa, oculta a la vista, y "Lleno".
        Assert.Contains("<span class=\"visually-hidden\">Martes 3 de noviembre, </span>", lleno.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label", lleno.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-disabled", lleno.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(Dias(html), d => d.Href.Contains("fecha=2026-11-03", StringComparison.Ordinal));
        Assert.Contains("aria-current=\"true\"", Dias(html)[0].Html, StringComparison.Ordinal);
        Assert.Contains("Miércoles 4 de noviembre", html, StringComparison.Ordinal);
        Assert.Contains("Elige una hora", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_dia_sin_ningun_horario_posible_para_el_tipo_no_aparece()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync($"update agenda.tipo_de_visita set duracion_min = 45 where id = '{Seguimiento}'");
        await escenario.InsertarCustomAsync(new DateOnly(2026, 11, 4), "tarde corta", (new TimeOnly(15, 30), new TimeOnly(16, 0)));
        using var cliente = escenario.Web.CreateClient();

        var seguimiento = await PedirAsync(cliente, $"/reservar/horario?tipo={Seguimiento}");
        var primera = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");

        Assert.DoesNotContain("fecha=2026-11-04", seguimiento, StringComparison.Ordinal);
        Assert.DoesNotContain(Dias(seguimiento), d => d.Href.Contains("2026-11-04", StringComparison.Ordinal));
        Assert.Empty(DiasLlenos().Matches(seguimiento));

        // En el calendario de escritorio sí está su celda, deshabilitada como cualquier día sin horarios.
        Assert.Contains("<span class=\"visually-hidden\">Miércoles 4 de noviembre, no disponible</span>", seguimiento, StringComparison.Ordinal);
        Assert.DoesNotContain(CeldasConHoras(seguimiento), c => c.Fecha == "2026-11-04");
        Assert.Contains("Consulta de seguimiento · 45 min", seguimiento, StringComparison.Ordinal);

        // A Primera consulta, de 30 min, sí le cabe un horario ese día.
        var miercoles = Assert.Single(Dias(primera), d => d.Href.EndsWith("fecha=2026-11-04", StringComparison.Ordinal));
        Assert.Contains("1 libre<", miercoles.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_horarios_en_toda_la_ventana_sale_un_solo_mensaje_con_el_WhatsApp()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync("delete from agenda.agenda_base_tramo");
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");

        Assert.Contains("Por ahora no hay horarios libres para agendar en línea.", html, StringComparison.Ordinal);

        // El WhatsApp todavía no se conoce: el mensaje lo nombra sin número (Gerardo, 2026-11-02).
        Assert.Contains("Escríbenos por WhatsApp y te ayudamos.", html, StringComparison.Ordinal);
        Assert.Empty(Dias(html));
        Assert.Empty(Horas(html));
        Assert.DoesNotContain("Continuar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Elegir_una_hora_muestra_el_resumen_y_Continuar_lleva_a_Tus_datos_con_tipo_fecha_y_hora()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-05&hora=16:30");

        Assert.Contains("Jue 5 nov · 4:30 pm", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Elige una hora", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"true\"", Assert.Single(Horas(html), h => h.Texto == "4:30 pm").Html, StringComparison.Ordinal);

        var continuar = Assert.Single(EnlaceContinuar().Matches(html)).Groups["href"].Value;
        Assert.Equal($"/reservar/datos?tipo={Primera}&fecha=2026-11-05&hora=16:30", continuar);

        var datos = await PedirAsync(cliente, continuar);
        Assert.Contains("Tus datos", datos, StringComparison.Ordinal);
        Assert.Contains("Jue 5 nov · 4:30 pm", datos, StringComparison.Ordinal);
        Assert.Contains("<form", datos, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("16:45")]
    [InlineData("4pm")]
    public async Task Una_hora_que_no_es_libre_ese_dia_no_queda_elegida(string hora)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-05&hora={hora}");

        Assert.Contains("Jueves 5 de noviembre", html, StringComparison.Ordinal);
        Assert.Contains("Elige una hora", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/reservar/datos", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/reservar/horario")]
    [InlineData("/reservar/horario?tipo=no-es-un-id")]
    [InlineData("/reservar/horario?tipo=01a19e61-0000-7000-8000-000000000000")]
    public async Task Un_tipo_invalido_en_la_direccion_muestra_un_mensaje_y_el_enlace_a_Tipo_de_consulta(string ruta)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        await AfirmarTipoInvalidoAsync(cliente, ruta);
    }

    [Fact]
    public async Task Un_tipo_que_no_se_agenda_en_linea_muestra_un_mensaje_y_el_enlace_a_Tipo_de_consulta()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync($"update agenda.tipo_de_visita set reservable_en_linea = false where id = '{Seguimiento}'");
        using var cliente = escenario.Web.CreateClient();

        await AfirmarTipoInvalidoAsync(cliente, $"/reservar/horario?tipo={Seguimiento}");
    }

    [Fact]
    public async Task El_Aviso_de_privacidad_se_abre_desde_la_casilla_y_desde_el_pie_con_su_version()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var datos = await PedirAsync(cliente, $"/reservar/datos?tipo={Primera}&fecha=2026-11-05&hora=16:30");
        var casilla = Assert.Single(Casilla().Matches(datos)).Value;
        Assert.Equal("/aviso-de-privacidad", Assert.Single(EnlaceAlAviso().Matches(casilla)).Groups["href"].Value);

        var inicio = await PedirAsync(cliente, "/");
        var pie = Assert.Single(Pie().Matches(inicio)).Value;
        Assert.Equal("/aviso-de-privacidad", Assert.Single(EnlaceAlAviso().Matches(pie)).Groups["href"].Value);

        var aviso = await PedirAsync(cliente, "/aviso-de-privacidad");
        Assert.Matches(H1Aviso(), aviso);
        Assert.Contains("está en revisión", aviso, StringComparison.Ordinal);
        Assert.Contains("Dra. Selina Medina Castillo", aviso, StringComparison.Ordinal);
        Assert.Contains("agendar y gestionar tu cita", aviso, StringComparison.Ordinal);
        Assert.Contains("Mariano Arista No. 735, Col. Barrio de Tequisquiapan", aviso, StringComparison.Ordinal);
        Assert.Contains("Versión 2026-11-borrador", aviso, StringComparison.Ordinal);
        SinScripts(aviso);
    }

    [Fact]
    public async Task En_escritorio_el_calendario_es_una_tabla_de_lunes_a_domingo_con_los_dias_habiles_de_la_ventana()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");
        var tabla = Assert.Single(Calendario().Matches(html)).Value;

        // Siete columnas siempre, con su encabezado (Gerardo, 2026-11-04).
        Assert.Equal(
            ["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"],
            EncabezadoDeDia().Matches(tabla).Select(m => m.Groups["corto"].Value));
        Assert.Contains("<caption class=\"visually-hidden\">Noviembre 2026</caption>", tabla, StringComparison.Ordinal);

        // Noviembre empieza en domingo: seis semanas, siete celdas cada una.
        var semanas = Semanas(tabla);
        Assert.Equal(6, semanas.Count);
        Assert.All(semanas, s => Assert.Equal(7, s.Count));
        Assert.Contains("Domingo 1 de noviembre, no disponible", semanas[0][6], StringComparison.Ordinal);

        // Los días hábiles de noviembre desde mañana son enlaces, con su fecha completa y sus horarios libres.
        var conHoras = CeldasConHoras(tabla);
        Assert.Equal(
            DiasHabiles(new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 30)).Select(f => f.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            conHoras.Select(c => c.Fecha));
        Assert.All(conHoras, c => Assert.Equal($"/reservar/horario?tipo={Primera}&fecha={c.Fecha}", c.Href));
        Assert.Contains("aria-label=\"Martes 3 de noviembre, 5 horarios libres\"", conHoras[0].Html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"true\"", conHoras[0].Html, StringComparison.Ordinal);

        // Hoy, sábados y domingos: deshabilitados, sin enlace, con su fecha completa para lectores de pantalla.
        Assert.Contains("Lunes 2 de noviembre, no disponible", tabla, StringComparison.Ordinal);
        Assert.Contains("Sábado 7 de noviembre, no disponible", semanas[1][5], StringComparison.Ordinal);
        Assert.Contains("Domingo 8 de noviembre, no disponible", semanas[1][6], StringComparison.Ordinal);
        Assert.Contains("Con horarios libres", html, StringComparison.Ordinal);
        Assert.Contains("Lleno o no disponible", html, StringComparison.Ordinal);

        // La nota sale de las reglas; "Hasta el" del último día de HorariosLibres.
        Assert.Contains("Hora del centro de México · desde mañana y hasta 2 meses", html, StringComparison.Ordinal);
        Assert.Contains("Hasta el 1 de enero", html, StringComparison.Ordinal);

        // Mes siguiente: un enlace al primer día de diciembre con horas; no hay mes anterior en la ventana.
        Assert.Equal($"/reservar/horario?tipo={Primera}&fecha=2026-12-01", Assert.Single(MesSiguiente().Matches(html)).Groups["href"].Value);
        Assert.Empty(MesAnterior().Matches(html));
        SinScripts(html);
    }

    [Fact]
    public async Task En_escritorio_el_mes_siguiente_muestra_diciembre_con_su_mes_anterior()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-12-01");
        var tabla = Assert.Single(Calendario().Matches(html)).Value;

        Assert.Contains("<caption class=\"visually-hidden\">Diciembre 2026</caption>", tabla, StringComparison.Ordinal);
        Assert.Equal("2026-12-01", CeldasConHoras(tabla)[0].Fecha);
        Assert.Equal($"/reservar/horario?tipo={Primera}&fecha=2026-11-03", Assert.Single(MesAnterior().Matches(html)).Groups["href"].Value);
        Assert.Equal($"/reservar/horario?tipo={Primera}&fecha=2027-01-01", Assert.Single(MesSiguiente().Matches(html)).Groups["href"].Value);
    }

    [Fact]
    public async Task En_escritorio_un_sabado_abierto_aparece_habilitado_en_su_columna()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(new DateOnly(2026, 11, 7), "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(11, 0)));
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");
        var semanas = Semanas(Assert.Single(Calendario().Matches(html)).Value);

        var sabado = Assert.Single(CeldasConHoras(semanas[1][5]));
        Assert.Equal("2026-11-07", sabado.Fecha);
        Assert.Contains("aria-label=\"Sábado 7 de noviembre, 2 horarios libres\"", sabado.Html, StringComparison.Ordinal);

        // El siguiente sigue cerrado.
        Assert.Contains("Sábado 14 de noviembre, no disponible", semanas[2][5], StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_escritorio_un_dia_lleno_se_ve_Lleno_y_si_la_direccion_lo_pide_dice_que_no_tiene_horarios()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        TimeOnly[] horas = [new(15, 30), new(16, 0), new(16, 30), new(17, 0), new(17, 30)];
        for (var i = 0; i < horas.Length; i++)
        {
            var comando = ReservaEnLineaTests.Comando(new DateOnly(2026, 11, 3), horas[i]);
            comando.Telefono = $"444 555 {1000 + i}";
            Assert.True((await escenario.AgendarAsync(comando)).EsExito);
        }

        using var cliente = escenario.Web.CreateClient();

        // Sin fecha: el martes se ve "Lleno", no se puede elegir y el elegido es el miércoles.
        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}");
        var tabla = Assert.Single(Calendario().Matches(html)).Value;
        var lleno = Assert.Single(CeldasLlenas().Matches(tabla)).Value;
        Assert.Contains("<span class=\"visually-hidden\">Martes 3 de noviembre, </span>", lleno, StringComparison.Ordinal);
        Assert.Contains("Lleno", lleno, StringComparison.Ordinal);
        Assert.DoesNotContain(CeldasConHoras(tabla), c => c.Fecha == "2026-11-03");
        Assert.Contains("aria-current=\"true\"", Assert.Single(CeldasConHoras(tabla), c => c.Fecha == "2026-11-04").Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Este día ya no tiene horarios libres.", html, StringComparison.Ordinal);

        // Con la fecha del martes en la dirección: el escritorio lo dice y no hay hora que continuar.
        var delMartes = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-03&hora=16:00");
        Assert.Contains("Este día ya no tiene horarios libres. Elige otro día en el calendario.", delMartes, StringComparison.Ordinal);
        var martes = Assert.Single(CeldasLlenas().Matches(delMartes)).Value;
        Assert.Contains("dsm-calendario__dia--elegido", martes, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"true\"", martes, StringComparison.Ordinal);
        Assert.DoesNotContain("/reservar/datos", delMartes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_escritorio_cambiar_el_tipo_conserva_el_dia_elegido()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-05&hora=16:30");

        // Los tipos reservables, en su orden; el actual marcado; la fecha se conserva, la hora no.
        var tipos = Tipos().Matches(html).ToList();
        Assert.Equal(
            [$"/reservar/horario?tipo={Primera}&fecha=2026-11-05", $"/reservar/horario?tipo={Seguimiento}&fecha=2026-11-05"],
            tipos.Select(t => t.Groups["href"].Value));
        Assert.Contains("aria-current=\"true\"", tipos[0].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current", tipos[1].Value, StringComparison.Ordinal);

        var seguimiento = await PedirAsync(cliente, tipos[1].Groups["href"].Value);

        Assert.Contains("Consulta de seguimiento · 30 min", seguimiento, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"true\"", Assert.Single(CeldasConHoras(seguimiento), c => c.Fecha == "2026-11-05").Html, StringComparison.Ordinal);
        var horas = Horas(seguimiento);
        Assert.Equal(TardeCompleta, horas.Select(h => h.Texto));
        Assert.All(horas, h => Assert.StartsWith($"/reservar/horario?tipo={Seguimiento}&fecha=2026-11-05&hora=", h.Href, StringComparison.Ordinal));
        Assert.Contains("Elige una hora", seguimiento, StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_escritorio_cambiar_a_un_tipo_sin_horarios_ese_dia_no_elige_otro_dia()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync($"update agenda.tipo_de_visita set duracion_min = 45 where id = '{Seguimiento}'");
        await escenario.InsertarCustomAsync(new DateOnly(2026, 11, 4), "tarde corta", (new TimeOnly(15, 30), new TimeOnly(16, 0)));
        using var cliente = escenario.Web.CreateClient();

        // En Primera consulta el miércoles 4 tiene horario; el enlace a Seguimiento conserva esa fecha.
        var primera = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-04");
        var aSeguimiento = Assert.Single(Tipos().Matches(primera), t => t.Groups["href"].Value.Contains(Seguimiento, StringComparison.Ordinal)).Groups["href"].Value;
        Assert.Equal($"/reservar/horario?tipo={Seguimiento}&fecha=2026-11-04", aSeguimiento);

        var html = await PedirAsync(cliente, aSeguimiento);

        Assert.Contains("Este día no tiene horarios para este tipo de consulta. Elige otro día en el calendario.", html, StringComparison.Ordinal);
        var tabla = Assert.Single(Calendario().Matches(html)).Value;
        Assert.Contains("<caption class=\"visually-hidden\">Noviembre 2026</caption>", tabla, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current", tabla, StringComparison.Ordinal);
        Assert.Contains("Miércoles 4 de noviembre, no disponible", tabla, StringComparison.Ordinal);
        Assert.Contains("<dd>Elige una hora</dd>", Assert.Single(TuCita().Matches(html)).Value, StringComparison.Ordinal);
        Assert.DoesNotContain("/reservar/datos", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_escritorio_el_panel_Tu_cita_muestra_consulta_fecha_y_hora_y_lugar_con_Continuar()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var sinHora = Assert.Single(TuCita().Matches(await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-05"))).Value;
        Assert.Contains("Elige una hora", sinHora, StringComparison.Ordinal);
        Assert.Empty(EnlaceContinuar().Matches(sinHora));

        var html = await PedirAsync(cliente, $"/reservar/horario?tipo={Primera}&fecha=2026-11-05&hora=16:30");
        var panel = Assert.Single(TuCita().Matches(html)).Value;

        Assert.Contains(">Tu cita</h2>", panel, StringComparison.Ordinal);
        Assert.Contains("<dd>Primera consulta · 30 min</dd>", panel, StringComparison.Ordinal);
        Assert.Contains("<dd>Jueves 5 de noviembre, 4:30 pm</dd>", panel, StringComparison.Ordinal);
        Assert.Contains("<dd>Hospital Star Médica, consultorio 101</dd>", panel, StringComparison.Ordinal);

        // Un solo "Continuar", el del panel: el mismo en los dos anchos.
        Assert.Equal(
            $"/reservar/datos?tipo={Primera}&fecha=2026-11-05&hora=16:30",
            Assert.Single(EnlaceContinuar().Matches(panel)).Groups["href"].Value);
        Assert.Single(EnlaceContinuar().Matches(html));
    }

    private static async Task AfirmarTipoInvalidoAsync(HttpClient cliente, string ruta)
    {
        var html = await PedirAsync(cliente, ruta);

        Assert.Contains("No encontramos ese tipo de consulta.", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/reservar\"", html, StringComparison.Ordinal);
        Assert.Empty(Dias(html));
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.DoesNotContain("agenda.", html, StringComparison.Ordinal);
    }

    /// <summary>Pide la página, afirma 200 y devuelve el HTML decodificado.</summary>
    private static async Task<string> PedirAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"'{ruta}' respondió {(int)respuesta.StatusCode}.");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    private static void SinScripts(string html)
    {
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blazor.web.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("_framework/", html, StringComparison.Ordinal);
    }

    private static List<DateOnly> DiasHabiles(DateOnly desde, DateOnly hasta)
    {
        var dias = new List<DateOnly>();
        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            if (fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                dias.Add(fecha);
            }
        }

        return dias;
    }

    private static List<(string Href, string Html)> Dias(string html) =>
        [.. EnlaceDeDia().Matches(html).Select(m => (m.Groups["href"].Value, m.Value))];

    private static List<(string Href, string Texto, string Html)> Horas(string html) =>
        [.. EnlaceDeHora().Matches(html).Select(m => (m.Groups["href"].Value, m.Groups["texto"].Value.Trim(), m.Value))];

    private static List<(string Href, string Fecha, string Html)> CeldasConHoras(string html) =>
        [.. CeldaConHoras().Matches(html).Select(m => (m.Groups["href"].Value, m.Groups["fecha"].Value, m.Value))];

    // Las filas del cuerpo del calendario, cada una con el HTML de sus siete celdas.
    private static List<List<string>> Semanas(string tabla) =>
        [.. Fila().Matches(tabla[tabla.IndexOf("<tbody>", StringComparison.Ordinal)..])
            .Select(f => Celda().Matches(f.Value).Select(c => c.Groups["celda"].Value).ToList())];

    [GeneratedRegex("""<table class="dsm-calendario__tabla">.*?</table>""", RegexOptions.Singleline)]
    private static partial Regex Calendario();

    [GeneratedRegex("""<th scope="col"><abbr title="[^"]+">(?<corto>[^<]+)</abbr></th>""")]
    private static partial Regex EncabezadoDeDia();

    [GeneratedRegex("""<tr>.*?</tr>""", RegexOptions.Singleline)]
    private static partial Regex Fila();

    [GeneratedRegex("""<td>(?<celda>.*?)</td>""", RegexOptions.Singleline)]
    private static partial Regex Celda();

    [GeneratedRegex("""<a href="(?<href>/reservar/horario\?tipo=[^"&]+&fecha=(?<fecha>[0-9-]+))" class="dsm-calendario__dia"[^>]*>.*?</a>""", RegexOptions.Singleline)]
    private static partial Regex CeldaConHoras();

    [GeneratedRegex("""<span class="dsm-calendario__dia dsm-calendario__dia--lleno[^"]*"[^>]*>.*?Lleno</span>""", RegexOptions.Singleline)]
    private static partial Regex CeldasLlenas();

    [GeneratedRegex("""<a href="(?<href>[^"]+)" class="dsm-calendario__vecino" aria-label="Mes anterior[^"]*">""")]
    private static partial Regex MesAnterior();

    [GeneratedRegex("""<a href="(?<href>[^"]+)" class="dsm-calendario__vecino" aria-label="Mes siguiente[^"]*">""")]
    private static partial Regex MesSiguiente();

    [GeneratedRegex("""<a href="(?<href>[^"]+)" class="dsm-tipo"[^>]*>""")]
    private static partial Regex Tipos();

    [GeneratedRegex("""<div class="dsm-tu-cita[^"]*">.*?Sin cuenta\. Tu cita queda confirmada al instante\.</p>""", RegexOptions.Singleline)]
    private static partial Regex TuCita();

    [GeneratedRegex("""<a(?=[^>]*class="[^"]*\bbtn-primary\b)[^>]*href="(?<href>[^"]+)"[^>]*>\s*Agendar una cita\s*</a>""")]
    private static partial Regex EnlaceAgendar();

    [GeneratedRegex("""<form(?=[^>]*method="get")(?=[^>]*action="/reservar/horario")[^>]*>""")]
    private static partial Regex FormularioGetAHorario();

    [GeneratedRegex("""<input(?=[^>]*type="radio")(?=[^>]*name="tipo")(?=[^>]*value="(?<valor>[^"]+)")[^>]*>""")]
    private static partial Regex Radios();

    [GeneratedRegex("""<a(?=[^>]*aria-label="Regresar")[^>]*href="(?<href>[^"]+)"[^>]*>""")]
    private static partial Regex EnlaceRegresar();

    [GeneratedRegex("""<a[^>]*href="(?<href>/reservar/horario\?tipo=[^"&]+&fecha=(?<fecha>[0-9-]+))#dia-\k<fecha>"[^>]*class="dsm-dia"[^>]*>.*?</a>""", RegexOptions.Singleline)]
    private static partial Regex EnlaceDeDia();

    [GeneratedRegex("""<span[^>]*class="dsm-dia dsm-dia--lleno"[^>]*>.*?Lleno</span>""", RegexOptions.Singleline)]
    private static partial Regex DiasLlenos();

    [GeneratedRegex("""<a[^>]*href="(?<href>/reservar/horario\?[^"]*&hora=[^"]+)"[^>]*class="dsm-hora"[^>]*>(?<texto>[^<]*)</a>""")]
    private static partial Regex EnlaceDeHora();

    [GeneratedRegex("""<a[^>]*href="(?<href>[^"]+)"[^>]*class="[^"]*\bbtn-primary\b[^"]*"[^>]*>\s*Continuar""")]
    private static partial Regex EnlaceContinuar();

    [GeneratedRegex("""<label[^>]*class="dsm-casilla".*?</label>""", RegexOptions.Singleline)]
    private static partial Regex Casilla();

    [GeneratedRegex("""<footer[^>]*>.*?</footer>""", RegexOptions.Singleline)]
    private static partial Regex Pie();

    [GeneratedRegex("""<a[^>]*href="(?<href>[^"]+)"[^>]*>\s*Aviso de privacidad\s*</a>""")]
    private static partial Regex EnlaceAlAviso();

    [GeneratedRegex("""<h1[^>]*>\s*Aviso de privacidad\s*</h1>""")]
    private static partial Regex H1Aviso();
}
