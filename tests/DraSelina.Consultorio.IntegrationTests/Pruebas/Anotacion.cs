namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>La fila que guarda el comando de prueba <see cref="Anotar"/>.</summary>
public sealed class Anotacion
{
    public Guid Id { get; set; }

    public string Texto { get; set; } = "";
}

/// <summary>El rastro que deja el handler de <see cref="AnotacionHecha"/> cuando el mensaje llega.</summary>
public sealed class RastroDeAnotacion
{
    /// <summary>El id de la anotación que el evento anuncia.</summary>
    public Guid Id { get; set; }

    /// <summary>Si la anotación ya se podía leer cuando corrió el handler del evento.</summary>
    public bool AnotacionConfirmada { get; set; }
}
