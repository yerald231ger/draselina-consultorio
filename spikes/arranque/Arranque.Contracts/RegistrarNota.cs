using System.ComponentModel.DataAnnotations;

namespace Arranque.Contracts;

// Tres formas del mismo comando, para contestar la segunda mitad de la pregunta 3:
// qué forma exige el enlace de formularios de Blazor en SSR estático.

/// <summary>Forma A: clase con propiedades <c>set</c> y constructor sin parámetros.</summary>
public sealed class RegistrarNota : IComando
{
    [Required(ErrorMessage = "El texto es obligatorio.")]
    [StringLength(20, ErrorMessage = "El texto no puede pasar de 20 caracteres.")]
    public string? Texto { get; set; }
}

/// <summary>Forma B: record con propiedades <c>init</c> y constructor sin parámetros.</summary>
public sealed record RegistrarNotaInit : IComando
{
    [Required(ErrorMessage = "El texto es obligatorio.")]
    [StringLength(20, ErrorMessage = "El texto no puede pasar de 20 caracteres.")]
    public string? Texto { get; init; }
}

/// <summary>Forma C: record posicional, sin constructor sin parámetros.</summary>
public sealed record RegistrarNotaPosicional(
    [property: Required(ErrorMessage = "El texto es obligatorio.")]
    [property: StringLength(20, ErrorMessage = "El texto no puede pasar de 20 caracteres.")]
    string? Texto) : IComando;
