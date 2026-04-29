namespace Pineda.Facturacion.Application.Common;

public sealed class OperationalOrderConflictException : InvalidOperationException
{
    public OperationalOrderConflictException(string message)
        : base(message)
    {
    }
}
