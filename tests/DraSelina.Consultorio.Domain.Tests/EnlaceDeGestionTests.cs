using System.Security.Cryptography;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary><see cref="EnlaceDeGestion"/>: el token derivado de la clave y del id, y su hash (AD-19, RN-22).</summary>
public class EnlaceDeGestionTests
{
    private static readonly ClaveDeGestion Clave = ClaveDeGestion.Crear("clave-de-prueba-de-32-bytes-o-mas"u8);
    private static readonly ClaveDeGestion OtraClave = ClaveDeGestion.Crear("otra-clave-de-prueba-de-32-bytes"u8);

    [Fact]
    public void Es_HMAC_SHA256_de_la_clave_y_el_id_en_base64url_sin_relleno_y_su_hash_es_SHA256_del_token()
    {
        var cita = CitaId.Nuevo();

        var enlace = EnlaceDeGestion.Derivar(Clave, cita);

        var esperado = HMACSHA256.HashData("clave-de-prueba-de-32-bytes-o-mas"u8, cita.Valor.ToByteArray(bigEndian: true));
        Assert.Equal(43, enlace.Token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", enlace.Token);
        Assert.Equal(esperado, Bytes(enlace.Token));
        Assert.Equal(SHA256.HashData(esperado), enlace.Hash);
        Assert.Equal(32, enlace.Hash.Length);
    }

    [Fact]
    public void La_misma_Cita_da_el_mismo_token()
    {
        var cita = CitaId.Nuevo();

        Assert.Equal(EnlaceDeGestion.Derivar(Clave, cita).Token, EnlaceDeGestion.Derivar(Clave, cita).Token);
        Assert.Equal(EnlaceDeGestion.Derivar(Clave, cita).Hash, EnlaceDeGestion.Derivar(Clave, cita).Hash);
    }

    [Fact]
    public void Dos_Citas_dan_tokens_y_hashes_distintos()
    {
        var una = EnlaceDeGestion.Derivar(Clave, CitaId.Nuevo());
        var otra = EnlaceDeGestion.Derivar(Clave, CitaId.Nuevo());

        Assert.NotEqual(una.Token, otra.Token);
        Assert.NotEqual(una.Hash, otra.Hash);
    }

    [Fact]
    public void Otra_clave_da_otro_token()
    {
        var cita = CitaId.Nuevo();

        Assert.NotEqual(EnlaceDeGestion.Derivar(Clave, cita).Token, EnlaceDeGestion.Derivar(OtraClave, cita).Token);
    }

    [Fact]
    public void Ni_la_clave_ni_el_token_se_escriben_como_texto()
    {
        var enlace = EnlaceDeGestion.Derivar(Clave, CitaId.Nuevo());

        Assert.DoesNotContain(enlace.Token, enlace.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("clave-de-prueba", Clave.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Una_clave_de_menos_de_32_bytes_no_se_acepta()
    {
        Assert.Throws<ArgumentException>(() => ClaveDeGestion.Crear(new byte[31]));
    }

    private static byte[] Bytes(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
