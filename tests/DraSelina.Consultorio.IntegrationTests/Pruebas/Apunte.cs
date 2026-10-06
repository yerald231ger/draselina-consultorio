namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>El tipo de prueba más pequeño: comprueba que el esquema <c>pruebas</c> guarda y devuelve una fila.</summary>
public sealed class Apunte
{
    public Guid Id { get; set; }

    public string Texto { get; set; } = "";
}
