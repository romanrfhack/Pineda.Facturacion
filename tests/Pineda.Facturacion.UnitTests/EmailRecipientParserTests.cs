using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.UnitTests;

public class EmailRecipientParserTests
{
    [Theory]
    [InlineData("a@x.com; b@y.com")]
    [InlineData("a@x.com,b@y.com")]
    [InlineData("a@x.com\nb@y.com")]
    public void SplitRecipients_Supports_Configured_Separators(string value)
    {
        var recipients = EmailRecipientParser.SplitRecipients(value);

        Assert.Equal(["a@x.com", "b@y.com"], recipients);
    }

    [Fact]
    public void NormalizeRecipients_Deduplicates_Case_Insensitively()
    {
        var recipients = EmailRecipientParser.NormalizeRecipients(["Cliente@example.com; cliente@example.com", "CLIENTE@example.com"]);

        Assert.Equal(["Cliente@example.com"], recipients);
    }

    [Fact]
    public void FindInvalidRecipients_Returns_Invalid_Entries()
    {
        var invalidRecipients = EmailRecipientParser.FindInvalidRecipients(["cliente@example.com; invalido", "otro-invalido"]);

        Assert.Equal(["invalido", "otro-invalido"], invalidRecipients);
    }

    [Fact]
    public void IsValidEmailAddress_Rejects_Header_Injected_Values()
    {
        Assert.False(EmailRecipientParser.IsValidEmailAddress("cliente@example.com\r\nBcc:otro@example.com"));
    }
}
