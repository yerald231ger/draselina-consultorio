namespace DraSelina.Consultorio.UI.Pages.Administracion;

/// <summary>
/// El texto buscado en Pacientes (entrada 5.7), recordado mientras dura el circuito: al volver del historial con "Volver a
/// Pacientes" la lista muestra la misma búsqueda; al recargar (circuito nuevo) empieza vacía. Nunca va en la dirección
/// ni en el almacenamiento del navegador: es parte de un nombre (AD-11, AD-23; Gerardo, 2026-11-10). Se registra por
/// ámbito en <c>Program.cs</c>: en Interactive Server, un ámbito es un circuito.
/// </summary>
public sealed class BusquedaRecordada
{
    /// <summary>Lo último que se buscó, sin espacios al principio ni al final; vacío al empezar.</summary>
    public string Texto { get; set; } = "";
}
