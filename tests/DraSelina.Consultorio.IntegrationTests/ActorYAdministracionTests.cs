using System.Security.Claims;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Quién actúa y el rechazo de administración sin sesión (AD-22), contra la web real del arnés. El usuario del
/// ámbito se fija como lo fija el renderizador de Blazor: por <see cref="IHostEnvironmentAuthenticationStateProvider"/>.
/// <c>Sistema</c>, como lo declara quien crea el ámbito desde el host: por <see cref="ActorDelHost.DeclararSistema"/>.
/// </summary>
public sealed class ActorYAdministracionTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static ClaimsPrincipal Anonimo => new(new ClaimsIdentity());

    private static ClaimsPrincipal Autenticado =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "administradora"), new Claim(ClaimTypes.NameIdentifier, IdDeLaCuenta)],
            authenticationType: "Prueba"));

    private const string IdDeLaCuenta = "0b6f7c1e-cuenta-de-prueba";

    [Fact]
    public async Task Un_comando_marcado_sin_usuario_vuelve_no_autorizado_y_el_handler_no_corre()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Anonimo);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task Un_comando_marcado_con_usuario_corre_y_su_handler_recibe_Administrador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Administrador, resultado.Valor);
        Assert.Equal(1, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task Una_consulta_marcada_sin_usuario_vuelve_no_autorizado_y_el_handler_no_corre()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Anonimo);

        var resultado = await Consulta(ambito).HandleAsync(new LeerAdministracion(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DeLaConsulta);
    }

    [Fact]
    public async Task Una_consulta_marcada_con_usuario_corre_y_su_handler_recibe_Administrador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);

        var resultado = await Consulta(ambito).HandleAsync(new LeerAdministracion(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Administrador, resultado.Valor);
        Assert.Equal(1, Llamadas(web).DeLaConsulta);
    }

    [Fact]
    public async Task Un_comando_marcado_e_invalido_sin_usuario_vuelve_no_autorizado_y_no_invalido()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Anonimo);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar { Texto = "" }, Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Empty(resultado.Errores);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task Un_comando_marcado_e_invalido_con_usuario_vuelve_invalido()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar { Texto = "" }, Cancelacion);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal([Administrar.MensajeTextoRequerido], resultado.Errores[nameof(Administrar.Texto)]);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task Un_comando_marcado_en_un_ambito_Sistema_vuelve_no_autorizado()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        DeclararSistema(ambito);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task Un_comando_no_marcado_sin_usuario_recibe_Paciente()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Anonimo);

        var resultado = await Handler<PreguntarActor>(ambito).HandleAsync(new PreguntarActor(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Paciente, resultado.Valor);
    }

    [Fact]
    public async Task Un_comando_no_marcado_en_un_ambito_Sistema_recibe_Sistema()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        DeclararSistema(ambito);

        var resultado = await Handler<PreguntarActor>(ambito).HandleAsync(new PreguntarActor(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Sistema, resultado.Valor);
    }

    [Fact]
    public async Task Un_comando_no_marcado_en_un_ambito_sin_estado_de_autenticacion_pasa_sin_pedir_el_actor()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var comando = new Anotar { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}" };

        var resultado = await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<Anotar, Guid>>()
            .HandleAsync(comando, Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(comando.Id, resultado.Valor);
    }

    [Fact]
    public async Task Pedir_el_actor_en_un_ambito_sin_estado_de_autenticacion_ni_Sistema_lanza()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler<PreguntarActor>(ambito).HandleAsync(new PreguntarActor(), Cancelacion));

        Assert.Contains("no tiene actor", error.Message);
    }

    [Fact]
    public async Task Con_usuario_el_id_del_Administrador_es_el_de_su_cuenta()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);

        Assert.Equal(IdDeLaCuenta, await ambito.ServiceProvider.GetRequiredService<IActorActual>().IdDelAdministradorAsync());
    }

    [Fact]
    public async Task Sin_usuario_o_como_Sistema_no_hay_id_del_Administrador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var anonimo = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(anonimo, Anonimo);
        await using var sistema = web.Services.CreateAsyncScope();
        DeclararSistema(sistema);

        Assert.Null(await anonimo.ServiceProvider.GetRequiredService<IActorActual>().IdDelAdministradorAsync());
        Assert.Null(await sistema.ServiceProvider.GetRequiredService<IActorActual>().IdDelAdministradorAsync());
    }

    [Fact]
    public async Task El_actor_es_el_mismo_servicio_por_ambito()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        Assert.Same(
            ambito.ServiceProvider.GetRequiredService<ActorDelHost>(),
            ambito.ServiceProvider.GetRequiredService<IActorActual>());
    }

    // El área (entrada 5.10): en una petición HTTP, Administrador solo bajo /admin; en el circuito, como siempre. Las
    // pruebas por HTTP de la reserva y del Enlace de gestión están en ActorFueraDeLaAdministracionTests.

    [Theory]
    [InlineData("/reservar/datos")]
    [InlineData("/c/token/cancelar")]
    [InlineData("/c/token/calendario.ics")]
    [InlineData("/")]
    [InlineData("/administracion")]
    public async Task Con_usuario_una_peticion_fuera_de_admin_recibe_Paciente_sin_id(string ruta)
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);
        using var _ = EnLaPeticion(web, ambito, ruta);

        var resultado = await Handler<PreguntarActor>(ambito).HandleAsync(new PreguntarActor(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Paciente, resultado.Valor);
        Assert.Null(await ambito.ServiceProvider.GetRequiredService<IActorActual>().IdDelAdministradorAsync());
    }

    [Fact]
    public async Task Con_usuario_un_comando_marcado_en_una_peticion_fuera_de_admin_vuelve_no_autorizado()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);
        using var _ = EnLaPeticion(web, ambito, "/reservar");

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/citas/0b6f7c1e")]
    [InlineData("/ADMIN/pacientes")]
    public async Task Con_usuario_una_peticion_bajo_admin_recibe_Administrador_con_su_id(string ruta)
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Autenticado);
        using var _ = EnLaPeticion(web, ambito, ruta);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Administrador, resultado.Valor);
        Assert.Equal(IdDeLaCuenta, await ambito.ServiceProvider.GetRequiredService<IActorActual>().IdDelAdministradorAsync());
    }

    [Fact]
    public async Task Sin_usuario_un_comando_marcado_en_una_peticion_bajo_admin_vuelve_no_autorizado()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, Anonimo);
        using var _ = EnLaPeticion(web, ambito, "/admin/citas/0b6f7c1e");

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task En_el_circuito_la_peticion_de_su_conexion_no_cuenta_y_con_usuario_es_Administrador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var conexion = web.Services.CreateAsyncScope();
        await using var circuito = web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(circuito, Autenticado);
        // La petición de la conexión del circuito (/_blazor) es de otro ámbito.
        using var _ = EnLaPeticion(web, conexion, "/_blazor");

        var resultado = await Handler<Administrar>(circuito).HandleAsync(new Administrar(), Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(Actor.Administrador, resultado.Valor);
    }

    // Pone una petición HTTP en curso a <paramref name="ruta"/> cuyos servicios son los de <paramref name="ambito"/>, como
    // hace el host; al desecharlo, ya no hay petición.
    private static PeticionEnCurso EnLaPeticion(WebApplicationFactory<Program> web, AsyncServiceScope ambito, string ruta)
    {
        var acceso = web.Services.GetRequiredService<IHttpContextAccessor>();
        var peticion = new DefaultHttpContext { RequestServices = ambito.ServiceProvider };
        peticion.Request.Path = ruta;
        acceso.HttpContext = peticion;
        return new PeticionEnCurso(acceso);
    }

    private sealed class PeticionEnCurso(IHttpContextAccessor acceso) : IDisposable
    {
        public void Dispose() => acceso.HttpContext = null;
    }

    private static void DeclararSistema(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<ActorDelHost>().DeclararSistema();

    private static ICommandHandler<TComando, Actor> Handler<TComando>(AsyncServiceScope ambito)
        where TComando : ICommand<Actor> =>
        ambito.ServiceProvider.GetRequiredService<ICommandHandler<TComando, Actor>>();

    private static IQueryHandler<LeerAdministracion, Actor> Consulta(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<IQueryHandler<LeerAdministracion, Actor>>();

    private static LlamadasDeAdministracion Llamadas(WebApplicationFactory<Program> web) =>
        web.Services.GetRequiredService<LlamadasDeAdministracion>();
}
