using System.Security.Claims;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Quién actúa y el rechazo de administración sin sesión (AD-22), contra la web real del arnés. El usuario del
/// ámbito se fija como lo fija el renderizador de Blazor: por <see cref="IHostEnvironmentAuthenticationStateProvider"/>.
/// <c>Sistema</c>, como lo declara un trabajo programado: por <see cref="ActorDelHost.DeclararSistema"/>.
/// </summary>
public sealed class ActorYAdministracionTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static ClaimsPrincipal Anonimo => new(new ClaimsIdentity());

    private static ClaimsPrincipal Autenticado =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "administradora")], authenticationType: "Prueba"));

    [Fact]
    public async Task Un_comando_marcado_sin_usuario_vuelve_no_autorizado_y_el_handler_no_corre()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        FijarUsuario(ambito, Anonimo);

        var resultado = await Handler<Administrar>(ambito).HandleAsync(new Administrar(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DelComando);
    }

    [Fact]
    public async Task Un_comando_marcado_con_usuario_corre_y_su_handler_recibe_Administrador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        FijarUsuario(ambito, Autenticado);

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
        FijarUsuario(ambito, Anonimo);

        var resultado = await Consulta(ambito).HandleAsync(new LeerAdministracion(), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(0, Llamadas(web).DeLaConsulta);
    }

    [Fact]
    public async Task Una_consulta_marcada_con_usuario_corre_y_su_handler_recibe_Administrador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        FijarUsuario(ambito, Autenticado);

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
        FijarUsuario(ambito, Anonimo);

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
        FijarUsuario(ambito, Autenticado);

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
        FijarUsuario(ambito, Anonimo);

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
    public async Task El_actor_es_el_mismo_servicio_por_ambito()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        Assert.Same(
            ambito.ServiceProvider.GetRequiredService<ActorDelHost>(),
            ambito.ServiceProvider.GetRequiredService<IActorActual>());
    }

    private static void FijarUsuario(AsyncServiceScope ambito, ClaimsPrincipal usuario)
    {
        var proveedor = Assert.IsAssignableFrom<IHostEnvironmentAuthenticationStateProvider>(
            ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>());
        proveedor.SetAuthenticationState(Task.FromResult(new AuthenticationState(usuario)));
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
