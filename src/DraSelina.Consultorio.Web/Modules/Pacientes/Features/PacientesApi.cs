using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using DraSelina.Consultorio.Pacientes.Domain;
using PhoneNumbers;

namespace DraSelina.Consultorio.Pacientes.Features;

/// <summary>
/// <see cref="IPacientesApi"/> sobre el agregado <see cref="Paciente"/>. Cada llamada abre y confirma su propia
/// transacción de Pacientes (AD-28).
/// </summary>
public sealed class PacientesApi(ITransaccionDePacientes transaccion, IPacientes pacientes, IClock reloj) : IPacientesApi
{
    /// <summary>La región de los números sin prefijo internacional.</summary>
    private const string Mexico = "MX";

    // Dos vueltas bastan: si la primera choca con el índice único, otro registró al mismo Paciente en ese instante
    // y la segunda lo encuentra.
    private const int Intentos = 2;

    public async Task<Resultado<PatientId>> BuscarOCrearAsync(
        string nombre,
        string telefono,
        string? correo,
        CancellationToken cancellationToken)
    {
        if (TelefonoDeMexico(telefono) is not { } tel)
        {
            return Resultado.Fallo(CodigosDePacientes.TelefonoInvalido).Como<PatientId>();
        }

        if (NombreCompleto.Crear(nombre) is not { } nom)
        {
            return Resultado.Fallo(CodigosDePacientes.NombreInvalido).Como<PatientId>();
        }

        CorreoElectronico? cor = null;
        if (!string.IsNullOrWhiteSpace(correo) && (cor = CorreoElectronico.Crear(correo)) is null)
        {
            return Resultado.Fallo(CodigosDePacientes.CorreoInvalido).Como<PatientId>();
        }

        for (var intento = 1; ; intento++)
        {
            await using var tx = await transaccion.AbrirAsync(cancellationToken);
            var ahora = reloj.UtcNow;

            var paciente = await pacientes.BuscarAsync(tel, nom, cancellationToken);
            if (paciente is null)
            {
                paciente = Paciente.Registrar(tel, nom, cor, ahora);
                pacientes.Agregar(paciente);
            }
            else if (cor is not null)
            {
                paciente.ActualizarCorreo(cor, ahora);
            }

            if (await pacientes.GuardarAsync(cancellationToken) == GuardadoDePaciente.IdentidadRepetida)
            {
                await tx.DeshacerAsync(cancellationToken);
                if (intento < Intentos)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "El Paciente chocó con el índice de identidad y después no se encontró: la búsqueda y el índice no comparan igual.");
            }

            await tx.ConfirmarAsync(cancellationToken);
            return paciente.Id;
        }
    }

    // libphonenumber decide si el número existe en el plan de numeración de México y lo da en E.164; el dominio
    // solo acepta esa forma.
    private static Telefono? TelefonoDeMexico(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var util = PhoneNumberUtil.GetInstance();
        PhoneNumber numero;
        try
        {
            numero = util.Parse(texto, Mexico);
        }
        catch (NumberParseException)
        {
            return null;
        }

        return util.IsValidNumberForRegion(numero, Mexico)
            ? Telefono.DesdeE164(util.Format(numero, PhoneNumberFormat.E164))
            : null;
    }
}
