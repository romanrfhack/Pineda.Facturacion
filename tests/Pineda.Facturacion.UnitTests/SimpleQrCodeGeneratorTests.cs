using System.Security.Cryptography;
using Pineda.Facturacion.Infrastructure.Documents;

namespace Pineda.Facturacion.UnitTests;

public sealed class SimpleQrCodeGeneratorTests
{
    // Independent golden vectors from Project Nayuki qrcodegen 1.8.0, byte mode,
    // ECC MEDIUM with boostecl=False, automatic mask. Hash row-major 0/1 module bytes.
    // https://github.com/nayuki/QR-Code-generator/blob/master/python/qrcodegen.py
    [Theory]
    [InlineData("HELLO", 21, "B7E69D6FB795BCDAC9EBCAE9D9FB2523F799BE93CB4A4F75CE46E957616C3161")]
    [InlineData("https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id=00000000-0000-4000-8000-000000000057&re=AAA010101AAA&rr=BBB010101BBB&tt=0&fe=12345678", 49, "562A5FF126266B87E3A0A302AA38424B845698295B478996BD0D34AF674B5BC9")]
    public void Encode_Matches_Independent_Byte_Mode_Medium_Ecc_Vector(string text, int size, string hash)
    {
        var qr = SimpleQrCodeGenerator.TryEncode(text);
        Assert.NotNull(qr);
        Assert.Equal(size, qr.Size);
        var modules = Enumerable.Range(0, qr.Size)
            .SelectMany(y => Enumerable.Range(0, qr.Size).Select(x => (byte)(qr.GetModule(x, y) ? 1 : 0)))
            .ToArray();
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(modules)));
    }
}
