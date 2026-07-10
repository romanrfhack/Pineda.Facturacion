namespace Pineda.Facturacion.Application.Common;

public sealed class FiscalReceiverSpecialFieldDefinitionConflictException : InvalidOperationException
{
    public FiscalReceiverSpecialFieldDefinitionConflictException(string message)
        : base(message)
    {
    }
}
