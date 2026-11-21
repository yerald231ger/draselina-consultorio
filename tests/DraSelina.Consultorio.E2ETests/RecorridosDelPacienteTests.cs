using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace DraSelina.Consultorio.E2ETests;

/// <summary>
/// Los caminos felices del Paciente en el teléfono (UJ-1 y UJ-2 de EXPERIENCE.md): agendar con correo, agendar sin
/// correo y cancelar con el Enlace de gestión del correo. Cada prueba agenda su propia Cita con un celular y un correo
/// que no usa otra, así pasa sola o en cualquier orden (y no toca el tope semanal por teléfono).
/// </summary>
public sealed class RecorridosDelPacienteTests(Consultorio consultorio)
{
    // La verificación anti-bots de Tus datos pide 3 segundos entre pintar el formulario y enviarlo
    // (VerificacionAntiBots.TiempoMinimo). La prueba espera como una persona; nada la apaga.
    private static readonly TimeSpan TiempoMinimoAntiBots = TimeSpan.FromSeconds(3.5);

    // El outbox entrega cada 5 segundos (ServicioDeEntregaDeEventos.Intervalo): dos vueltas y algo bastan para saber
    // que un correo no salió.
    private static readonly TimeSpan SinCorreoTras = TimeSpan.FromSeconds(12);

    // Las navegaciones esperan el documento, no el evento load (ver ElegirElPrimerHorarioLibreAsync).
    private static readonly PageGotoOptions SinEsperarElLoad = new() { WaitUntil = WaitUntilState.DOMContentLoaded };

    [Fact]
    public async Task Agendar_con_correo_confirma_y_manda_el_Enlace_de_gestion_por_correo()
    {
        var paciente = Paciente.Nuevo(conCorreo: true);
        await using var celular = await consultorio.NuevoCelularAsync();
        var pagina = await celular.NewPageAsync();

        var enlace = await AgendarAsync(pagina, paciente);

        await Expect(pagina.Locator(".dsm-confirmada__nota")).Not.ToHaveTextAsync("No recibirás recordatorio. Guarda este enlace o agrega la cita a tu calendario.");
        var correo = await consultorio.Mailpit.ConfirmacionParaAsync(paciente.Correo!);
        Assert.Equal(enlace, Mailpit.EnlaceDeGestionEn(correo));
        // La búsqueda por token sí encuentra un correo que existe: así la prueba sin correo no pasa en falso.
        Assert.True(await consultorio.Mailpit.CorreosConAsync(Token(enlace)) > 0);
    }

    [Fact]
    public async Task Agendar_sin_correo_pone_el_Enlace_de_gestion_antes_de_la_tarjeta_y_no_manda_correo()
    {
        var paciente = Paciente.Nuevo(conCorreo: false);
        await using var celular = await consultorio.NuevoCelularAsync();
        var pagina = await celular.NewPageAsync();

        var enlace = await AgendarAsync(pagina, paciente);

        await Expect(pagina.Locator(".dsm-confirmada__nota"))
            .ToHaveTextAsync("No recibirás recordatorio. Guarda este enlace o agrega la cita a tu calendario.");
        var panelAntesDeLaTarjeta = await pagina.EvaluateAsync<bool>(
            """
            () => {
              const panel = document.querySelector('.dsm-enlace-de-gestion');
              const tarjeta = document.querySelector('.dsm-tarjeta');
              return !!panel && !!tarjeta
                && (panel.compareDocumentPosition(tarjeta) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
            }
            """);
        Assert.True(panelAntesDeLaTarjeta, "El panel del Enlace de gestión debe ir antes de la tarjeta de la Cita.");

        await Task.Delay(SinCorreoTras, TestContext.Current.CancellationToken);
        Assert.Equal(0, await consultorio.Mailpit.CorreosConAsync(Token(enlace)));
    }

    [Fact]
    public async Task Cancelar_con_el_Enlace_de_gestion_del_correo_deja_la_cita_cancelada()
    {
        var paciente = Paciente.Nuevo(conCorreo: true);
        await using var celular = await consultorio.NuevoCelularAsync();
        var pagina = await celular.NewPageAsync();
        await AgendarAsync(pagina, paciente);
        var enlace = Mailpit.EnlaceDeGestionEn(await consultorio.Mailpit.ConfirmacionParaAsync(paciente.Correo!));

        await pagina.GotoAsync(enlace, SinEsperarElLoad);
        await pagina.GetByRole(AriaRole.Link, new() { Name = "Cancelar cita" }).ClickAsync();
        await pagina.Locator("button.dsm-boton-cancelar").ClickAsync();

        await Expect(pagina.Locator("#cita-cancelada")).ToBeVisibleAsync();

        await pagina.GotoAsync(enlace, SinEsperarElLoad);
        await Expect(pagina.Locator(".dsm-chip-de-estado--cancelada")).ToHaveTextAsync("Cancelada");
    }

    /// <summary>
    /// UJ-1 en el teléfono: Tipo de consulta (el primero ya viene elegido), el primer horario libre, Tus datos y
    /// "Confirmar cita". Devuelve el Enlace de gestión que muestra la confirmación.
    /// </summary>
    private static async Task<string> AgendarAsync(IPage pagina, Paciente paciente)
    {
        await pagina.GotoAsync("/", SinEsperarElLoad);
        await pagina.GetByRole(AriaRole.Link, new() { Name = "Agendar cita", Exact = true }).First.ClickAsync();
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Ver horarios disponibles" }).ClickAsync();

        var (dia, fecha, hora) = await ElegirElPrimerHorarioLibreAsync(pagina);
        await pagina.GetByRole(AriaRole.Link, new() { Name = "Continuar" }).ClickAsync();

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Tus datos" })).ToBeVisibleAsync();
        var pintado = DateTimeOffset.UtcNow;

        await pagina.GetByLabel("Nombre completo").FillAsync(paciente.Nombre);
        await pagina.GetByLabel("Celular").FillAsync(paciente.Celular);
        if (paciente.Correo is { } correo)
        {
            await pagina.GetByLabel("Correo (opcional)").FillAsync(correo);
        }

        await pagina.GetByLabel("Motivo de la consulta").FillAsync("Dolor de rodilla derecha desde hace dos semanas");
        await pagina.Locator("#Consentimiento").CheckAsync();

        var falta = TiempoMinimoAntiBots - (DateTimeOffset.UtcNow - pintado);
        if (falta > TimeSpan.Zero)
        {
            await Task.Delay(falta, TestContext.Current.CancellationToken);
        }

        await pagina.GetByRole(AriaRole.Button, new() { Name = "Confirmar cita" }).ClickAsync();

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Tu cita está confirmada" })).ToBeVisibleAsync();
        await Expect(pagina.Locator(".dsm-confirmada__dia")).ToHaveTextAsync(dia);
        await Expect(pagina.Locator(".dsm-confirmada__fecha")).ToHaveTextAsync($"{fecha}, {hora}");
        return (await pagina.Locator(".dsm-enlace-de-gestion__direccion").InnerTextAsync()).Trim();
    }

    /// <summary>
    /// El primer horario libre: los del día que la página ya muestra; si ese día no tiene, los de los días siguientes
    /// de la tira. Los horarios empiezan desde mañana, así que la Cita queda a más de 2 horas.
    /// </summary>
    /// <returns>El día de la semana ("Jueves"), el día y mes ("26 de noviembre") y la hora ("4:30 pm") elegidos.</returns>
    private static async Task<(string Dia, string Fecha, string Hora)> ElegirElPrimerHorarioLibreAsync(IPage pagina)
    {
        // Se espera lo que vería una persona, nunca el evento load: con la máquina cargada (por ejemplo, con las
        // pruebas de integración corriendo a la vez) ese evento puede tardar más que el límite de Playwright.
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Elige día y hora" })).ToBeVisibleAsync();

        var horas = pagina.Locator("ul[aria-label='Horas'] a.dsm-hora");
        var dias = pagina.Locator("ul[aria-label='Días'] a.dsm-dia");
        var totalDeDias = await dias.CountAsync();

        for (var i = 0; await horas.CountAsync() == 0; i++)
        {
            Assert.True(i < totalDeDias, "Ningún día de la tira tiene horarios libres.");
            await dias.Nth(i).ClickAsync();
            await Expect(dias.Nth(i)).ToHaveAttributeAsync("aria-current", "true");
        }

        var hora = (await horas.First.InnerTextAsync()).Trim();
        await horas.First.ClickAsync();
        // "Continuar" aparece solo con una hora elegida. El día marcado se llama "Jueves 26 de noviembre, 3 libres".
        await Expect(pagina.GetByRole(AriaRole.Link, new() { Name = "Continuar" })).ToBeVisibleAsync();
        var diaMarcado = (await pagina.Locator("ul[aria-label='Días'] a.dsm-dia[aria-current='true']")
            .GetAttributeAsync("aria-label"))!.Split(',')[0];
        var espacio = diaMarcado.IndexOf(' ');
        return (diaMarcado[..espacio], diaMarcado[(espacio + 1)..], hora);
    }

    private static string Token(string enlace) => enlace[(enlace.LastIndexOf('/') + 1)..];

    /// <summary>Un Paciente que no comparte celular ni correo con otra prueba.</summary>
    private sealed record Paciente(string Nombre, string Celular, string? Correo)
    {
        public static Paciente Nuevo(bool conCorreo)
        {
            // Celular de la Ciudad de México (55 y ocho dígitos), distinto en cada prueba.
            var ocho = Random.Shared.Next(10_000_000, 100_000_000);
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            return new Paciente($"Mariana Prueba {sufijo}", $"55{ocho}", conCorreo ? $"mariana.{sufijo}@ejemplo.com" : null);
        }
    }
}
