namespace DraSelina.Consultorio.Kernel.Tests;

public class ResultadoTests
{
    [Fact]
    public void Hay_exactamente_cuatro_formas()
    {
        Assert.Equal(
            [FormaDeResultado.Exito, FormaDeResultado.Invalido, FormaDeResultado.NoAutorizado, FormaDeResultado.Fallo],
            Enum.GetValues<FormaDeResultado>());
    }

    [Fact]
    public void Exito_sin_valor()
    {
        var resultado = Resultado.Exito();

        Assert.Equal(FormaDeResultado.Exito, resultado.Forma);
        Assert.True(resultado.EsExito);
        Assert.Empty(resultado.Errores);
        Assert.Null(resultado.Codigo);
    }

    [Fact]
    public void Exito_con_valor()
    {
        var id = CitaId.Nuevo();

        var resultado = Resultado.Exito(id);
        Resultado<CitaId> implicito = id;

        Assert.True(resultado.EsExito);
        Assert.Equal(id, resultado.Valor);
        Assert.True(implicito.EsExito);
        Assert.Equal(id, implicito.Valor);
    }

    [Fact]
    public void Invalido_con_errores_de_dos_campos_deja_cada_error_bajo_el_nombre_de_su_propiedad()
    {
        var resultado = Resultado.Invalido(
        [
            ("Nombre", "El nombre es obligatorio."),
            ("Telefono", "El teléfono es obligatorio."),
            ("Telefono", "El teléfono no es válido."),
        ]);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.False(resultado.EsExito);
        Assert.Null(resultado.Codigo);
        Assert.Equal(["Nombre", "Telefono"], resultado.Errores.Keys.Order());
        Assert.Equal(["El nombre es obligatorio."], resultado.Errores["Nombre"]);
        Assert.Equal(["El teléfono es obligatorio.", "El teléfono no es válido."], resultado.Errores["Telefono"]);
    }

    [Fact]
    public void Invalido_de_un_solo_campo()
    {
        var resultado = Resultado.Invalido("Motivo", "El motivo es obligatorio.");

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal(["El motivo es obligatorio."], Assert.Single(resultado.Errores).Value);
    }

    [Fact]
    public void Invalido_sin_errores_no_existe()
    {
        Assert.Throws<ArgumentException>(() => Resultado.Invalido([]));
    }

    [Fact]
    public void No_autorizado()
    {
        var resultado = Resultado.NoAutorizado();

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.False(resultado.EsExito);
        Assert.Empty(resultado.Errores);
        Assert.Null(resultado.Codigo);
    }

    [Fact]
    public void Fallo_esperado_lleva_el_codigo_que_define_el_modulo()
    {
        var resultado = Resultado.Fallo("agenda.horario_ocupado");

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.False(resultado.EsExito);
        Assert.Equal("agenda.horario_ocupado", resultado.Codigo);
        Assert.Empty(resultado.Errores);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Fallo_sin_codigo_no_existe(string codigo)
    {
        Assert.Throws<ArgumentException>(() => Resultado.Fallo(codigo));
    }

    [Fact]
    public void Un_resultado_no_exitoso_se_lleva_al_tipo_del_llamador_sin_perder_nada()
    {
        var invalido = Resultado.Invalido("Nombre", "El nombre es obligatorio.").Como<CitaId>();
        var noAutorizado = Resultado.NoAutorizado().Como<CitaId>();
        var fallo = Resultado.Fallo("agenda.horario_ocupado").Como<CitaId>();

        Assert.Equal(FormaDeResultado.Invalido, invalido.Forma);
        Assert.Equal(["El nombre es obligatorio."], invalido.Errores["Nombre"]);
        Assert.Equal(FormaDeResultado.NoAutorizado, noAutorizado.Forma);
        Assert.Equal(FormaDeResultado.Fallo, fallo.Forma);
        Assert.Equal("agenda.horario_ocupado", fallo.Codigo);
    }

    [Fact]
    public void Pedir_el_valor_de_un_resultado_no_exitoso_es_un_error_de_programacion()
    {
        var fallo = Resultado.Fallo("agenda.horario_ocupado").Como<CitaId>();

        Assert.Throws<InvalidOperationException>(() => fallo.Valor);
        Assert.Throws<InvalidOperationException>(() => Resultado.Exito().Como<CitaId>());
    }
}
