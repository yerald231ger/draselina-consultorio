namespace DraSelina.Consultorio.Kernel;

/// <summary>Identificador de una Cita. Guid v7 tipado: no es intercambiable con ningún otro id (AD-18).</summary>
public readonly record struct CitaId(Guid Valor)
{
    /// <summary>Genera un identificador nuevo, Guid versión 7.</summary>
    public static CitaId Nuevo() => new(Guid.CreateVersion7());

    public override string ToString() => Valor.ToString();
}
